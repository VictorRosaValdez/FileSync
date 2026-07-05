using System.Globalization;

namespace FileSync.Shared.Time;

public static class Iso8601
{
    private const string FormatString = "yyyy-MM-ddTHH:mm:ssZ";

    public static string Format(DateTime utc) =>
        utc.ToUniversalTime().ToString(FormatString, CultureInfo.InvariantCulture);

    public static DateTime Parse(string value) =>
        DateTime.ParseExact(value, FormatString, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);
}
