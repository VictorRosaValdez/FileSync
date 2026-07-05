using System.Collections.Concurrent;
using FileSync.Shared.Hashing;

namespace FileSync.Server.Storage;

// Voorkomt dat elk STAT/MANIFEST-verzoek een bestand opnieuw volledig hasht: de hash
// wordt hergebruikt zolang bestandsgrootte en laatste-schrijftijd niet gewijzigd zijn.
public sealed class HashCache
{
    private sealed record Entry(long Size, DateTime LastWriteUtc, string Hash);

    private readonly ConcurrentDictionary<string, Entry> _entries = new();

    public async Task<string> GetOrComputeHashAsync(string absolutePath)
    {
        var info = new FileInfo(absolutePath);

        if (_entries.TryGetValue(absolutePath, out Entry? cached)
            && cached.Size == info.Length
            && cached.LastWriteUtc == info.LastWriteTimeUtc)
        {
            return cached.Hash;
        }

        await using FileStream stream = File.OpenRead(absolutePath);
        string hash = await FileHasher.ComputeSha256HexAsync(stream);
        _entries[absolutePath] = new Entry(info.Length, info.LastWriteTimeUtc, hash);
        return hash;
    }

    public void Invalidate(string absolutePath) => _entries.TryRemove(absolutePath, out _);

    public void Set(string absolutePath, long size, DateTime lastWriteUtc, string hash) =>
        _entries[absolutePath] = new Entry(size, lastWriteUtc, hash);
}
