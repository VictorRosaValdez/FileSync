using System.Text;
using FileSync.Shared.Time;

namespace FileSync.Shared.Manifest;

// MANIFEST-tekstbody (PROTOCOL.md §1): UTF-8, regels gescheiden door LF, velden
// gescheiden door TAB. Bewust geen CRLF hier: dit is een eenvoudigere tekstindeling
// dan de kopregels/headers van het protocol zelf, die wél CRLF gebruiken.
public static class ManifestSerializer
{
    public static string Serialize(IEnumerable<ManifestEntry> entries)
    {
        var builder = new StringBuilder();
        foreach (var entry in entries)
        {
            builder.Append(entry.Hash).Append('\t')
                   .Append(entry.Size).Append('\t')
                   .Append(Iso8601.Format(entry.ModifiedUtc)).Append('\t')
                   .Append(entry.Path).Append('\n');
        }

        return builder.ToString();
    }

    public static List<ManifestEntry> Deserialize(string body)
    {
        var entries = new List<ManifestEntry>();

        foreach (string line in body.Split('\n'))
        {
            if (line.Length == 0)
            {
                continue;
            }

            string[] fields = line.Split('\t');
            if (fields.Length != 4)
            {
                throw new FormatException($"Ongeldige manifestregel (verwacht 4 velden): '{line}'.");
            }

            if (!long.TryParse(fields[1], out long size))
            {
                throw new FormatException($"Ongeldige grootte in manifestregel: '{line}'.");
            }

            DateTime modified = Iso8601.Parse(fields[2]);
            entries.Add(new ManifestEntry(fields[0], size, modified, fields[3]));
        }

        return entries;
    }
}
