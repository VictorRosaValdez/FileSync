using System.Text;
using FileSync.Shared.Hashing;

namespace FileSync.Tests.Hashing;

public class FileHasherTests
{
    [Test]
    public async Task ComputeSha256Hex_EmptyInput_MatchesKnownVector()
    {
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(Array.Empty<byte>()));
        Assert.That(hash, Is.EqualTo("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"));
    }

    [Test]
    public async Task ComputeSha256Hex_Abc_MatchesKnownVector()
    {
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(Encoding.ASCII.GetBytes("abc")));
        Assert.That(hash, Is.EqualTo("ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad"));
    }

    [Test]
    public async Task ComputeSha256Hex_StreamedLargeInput_MatchesReferenceHash()
    {
        var random = new Random(42);
        byte[] data = new byte[10 * 1024 * 1024];
        random.NextBytes(data);

        string streamed = await FileHasher.ComputeSha256HexAsync(new MemoryStream(data), bufferSize: 4096);

        using var sha256 = System.Security.Cryptography.SHA256.Create();
        string reference = Convert.ToHexString(sha256.ComputeHash(data)).ToLowerInvariant();

        Assert.That(streamed, Is.EqualTo(reference));
    }
}
