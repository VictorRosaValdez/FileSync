namespace FileSync.Shared.Protocol;

// Grammatica: antwoord-kopregel = "SYNC/1.0" SP statuscode SP reden
// De reden mag zelf spaties bevatten (bv. "Hash Mismatch"), dus er wordt in
// hoogstens drie delen gesplitst in plaats van op elke spatie.
public sealed record ResponseLine(string Version, int StatusCode, string Reason)
{
    public static ResponseLine Parse(string line)
    {
        string[] parts = line.Split(' ', 3);
        if (parts.Length != 3)
        {
            throw new MalformedMessageException($"Ongeldige statusregel: '{line}'.");
        }

        if (!int.TryParse(parts[1], out int statusCode))
        {
            throw new MalformedMessageException($"Ongeldige statuscode: '{parts[1]}'.");
        }

        return new ResponseLine(parts[0], statusCode, parts[2]);
    }
}
