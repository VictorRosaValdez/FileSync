namespace FileSync.Server.Storage;

// Vertaalt een gevalideerd, canoniek protocolpad naar absolute paden op schijf onder
// de opslagmap van de server, en zorgt dat de benodigde submappen bestaan.
public sealed class FileStore
{
    public string StorageRoot { get; }

    public FileStore(string storageRoot)
    {
        StorageRoot = storageRoot;
        Directory.CreateDirectory(storageRoot);
    }

    public string ToAbsolutePath(string canonicalPath) =>
        Path.Combine(StorageRoot, canonicalPath.Replace('/', Path.DirectorySeparatorChar));

    public string ToPartPath(string canonicalPath) => ToAbsolutePath(canonicalPath) + ".part";

    public void EnsureParentDirectoryExists(string absolutePath)
    {
        string? directory = Path.GetDirectoryName(absolutePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }
    }

    public IEnumerable<string> EnumerateCanonicalPaths()
    {
        foreach (string absolutePath in Directory.EnumerateFiles(StorageRoot, "*", SearchOption.AllDirectories))
        {
            if (absolutePath.EndsWith(".part", StringComparison.Ordinal))
            {
                continue;
            }

            yield return Path.GetRelativePath(StorageRoot, absolutePath).Replace(Path.DirectorySeparatorChar, '/');
        }
    }
}
