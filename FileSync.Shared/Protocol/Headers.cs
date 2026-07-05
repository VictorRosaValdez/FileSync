namespace FileSync.Shared.Protocol;

public sealed class Headers
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public void Set(string name, string value) => _values[name] = value;

    public bool TryGet(string name, out string value) => _values.TryGetValue(name, out value!);

    public string GetString(string name)
    {
        if (!TryGet(name, out string value))
        {
            throw new MalformedMessageException($"Verplichte header ontbreekt: '{name}'.");
        }

        return value;
    }

    // Alle groottes/offsets in het protocol zijn decimaal, 64-bits en niet-negatief
    // (max 19 cijfers, zie PROTOCOL.md §1), zodat bestanden >4 GB correct aangegeven worden.
    public long GetInt64(string name)
    {
        string raw = GetString(name);
        if (raw.Length > 19 || !long.TryParse(raw, out long value) || value < 0)
        {
            throw new MalformedMessageException($"Header '{name}' bevat geen geldig 64-bits getal: '{raw}'.");
        }

        return value;
    }

    public bool TryGetInt64(string name, out long value)
    {
        value = 0;
        return TryGet(name, out string raw) && raw.Length <= 19 && long.TryParse(raw, out value) && value >= 0;
    }

    // Splitst op de EERSTE ": " in plaats van de eerste dubbele punt, omdat headerwaarden
    // (bv. een ISO-8601-tijdstip "2026-07-05T14:03:22Z") zelf dubbele punten bevatten.
    public static void ParseLine(string line, Headers into)
    {
        int separatorIndex = line.IndexOf(": ", StringComparison.Ordinal);
        if (separatorIndex < 0)
        {
            throw new MalformedMessageException($"Ongeldige headerregel: '{line}'.");
        }

        string name = line[..separatorIndex];
        string value = line[(separatorIndex + 2)..];
        into.Set(name, value);
    }
}
