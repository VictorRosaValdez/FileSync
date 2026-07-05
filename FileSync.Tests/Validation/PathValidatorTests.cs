using System.Text;
using FileSync.Shared.Validation;

namespace FileSync.Tests.Validation;

public class PathValidatorTests
{
    [Test]
    public void TryValidate_SimpleRelativePath_Succeeds()
    {
        bool ok = PathValidator.TryValidate("docs/rapport.pdf", out string canonical, out _);
        Assert.That(ok, Is.True);
        Assert.That(canonical, Is.EqualTo("docs/rapport.pdf"));
    }

    [Test]
    public void TryValidate_LeadingSlash_Fails()
    {
        bool ok = PathValidator.TryValidate("/docs/rapport.pdf", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.LeadingSlash));
    }

    [Test]
    public void TryValidate_DotDotSegment_Fails()
    {
        bool ok = PathValidator.TryValidate("../etc/passwd", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.TraversalSegment));
    }

    [Test]
    public void TryValidate_DotDotSegmentInMiddle_Fails()
    {
        bool ok = PathValidator.TryValidate("docs/../secret.txt", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.TraversalSegment));
    }

    [Test]
    public void TryValidate_SingleDotSegment_Fails()
    {
        bool ok = PathValidator.TryValidate("./rapport.pdf", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.TraversalSegment));
    }

    [TestCase("a\\b")]
    [TestCase("a:b")]
    [TestCase("a*b")]
    [TestCase("a?b")]
    [TestCase("a\"b")]
    [TestCase("a<b")]
    [TestCase("a>b")]
    [TestCase("a|b")]
    public void TryValidate_ForbiddenChars_Fails(string segment)
    {
        bool ok = PathValidator.TryValidate(segment, out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.ForbiddenChar));
    }

    [Test]
    public void TryValidate_ControlChar_Fails()
    {
        bool ok = PathValidator.TryValidate("bestand.txt", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.ControlChar));
    }

    [Test]
    public void TryValidate_SegmentEndingInSpace_Fails()
    {
        bool ok = PathValidator.TryValidate("bestand ", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.TrailingSpaceOrDot));
    }

    [Test]
    public void TryValidate_SegmentEndingInDot_Fails()
    {
        bool ok = PathValidator.TryValidate("bestand.", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.TrailingSpaceOrDot));
    }

    [TestCase("CON")]
    [TestCase("con.txt")]
    [TestCase("COM1")]
    [TestCase("LPT9")]
    [TestCase("NUL")]
    public void TryValidate_WindowsReservedNames_Fails(string segment)
    {
        bool ok = PathValidator.TryValidate(segment, out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.ReservedName));
    }

    [Test]
    public void TryValidate_SegmentExceeds240Utf8Bytes_Fails()
    {
        // 'é' als samengesteld teken (NFC) is 2 UTF-8-bytes, dus 130 tekens = 260 bytes.
        string segment = new string('é', 130);
        bool ok = PathValidator.TryValidate(segment, out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.SegmentTooLong));
    }

    [Test]
    public void TryValidate_FullPathExceeds2048Bytes_Fails()
    {
        string longPath = string.Join("/", Enumerable.Repeat(new string('a', 200), 12));
        bool ok = PathValidator.TryValidate(longPath, out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.PathTooLong));
    }

    [Test]
    public void TryValidate_DecomposedUnicodeInput_NormalizesToNfc()
    {
        // "é" opgebouwd uit 'e' + combinerend accent (NFD) moet gelijk worden aan het
        // samengestelde teken (NFC).
        string decomposed = "café.txt";
        string precomposed = "café.txt".Normalize(NormalizationForm.FormC);

        bool ok = PathValidator.TryValidate(decomposed, out string canonical, out _);
        Assert.That(ok, Is.True);
        Assert.That(canonical, Is.EqualTo(precomposed));
    }

    [Test]
    public void TryValidate_UnicodeSubfoldersAndForwardSlashes_Succeeds()
    {
        bool ok = PathValidator.TryValidate("mappen/café münchen/verslag.txt", out string canonical, out _);
        Assert.That(ok, Is.True);
        Assert.That(canonical, Is.EqualTo("mappen/café münchen/verslag.txt"));
    }

    [Test]
    public void TryValidate_DoubleSlash_Fails()
    {
        bool ok = PathValidator.TryValidate("docs//rapport.pdf", out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.ForbiddenChar));
    }

    [Test]
    public void TryValidate_EmptyString_Fails()
    {
        bool ok = PathValidator.TryValidate(string.Empty, out _, out var error);
        Assert.That(ok, Is.False);
        Assert.That(error, Is.EqualTo(PathValidationError.Empty));
    }
}
