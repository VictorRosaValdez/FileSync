namespace FileSync.Shared.Protocol;

// Bundelt het lezen van een kopregel + headers tot aan de afsluitende lege regel.
// Leest bewust NOOIT de body: alleen de aanroeper (commandhandler, of SyncSession bij
// de client) weet wat daarmee moet gebeuren (wegschrijven naar een .part-bestand,
// of in een buffer voor tests), dus die roept ProtocolStreamReader.ReadBody zelf aan.
public static class ProtocolMessageReader
{
    public static ParsedRequest ReadRequest(ProtocolStreamReader reader)
    {
        var line = RequestLine.Parse(reader.ReadLine());
        var headers = ReadHeaders(reader);
        return new ParsedRequest(line, headers);
    }

    public static ParsedResponse ReadResponse(ProtocolStreamReader reader)
    {
        var line = ResponseLine.Parse(reader.ReadLine());
        var headers = ReadHeaders(reader);
        return new ParsedResponse(line, headers);
    }

    private static Headers ReadHeaders(ProtocolStreamReader reader)
    {
        var headers = new Headers();
        while (true)
        {
            string line = reader.ReadLine();
            if (line.Length == 0)
            {
                return headers;
            }

            Headers.ParseLine(line, headers);
        }
    }
}
