using System.Text;
using FileSync.Shared.Protocol;

namespace FileSync.Tests.Protocol;

public class ProtocolStreamReaderTests
{
    private static ProtocolStreamReader ReaderFor(string content)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        return new ProtocolStreamReader(stream);
    }

    [Test]
    public void ReadLine_ParsesSimpleLine_ReturnsContentWithoutCrlf()
    {
        var reader = ReaderFor("HELLO SYNC/1.0\r\n");
        Assert.That(reader.ReadLine(), Is.EqualTo("HELLO SYNC/1.0"));
    }

    [Test]
    public void ReadLine_BareLfWithoutCr_Throws()
    {
        var reader = ReaderFor("HELLO SYNC/1.0\n");
        Assert.Throws<MalformedMessageException>(() => reader.ReadLine());
    }

    [Test]
    public void ReadLine_LineExceeds4096Bytes_ThrowsLineTooLong()
    {
        string tooLong = new string('a', 5000) + "\r\n";
        var reader = ReaderFor(tooLong);
        Assert.Throws<LineTooLongException>(() => reader.ReadLine());
    }

    [Test]
    public void ReadLine_EmptyLine_ReturnsEmptyString()
    {
        var reader = ReaderFor("\r\n");
        Assert.That(reader.ReadLine(), Is.EqualTo(string.Empty));
    }

    [Test]
    public void ReadLine_MultipleSequentialLines_ParsedInOrder()
    {
        var reader = ReaderFor("EERSTE\r\nTWEEDE\r\n");
        Assert.That(reader.ReadLine(), Is.EqualTo("EERSTE"));
        Assert.That(reader.ReadLine(), Is.EqualTo("TWEEDE"));
    }

    [Test]
    public void ReadBody_ReadsExactByteCount_FromLeftoverBufferThenStream()
    {
        // De header eindigt met een lege regel; direct daarna volgt binaire body-data
        // die door Refill() al mee de interne buffer in kan zijn gelezen samen met
        // de laatste header-bytes. ReadBody moet die eerst opmaken.
        byte[] header = Encoding.UTF8.GetBytes("UPLOAD a.txt SYNC/1.0\r\nContent-Length: 5\r\n\r\n");
        byte[] body = { 1, 2, 3, 4, 5 };
        var combined = new byte[header.Length + body.Length];
        header.CopyTo(combined, 0);
        body.CopyTo(combined, header.Length);

        var reader = new ProtocolStreamReader(new MemoryStream(combined));
        reader.ReadLine();
        reader.ReadLine();
        reader.ReadLine();

        var destination = new byte[5];
        reader.ReadBody(destination, 0, 5);
        Assert.That(destination, Is.EqualTo(body));
    }

    [Test]
    public void ReadBody_ConnectionClosedMidBody_ThrowsEndOfStream()
    {
        byte[] header = Encoding.UTF8.GetBytes("BYE SYNC/1.0\r\n\r\n");
        var reader = new ProtocolStreamReader(new MemoryStream(header));
        reader.ReadLine();
        reader.ReadLine();

        var destination = new byte[10];
        Assert.Throws<EndOfStreamException>(() => reader.ReadBody(destination, 0, 10));
    }
}
