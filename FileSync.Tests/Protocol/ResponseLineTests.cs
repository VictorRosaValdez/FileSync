using FileSync.Shared.Protocol;

namespace FileSync.Tests.Protocol;

public class ResponseLineTests
{
    [Test]
    public void Parse_ValidResponseLine_ReturnsStatusAndReason()
    {
        var line = ResponseLine.Parse("SYNC/1.0 200 OK");
        Assert.That(line.Version, Is.EqualTo("SYNC/1.0"));
        Assert.That(line.StatusCode, Is.EqualTo(200));
        Assert.That(line.Reason, Is.EqualTo("OK"));
    }

    [Test]
    public void Parse_ReasonContainingSpaces_KeptIntact()
    {
        var line = ResponseLine.Parse("SYNC/1.0 409 Hash Mismatch");
        Assert.That(line.Reason, Is.EqualTo("Hash Mismatch"));
    }

    [Test]
    public void Parse_MissingParts_Throws()
    {
        Assert.Throws<MalformedMessageException>(() => ResponseLine.Parse("SYNC/1.0 200"));
    }

    [Test]
    public void Parse_NonNumericStatusCode_Throws()
    {
        Assert.Throws<MalformedMessageException>(() => ResponseLine.Parse("SYNC/1.0 ABC OK"));
    }
}
