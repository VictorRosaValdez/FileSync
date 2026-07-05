using FileSync.Shared.Time;

namespace FileSync.Client.Sync;

// Hash zoals berekend voor de huidige (Size, ModifiedUtc) van het bestand [Hash], plus
// — los daarvan — de hash zoals die was toen de server dit pad voor het laatst
// aantoonbaar had [SyncedHash]. Deze twee vallen NIET automatisch samen: Hash wordt
// bijgewerkt zodra een bestand gelezen/gehasht wordt (puur om herhashen te vermijden),
// terwijl SyncedHash alleen wordt gezet nadat een upload/download/dedup-check heeft
// bevestigd dat de server exact deze inhoud heeft.
public readonly record struct CachedFileEntry(string Path, long Size, DateTime ModifiedUtc, string Hash, string? SyncedHash);

// Persistente cache per syncmap. Twee taken die bewust gescheiden zijn:
//  1) Rehash-geheugen (TryGetHash/SetHash): voorkomt dat een ongewijzigd bestand bij elke
//     sync-cyclus opnieuw volledig gehasht wordt.
//  2) Sync-status (LastSyncedHashes/MarkSynced): onthoudt per pad de hash zoals die was
//     toen de server dit bestand voor het laatst aantoonbaar had. ManifestDiffer gebruikt
//     ALLEEN dit tweede stuk om een externe verwijdering te herkennen. Zou dit gevuld
//     worden op het moment dat we een hash berekenen (in plaats van pas na een bevestigde
//     sync), dan zou een gloednieuw, nooit-geüpload bestand bij de allereerste cyclus al
//     lijken op "stond eerder in sync, dus nu extern verwijderd" — en per ongeluk lokaal
//     verwijderd worden in plaats van geüpload. Vandaar de strikte scheiding.
public sealed class LocalHashCache
{
    private readonly Dictionary<string, CachedFileEntry> _entries = new();
    private readonly string _cacheFilePath;

    public LocalHashCache(string cacheFilePath)
    {
        _cacheFilePath = cacheFilePath;
    }

    public IReadOnlyDictionary<string, string> LastSyncedHashes =>
        _entries.Values
            .Where(e => e.SyncedHash is not null)
            .ToDictionary(e => e.Path, e => e.SyncedHash!);

    public void Load()
    {
        if (!File.Exists(_cacheFilePath))
        {
            return;
        }

        foreach (string line in File.ReadAllLines(_cacheFilePath))
        {
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split('\t');
            if (fields.Length != 5 || !long.TryParse(fields[1], out long size))
            {
                continue; // corrupte regel: negeren, wordt bij de volgende cyclus opnieuw opgebouwd
            }

            DateTime modified = Iso8601.Parse(fields[2]);
            string? syncedHash = fields[4].Length == 0 ? null : fields[4];
            _entries[fields[0]] = new CachedFileEntry(fields[0], size, modified, fields[3], syncedHash);
        }
    }

    public void Save()
    {
        IEnumerable<string> lines = _entries.Values.Select(e =>
            $"{e.Path}\t{e.Size}\t{Iso8601.Format(e.ModifiedUtc)}\t{e.Hash}\t{e.SyncedHash}");
        File.WriteAllLines(_cacheFilePath, lines);
    }

    public bool TryGetHash(string path, long size, DateTime modifiedUtc, out string hash)
    {
        if (_entries.TryGetValue(path, out CachedFileEntry entry) && entry.Size == size && entry.ModifiedUtc == modifiedUtc)
        {
            hash = entry.Hash;
            return true;
        }

        hash = string.Empty;
        return false;
    }

    // Werkt alleen het rehash-geheugen bij; raakt de sync-status niet aan.
    public void SetHash(string path, long size, DateTime modifiedUtc, string hash)
    {
        string? existingSyncedHash = _entries.TryGetValue(path, out CachedFileEntry existing) ? existing.SyncedHash : null;
        _entries[path] = new CachedFileEntry(path, size, modifiedUtc, hash, existingSyncedHash);
    }

    // Bevestigt dat de server deze exacte inhoud nu heeft (na 201/204, of na een
    // geverifieerde download). Dit — en alleen dit — bepaalt LastSyncedHashes.
    public void MarkSynced(string path, long size, DateTime modifiedUtc, string hash) =>
        _entries[path] = new CachedFileEntry(path, size, modifiedUtc, hash, hash);

    public void Remove(string path) => _entries.Remove(path);
}
