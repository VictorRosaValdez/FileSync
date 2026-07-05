using FileSync.Shared.Protocol;

namespace FileSync.Tests.Protocol;

public class RequestLineTests
{
    [Test]
    public void Parse_ValidHelloLine_ReturnsCommandAndVersion()
    {
        var line = RequestLine.Parse("HELLO SYNC/1.0");
        Assert.That(line.Command, Is.EqualTo("HELLO"));
        Assert.That(line.Path, Is.Null);
        Assert.That(line.Version, Is.EqualTo("SYNC/1.0"));
    }

    [Test]
    public void Parse_ValidCommandWithPath_ReturnsPath()
    {
        var line = RequestLine.Parse("UPLOAD docs/rapport.pdf SYNC/1.0");
        Assert.That(line.Command, Is.EqualTo("UPLOAD"));
        Assert.That(line.Path, Is.EqualTo("docs/rapport.pdf"));
        Assert.That(line.Version, Is.EqualTo("SYNC/1.0"));
    }

    [Test]
    public void Parse_UnsupportedVersionToken_StillParsesAsVersion()
    {
        // De server moet dit later zelf afwijzen met 505 — de parser mag niet vooraf falen.
        var line = RequestLine.Parse("HELLO SYNC/9.9");
        Assert.That(line.Version, Is.EqualTo("SYNC/9.9"));
    }

    [Test]
    public void Parse_MissingVersionToken_Throws()
    {
        Assert.Throws<MalformedMessageException>(() => RequestLine.Parse("HELLO"));
    }

    [Test]
    public void Parse_PathContainingSpaces_ReconstructsFullPath()
    {
        // Een pad mag spaties bevatten (PROTOCOL.md §2 verbiedt ze niet), dus alles
        // tussen het commando en de versie hoort bij het pad, ook als dat meerdere
        // spatie-gescheiden delen oplevert.
        var line = RequestLine.Parse("STAT mappen/café münchen/verslag.txt SYNC/1.0");
        Assert.That(line.Command, Is.EqualTo("STAT"));
        Assert.That(line.Path, Is.EqualTo("mappen/café münchen/verslag.txt"));
        Assert.That(line.Version, Is.EqualTo("SYNC/1.0"));
    }
}
