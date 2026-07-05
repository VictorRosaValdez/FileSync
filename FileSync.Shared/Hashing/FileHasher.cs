using System.Security.Cryptography;

namespace FileSync.Shared.Hashing;

// Berekent de SHA-256 van een volledig bestand in vaste blokken van 64 KiB, zodat
// bestanden groter dan het beschikbare geheugen (>4 GB) probleemloos gehasht kunnen
// worden zonder ooit het hele bestand in het geheugen te laden.
public static class FileHasher
{
    public const int DefaultBufferSize = 64 * 1024;

    public static async Task<string> ComputeSha256HexAsync(Stream stream, int bufferSize = DefaultBufferSize)
    {
        using var sha256 = SHA256.Create();
        byte[] buffer = new byte[bufferSize];
        int bytesRead;

        while ((bytesRead = await stream.ReadAsync(buffer)) > 0)
        {
            sha256.TransformBlock(buffer, 0, bytesRead, null, 0);
        }

        sha256.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return Convert.ToHexString(sha256.Hash!).ToLowerInvariant();
    }
}
