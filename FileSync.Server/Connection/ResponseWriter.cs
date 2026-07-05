using FileSync.Shared.Protocol;

namespace FileSync.Server.Connection;

// Kleine helper voor de veelvoorkomende "alleen een statusregel, geen headers/body"-
// respons (400, 404, 423, 505, ...), zodat commandhandlers dat niet steeds herhalen.
public static class ResponseWriter
{
    public static void WriteStatusOnly(ProtocolWriter writer, int statusCode)
    {
        writer.WriteResponseLine(statusCode, StatusCodes.ReasonPhrase(statusCode));
        writer.EndHeaders();
        writer.Flush();
    }
}
