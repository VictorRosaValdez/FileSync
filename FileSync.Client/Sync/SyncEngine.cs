using System.Security.Cryptography.X509Certificates;
using FileSync.Client.Cli;
using FileSync.Client.Net;
using FileSync.Client.Security;
using FileSync.Shared.Hashing;
using FileSync.Shared.Logging;
using FileSync.Shared.Manifest;
using FileSync.Shared.Protocol;

namespace FileSync.Client.Sync;

// Orchestreert één volledige sync-cyclus: HELLO → MANIFEST → diff → per bestand
// UPLOAD/DOWNLOAD/DELETE → BYE. Elke cyclus gebruikt een verse SyncSession (dus een
// verse TCP-verbinding); een cyclus die halverwege faalt (bv. kabel eruit) wordt simpelweg
// afgebroken en hervat vanzelf bij de volgende cyclus via STAT/Part-Size — er is geen
// aparte retrylaag nodig.
public sealed class SyncEngine
{
    private readonly ClientOptions _options;
    private readonly IConsoleLogger _logger;
    private readonly LocalHashCache _cache;
    private readonly X509Certificate2? _trustedServerCertificate;

    public SyncEngine(ClientOptions options, IConsoleLogger logger)
    {
        _options = options;
        _logger = logger;
        _cache = new LocalHashCache(options.CacheFilePath);
        _cache.Load();

        _trustedServerCertificate = options.TrustedServerCertificatePath is { } path
            ? TrustedCertificateLoader.Load(path)
            : null;
    }

    public async Task RunOneCycleAsync()
    {
        await using SyncSession session = await SyncSession.ConnectAsync(_options.Host, _options.Port, _trustedServerCertificate);

        session.Hello(_options.ClientId);
        Dictionary<string, ManifestEntry> remote = session.Manifest().ToDictionary(e => e.Path);

        Dictionary<string, LocalFileState> local = await BuildLocalStateAsync();
        SyncPlan plan = ManifestDiffer.Diff(local, remote, _cache.LastSyncedHashes);

        foreach (LocalDeleteAction localDelete in plan.LocalDeletes)
        {
            ExecuteLocalDelete(localDelete);
        }

        foreach (UploadAction upload in plan.Uploads)
        {
            await ExecuteUploadAsync(session, upload);
        }

        foreach (DownloadAction download in plan.Downloads)
        {
            await ExecuteDownloadAsync(session, download);
        }

        foreach (DeleteAction delete in plan.ServerDeletes)
        {
            ExecuteDelete(session, delete);
        }

        session.Bye();
        _cache.Save();
    }

    private async Task<Dictionary<string, LocalFileState>> BuildLocalStateAsync()
    {
        var local = new Dictionary<string, LocalFileState>();

        foreach (LocalFileInfo file in FolderScanner.Scan(_options.Folder, _options.CacheFilePath))
        {
            if (!_cache.TryGetHash(file.CanonicalPath, file.Size, file.ModifiedUtc, out string hash))
            {
                await using FileStream stream = File.OpenRead(file.AbsolutePath);
                hash = await FileHasher.ComputeSha256HexAsync(stream);
                _cache.SetHash(file.CanonicalPath, file.Size, file.ModifiedUtc, hash);
            }

            local[file.CanonicalPath] = new LocalFileState(file.Size, file.ModifiedUtc, hash, file.AbsolutePath);
        }

        return local;
    }

    private async Task ExecuteUploadAsync(SyncSession session, UploadAction upload)
    {
        try
        {
            // Client-side dedup vóór elke UPLOAD (PROTOCOL.md §5 volgorde: STAT gaat altijd
            // aan UPLOAD vooraf). Dit is onze primaire garantie dat een ongewijzigd bestand
            // nooit opnieuw wordt overgedragen: bij een hash-match wordt UPLOAD hier al
            // overgeslagen, dus gaat er geen enkele body-byte over de lijn.
            StatResult stat = session.Stat(upload.CanonicalPath);
            if (string.Equals(stat.Hash, upload.Hash, StringComparison.OrdinalIgnoreCase))
            {
                _cache.MarkSynced(upload.CanonicalPath, upload.Size, upload.ModifiedUtc, upload.Hash);
                return;
            }

            long offset = stat.PartSize > 0 && stat.PartSize < upload.Size ? stat.PartSize : 0;

            using FileStream fileStream = File.OpenRead(upload.AbsolutePath);
            UploadResult result = await session.UploadAsync(upload.CanonicalPath, fileStream, offset, upload.Size, upload.Hash, upload.ModifiedUtc);

            if (result.StatusCode is StatusCodes.Created or StatusCodes.Identical)
            {
                _cache.MarkSynced(upload.CanonicalPath, upload.Size, upload.ModifiedUtc, upload.Hash);
            }
            else
            {
                _logger.Warn($"Upload van '{upload.CanonicalPath}' gaf status {result.StatusCode}; wordt volgende cyclus hervat.");
            }
        }
        catch (Exception ex)
        {
            _logger.Warn($"Upload van '{upload.CanonicalPath}' mislukt: {ex.Message}. Wordt volgende cyclus hervat.");
        }
    }

    private async Task ExecuteDownloadAsync(SyncSession session, DownloadAction download)
    {
        string absolutePath = Path.Combine(_options.Folder, download.CanonicalPath.Replace('/', Path.DirectorySeparatorChar));
        string partPath = absolutePath + ".part";

        try
        {
            string? directory = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            await using (var partStream = new FileStream(partPath, FileMode.Create, FileAccess.Write))
            {
                await session.DownloadAsync(download.CanonicalPath, 0, partStream);
            }

            // Zelfde atomiciteitstruc als de server: pas hernoemen na hash-verificatie, zodat
            // een onderbroken download nooit een half bestand op de zichtbare naam achterlaat.
            string actualHash;
            using (FileStream verifyStream = File.OpenRead(partPath))
            {
                actualHash = await FileHasher.ComputeSha256HexAsync(verifyStream);
            }

            if (!string.Equals(actualHash, download.Hash, StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(partPath);
                _logger.Warn($"Download van '{download.CanonicalPath}' had een afwijkende hash; wordt volgende cyclus hervat.");
                return;
            }

            File.Move(partPath, absolutePath, overwrite: true);
            _cache.MarkSynced(download.CanonicalPath, download.Size, File.GetLastWriteTimeUtc(absolutePath), actualHash);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Download van '{download.CanonicalPath}' mislukt: {ex.Message}. Wordt volgende cyclus hervat.");
        }
    }

    private void ExecuteDelete(SyncSession session, DeleteAction delete)
    {
        try
        {
            session.Delete(delete.CanonicalPath);
            _cache.Remove(delete.CanonicalPath);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Verwijderen van '{delete.CanonicalPath}' op de server mislukt: {ex.Message}.");
        }
    }

    // Spiegelt een verwijdering die elders (op de server, door een andere client) al
    // heeft plaatsgevonden: geen serververzoek nodig, alleen het lokale bestand en de
    // cache-vermelding opruimen.
    private void ExecuteLocalDelete(LocalDeleteAction localDelete)
    {
        try
        {
            if (File.Exists(localDelete.AbsolutePath))
            {
                File.Delete(localDelete.AbsolutePath);
            }

            _cache.Remove(localDelete.CanonicalPath);
        }
        catch (Exception ex)
        {
            _logger.Warn($"Lokaal verwijderen van '{localDelete.CanonicalPath}' mislukt: {ex.Message}.");
        }
    }
}
