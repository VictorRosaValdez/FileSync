using FileSync.Shared.Time;

namespace FileSync.Tests.Time;

public class Iso8601Tests
{
    [Test]
    public void Format_UtcDateTime_ProducesExpectedString()
    {
        var dt = new DateTime(2026, 7, 5, 14, 3, 22, DateTimeKind.Utc);
        Assert.That(Iso8601.Format(dt), Is.EqualTo("2026-07-05T14:03:22Z"));
    }

    [Test]
    public void Parse_ValidString_RoundTripsToSameInstant()
    {
        DateTime parsed = Iso8601.Parse("2026-07-05T14:03:22Z");
        Assert.That(Iso8601.Format(parsed), Is.EqualTo("2026-07-05T14:03:22Z"));
    }
}
