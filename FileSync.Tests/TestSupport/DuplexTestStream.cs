namespace FileSync.Tests.TestSupport;

// Eenvoudige stream voor tests: lezen gebeurt uit een vooraf ingesteld invoerbuffer,
// schrijven gaat naar een apart uitvoerbuffer dat de test achteraf kan inspecteren.
// Bootst zo het gedrag van een NetworkStream na zonder een echte socket nodig te hebben.
public sealed class DuplexTestStream : Stream
{
    private readonly MemoryStream _input;
    private readonly MemoryStream _output = new();

    public DuplexTestStream(byte[] inputBytes)
    {
        _input = new MemoryStream(inputBytes);
    }

    public byte[] OutputBytes => _output.ToArray();

    public override int Read(byte[] buffer, int offset, int count) => _input.Read(buffer, offset, count);

    public override void Write(byte[] buffer, int offset, int count) => _output.Write(buffer, offset, count);

    public override void Flush()
    {
    }

    public override bool CanRead => true;

    public override bool CanWrite => true;

    public override bool CanSeek => false;

    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => throw new NotSupportedException();
        set => throw new NotSupportedException();
    }

    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    public override void SetLength(long value) => throw new NotSupportedException();
}
