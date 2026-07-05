using System.Text;

namespace FileSync.Shared.Protocol;

public sealed class ProtocolWriter
{
    private readonly Stream _stream;

    public ProtocolWriter(Stream stream)
    {
        _stream = stream;
    }

    public void WriteRequestLine(string command, string? path, string version)
    {
        string line = path is null ? $"{command} {version}" : $"{command} {path} {version}";
        WriteLine(line);
    }

    public void WriteResponseLine(int statusCode, string reason)
    {
        WriteLine($"{Commands.SupportedVersion} {statusCode} {reason}");
    }

    public void WriteHeader(string name, string value) => WriteLine($"{name}: {value}");

    public void EndHeaders() => WriteLine(string.Empty);

    public void WriteBody(byte[] buffer, int offset, int count) => _stream.Write(buffer, offset, count);

    public void Flush() => _stream.Flush();

    // CRLF wordt hier altijd letterlijk geschreven (nooit Environment.NewLine): het
    // protocol schrijft dit vast voor, ongeacht of server/client op Windows of Linux draait.
    private void WriteLine(string line)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(line + "\r\n");
        _stream.Write(bytes, 0, bytes.Length);
    }
}
