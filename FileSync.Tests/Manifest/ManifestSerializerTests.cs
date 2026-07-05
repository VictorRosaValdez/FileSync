using FileSync.Shared.Manifest;

namespace FileSync.Tests.Manifest;

public class ManifestSerializerTests
{
    private static readonly DateTime SampleModified = new(2026, 7, 5, 14, 3, 22, DateTimeKind.Utc);

    [Test]
    public void Serialize_SingleEntry_TabSeparatedLfTerminated()
    {
        var entries = new[] { new ManifestEntry("9f2a", 1024, SampleModified, "docs/rapport.pdf") };
        string body = ManifestSerializer.Serialize(entries);
        Assert.That(body, Is.EqualTo("9f2a\t1024\t2026-07-05T14:03:22Z\tdocs/rapport.pdf\n"));
    }

    [Test]
    public void Serialize_MultipleEntries_JoinedByLfOnly_NoCr()
    {
        var entries = new[]
        {
            new ManifestEntry("aaa", 1, SampleModified, "a.txt"),
            new ManifestEntry("bbb", 2, SampleModified, "b.txt"),
        };
        string body = ManifestSerializer.Serialize(entries);
        Assert.That(body, Does.Not.Contain("\r"));
        Assert.That(body.Split('\n').Length, Is.EqualTo(3)); // 2 regels + lege staart na laatste \n
    }

    [Test]
    public void Deserialize_ValidBody_ParsesAllFields()
    {
        string body = "9f2a\t1024\t2026-07-05T14:03:22Z\tdocs/rapport.pdf\n";
        var entries = ManifestSerializer.Deserialize(body);

        Assert.That(entries, Has.Count.EqualTo(1));
        Assert.That(entries[0].Hash, Is.EqualTo("9f2a"));
        Assert.That(entries[0].Size, Is.EqualTo(1024));
        Assert.That(entries[0].ModifiedUtc, Is.EqualTo(SampleModified));
        Assert.That(entries[0].Path, Is.EqualTo("docs/rapport.pdf"));
    }

    [Test]
    public void Deserialize_MalformedLine_Throws()
    {
        string body = "9f2a\t1024\tdocs/rapport.pdf\n"; // ontbrekend veld
        Assert.Throws<FormatException>(() => ManifestSerializer.Deserialize(body));
    }

    [Test]
    public void Deserialize_EmptyBody_ReturnsEmptyList()
    {
        var entries = ManifestSerializer.Deserialize(string.Empty);
        Assert.That(entries, Is.Empty);
    }

    [Test]
    public void RoundTrip_SerializeThenDeserialize_PreservesData()
    {
        var original = new[]
        {
            new ManifestEntry("aaa", 100, SampleModified, "a.txt"),
            new ManifestEntry("bbb", 200, SampleModified, "sub/b.txt"),
        };

        string body = ManifestSerializer.Serialize(original);
        var roundTripped = ManifestSerializer.Deserialize(body);

        Assert.That(roundTripped, Is.EqualTo(original));
    }
}
