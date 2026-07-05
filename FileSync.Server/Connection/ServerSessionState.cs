namespace FileSync.Server.Connection;

// Toestand van één verbinding: elke TcpClient krijgt zijn eigen instantie, dus dit
// hoeft niet thread-safe te zijn (verzoeken op één verbinding komen nooit gelijktijdig,
// het protocol is strikt request-response).
public sealed class ServerSessionState
{
    public bool HelloReceived { get; set; }

    public string? ClientId { get; set; }
}
