using FileSync.Shared.Protocol;

namespace FileSync.Tests.Protocol;

public class HeadersTests
{
    [Test]
    public void ParseLine_HeaderMissingColonSpace_Throws()
    {
        var headers = new Headers();
        Assert.Throws<MalformedMessageException>(() => Headers.ParseLine("Client-Id-werkplek-anna", headers));
    }

    [Test]
    public void ParseLine_ValueContainingColons_PreservedAsSingleValue()
    {
        var headers = new Headers();
        Headers.ParseLine("Modified: 2026-07-05T14:03:22Z", headers);
        Assert.That(headers.GetString("Modified"), Is.EqualTo("2026-07-05T14:03:22Z"));
    }

    [Test]
    public void Headers_LookupIsCaseInsensitive()
    {
        var headers = new Headers();
        Headers.ParseLine("Client-Id: werkplek-anna", headers);
        Assert.That(headers.GetString("client-id"), Is.EqualTo("werkplek-anna"));
    }

    [Test]
    public void GetString_MissingHeader_Throws()
    {
        var headers = new Headers();
        Assert.Throws<MalformedMessageException>(() => headers.GetString("Client-Id"));
    }

    [Test]
    public void GetInt64_ValidValue_ReturnsParsedNumber()
    {
        var headers = new Headers();
        Headers.ParseLine("Content-Length: 5368709120", headers);
        Assert.That(headers.GetInt64("Content-Length"), Is.EqualTo(5368709120L));
    }

    [Test]
    public void GetInt64_NonNumericValue_Throws()
    {
        var headers = new Headers();
        Headers.ParseLine("Content-Length: abc", headers);
        Assert.Throws<MalformedMessageException>(() => headers.GetInt64("Content-Length"));
    }

    [Test]
    public void GetInt64_NegativeValue_Throws()
    {
        var headers = new Headers();
        Headers.ParseLine("Content-Length: -5", headers);
        Assert.Throws<MalformedMessageException>(() => headers.GetInt64("Content-Length"));
    }

    [Test]
    public void GetInt64_MoreThan19Digits_Throws()
    {
        var headers = new Headers();
        Headers.ParseLine("Content-Length: 12345678901234567890", headers);
        Assert.Throws<MalformedMessageException>(() => headers.GetInt64("Content-Length"));
    }
}
