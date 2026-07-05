namespace FileSync.Shared.Protocol;

// Grammatica: kopregel = COMMANDO [ SP pad ] SP "SYNC/1.0"
// Version wordt bewust generiek geparsed (niet als vaste literal), zodat een client
// ook een niet-ondersteunde versie zoals "SYNC/9.9" kan versturen en de server dat
// zelf kan afwijzen met 505.
public sealed record RequestLine(string Command, string? Path, string Version)
{
    public static RequestLine Parse(string line)
    {
        string[] parts = line.Split(' ');

        if (parts.Length < 2)
        {
            throw new MalformedMessageException($"Ongeldige kopregel: '{line}'.");
        }

        string command = parts[0];
        string version = parts[^1];

        if (parts.Length == 2)
        {
            return new RequestLine(command, null, version);
        }

        // Het pad zelf mag spaties bevatten (bv. "mappen/café münchen/verslag.txt"),
        // dus alles tussen het eerste en het laatste token vormt samen het pad.
        string path = string.Join(' ', parts[1..^1]);
        return new RequestLine(command, path, version);
    }
}
