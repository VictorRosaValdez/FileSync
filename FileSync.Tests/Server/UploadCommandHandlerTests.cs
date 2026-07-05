using System.Text;
using FileSync.Server.CommandHandlers;
using FileSync.Server.Connection;
using FileSync.Server.Locking;
using FileSync.Server.Storage;
using FileSync.Shared.Hashing;
using FileSync.Shared.Protocol;
using FileSync.Tests.TestSupport;

namespace FileSync.Tests.Server;

public class UploadCommandHandlerTests
{
    private string _storageRoot = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _storageRoot = Path.Combine(Path.GetTempPath(), "filesync-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_storageRoot);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_storageRoot))
        {
            Directory.Delete(_storageRoot, recursive: true);
        }
    }

    private static ParsedRequest BuildUploadRequest(string? path, long offset, long contentLength, long totalLength, string hash)
    {
        var headers = new Headers();
        headers.Set("Offset", offset.ToString());
        headers.Set("Content-Length", contentLength.ToString());
        headers.Set("Total-Length", totalLength.ToString());
        headers.Set("Hash", hash);
        headers.Set("Modified", "2026-07-05T14:03:22Z");
        return new ParsedRequest(new RequestLine("UPLOAD", path, "SYNC/1.0"), headers);
    }

    private CommandContext CreateContext(byte[] bodyBytes, out DuplexTestStream stream, PathLockRegistry? lockRegistry = null)
    {
        stream = new DuplexTestStream(bodyBytes);
        return new CommandContext(
            new ProtocolStreamReader(stream),
            new ProtocolWriter(stream),
            new ServerSessionState(),
            new FileStore(_storageRoot),
            new HashCache(),
            lockRegistry ?? new PathLockRegistry(),
            new NullLogger());
    }

    private static string ResponseStatusLine(DuplexTestStream stream) =>
        Encoding.UTF8.GetString(stream.OutputBytes).Split("\r\n")[0];

    [Test]
    public async Task Upload_OffsetZero_FreshFile_Returns201AndRenamesAtomically()
    {
        byte[] content = Encoding.UTF8.GetBytes("hallo wereld");
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(content));

        var request = BuildUploadRequest("a.txt", 0, content.Length, content.Length, hash);
        var ctx = CreateContext(content, out var stream);

        await UploadCommandHandler.HandleAsync(request, ctx);

        Assert.That(ResponseStatusLine(stream), Is.EqualTo("SYNC/1.0 201 Created"));
        Assert.That(File.Exists(Path.Combine(_storageRoot, "a.txt")), Is.True);
        Assert.That(File.Exists(Path.Combine(_storageRoot, "a.txt.part")), Is.False);
    }

    [Test]
    public async Task Upload_PartialChunk_Returns200AndKeepsPartFile()
    {
        byte[] fullContent = Encoding.UTF8.GetBytes("hallo wereld, dit is een test");
        byte[] firstChunk = fullContent[..10];
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(fullContent));

        var request = BuildUploadRequest("a.txt", 0, firstChunk.Length, fullContent.Length, hash);
        var ctx = CreateContext(firstChunk, out var stream);

        await UploadCommandHandler.HandleAsync(request, ctx);

        Assert.That(ResponseStatusLine(stream), Is.EqualTo("SYNC/1.0 200 OK"));
        string partPath = Path.Combine(_storageRoot, "a.txt.part");
        Assert.That(File.Exists(partPath), Is.True);
        Assert.That(new FileInfo(partPath).Length, Is.EqualTo(10));
    }

    [Test]
    public async Task Upload_ResumeWithCorrectOffset_AppendsAndCompletes()
    {
        byte[] fullContent = Encoding.UTF8.GetBytes("hallo wereld, dit is een test");
        byte[] firstChunk = fullContent[..10];
        byte[] secondChunk = fullContent[10..];
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(fullContent));

        var firstRequest = BuildUploadRequest("a.txt", 0, firstChunk.Length, fullContent.Length, hash);
        await UploadCommandHandler.HandleAsync(firstRequest, CreateContext(firstChunk, out _));

        var secondRequest = BuildUploadRequest("a.txt", firstChunk.Length, secondChunk.Length, fullContent.Length, hash);
        var secondCtx = CreateContext(secondChunk, out var secondStream);

        await UploadCommandHandler.HandleAsync(secondRequest, secondCtx);

        Assert.That(ResponseStatusLine(secondStream), Is.EqualTo("SYNC/1.0 201 Created"));
        byte[] finalBytes = await File.ReadAllBytesAsync(Path.Combine(_storageRoot, "a.txt"));
        Assert.That(finalBytes, Is.EqualTo(fullContent));
    }

    [Test]
    public async Task Upload_ResumeWithMismatchedOffset_Returns400()
    {
        byte[] fullContent = Encoding.UTF8.GetBytes("hallo wereld, dit is een test");
        byte[] firstChunk = fullContent[..10];
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(fullContent));

        var firstRequest = BuildUploadRequest("a.txt", 0, firstChunk.Length, fullContent.Length, hash);
        await UploadCommandHandler.HandleAsync(firstRequest, CreateContext(firstChunk, out _));

        // Client denkt dat er al 5 bytes staan, maar op de server staan er 10 → mismatch.
        byte[] wrongOffsetChunk = fullContent[5..];
        var secondRequest = BuildUploadRequest("a.txt", 5, wrongOffsetChunk.Length, fullContent.Length, hash);
        var secondCtx = CreateContext(wrongOffsetChunk, out var secondStream);

        await UploadCommandHandler.HandleAsync(secondRequest, secondCtx);

        Assert.That(ResponseStatusLine(secondStream), Is.EqualTo("SYNC/1.0 400 Bad Request"));
    }

    [Test]
    public async Task Upload_FinalChunkHashMismatch_Returns409AndDeletesPart()
    {
        byte[] content = Encoding.UTF8.GetBytes("hallo wereld");
        string wrongHash = new string('0', 64);

        var request = BuildUploadRequest("a.txt", 0, content.Length, content.Length, wrongHash);
        var ctx = CreateContext(content, out var stream);

        await UploadCommandHandler.HandleAsync(request, ctx);

        Assert.That(ResponseStatusLine(stream), Is.EqualTo("SYNC/1.0 409 Hash Mismatch"));
        Assert.That(File.Exists(Path.Combine(_storageRoot, "a.txt.part")), Is.False);
        Assert.That(File.Exists(Path.Combine(_storageRoot, "a.txt")), Is.False);
    }

    [Test]
    public async Task Upload_DedupHit_Returns204()
    {
        byte[] content = Encoding.UTF8.GetBytes("hallo wereld");
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(content));

        var firstRequest = BuildUploadRequest("a.txt", 0, content.Length, content.Length, hash);
        await UploadCommandHandler.HandleAsync(firstRequest, CreateContext(content, out _));

        var secondRequest = BuildUploadRequest("a.txt", 0, content.Length, content.Length, hash);
        var secondCtx = CreateContext(content, out var secondStream);

        await UploadCommandHandler.HandleAsync(secondRequest, secondCtx);

        Assert.That(ResponseStatusLine(secondStream), Is.EqualTo("SYNC/1.0 204 Identical"));
    }

    [Test]
    public async Task Upload_ConcurrentWriterSamePath_Returns423()
    {
        var lockRegistry = new PathLockRegistry();
        lockRegistry.TryAcquire("a.txt");

        byte[] content = Encoding.UTF8.GetBytes("hallo wereld");
        string hash = await FileHasher.ComputeSha256HexAsync(new MemoryStream(content));
        var request = BuildUploadRequest("a.txt", 0, content.Length, content.Length, hash);
        var ctx = CreateContext(content, out var stream, lockRegistry);

        await UploadCommandHandler.HandleAsync(request, ctx);

        Assert.That(ResponseStatusLine(stream), Is.EqualTo("SYNC/1.0 423 Locked"));
    }

    [Test]
    public async Task Upload_InvalidPath_Returns400BeforeTouchingDisk()
    {
        byte[] content = Encoding.UTF8.GetBytes("data");
        var request = BuildUploadRequest("../escape.txt", 0, content.Length, content.Length, new string('a', 64));
        var ctx = CreateContext(content, out var stream);

        await UploadCommandHandler.HandleAsync(request, ctx);

        Assert.That(ResponseStatusLine(stream), Is.EqualTo("SYNC/1.0 400 Bad Request"));
        Assert.That(Directory.EnumerateFileSystemEntries(_storageRoot), Is.Empty);
    }
}
