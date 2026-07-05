using System.Net.Sockets;
using System.Text;
using FileSync.Shared.Manifest;
using FileSync.Shared.Protocol;
using FileSync.Shared.Time;

namespace FileSync.Client.Net;

// Eén SyncSession komt overeen met precies één TCP-verbinding en dus één sync-cyclus:
// HELLO → MANIFEST → (STAT/UPLOAD/DOWNLOAD/DELETE per bestand) → BYE. Het protocol is
// strikt request-response, dus alle methoden hier sturen één verzoek en lezen daarna
// precies één antwoord voordat de volgende aanroep mag plaatsvinden.
public sealed class SyncSession : IAsyncDisposable
{
    private const int BufferSize = 64 * 1024;

    private readonly TcpClient _client;
    private readonly NetworkStream _stream;
    private readonly ProtocolStreamReader _reader;
    private readonly ProtocolWriter _writer;

    private SyncSession(TcpClient client)
    {
        _client = client;
        _stream = client.GetStream();
        _reader = new ProtocolStreamReader(_stream);
        _writer = new ProtocolWriter(_stream);
    }

    public static async Task<SyncSession> ConnectAsync(string host, int port)
    {
        var client = new TcpClient();
        await client.ConnectAsync(host, port);
        return new SyncSession(client);
    }

    public void Hello(string clientId)
    {
        _writer.WriteRequestLine(Commands.Hello, null, Commands.SupportedVersion);
        _writer.WriteHeader("Client-Id", clientId);
        _writer.EndHeaders();
        _writer.Flush();

        ParsedResponse response = ProtocolMessageReader.ReadResponse(_reader);
        if (response.Line.StatusCode != StatusCodes.Ok)
        {
            throw new SyncSessionException($"HELLO afgewezen door server: {response.Line.StatusCode} {response.Line.Reason}.");
        }
    }

    public List<ManifestEntry> Manifest()
    {
        _writer.WriteRequestLine(Commands.Manifest, null, Commands.SupportedVersion);
        _writer.EndHeaders();
        _writer.Flush();

        ParsedResponse response = ProtocolMessageReader.ReadResponse(_reader);
        long contentLength = response.Headers.GetInt64("Content-Length");
        byte[] body = new byte[contentLength];
        _reader.ReadBody(body, 0, (int)contentLength);

        return ManifestSerializer.Deserialize(Encoding.UTF8.GetString(body));
    }

    public StatResult Stat(string canonicalPath)
    {
        _writer.WriteRequestLine(Commands.Stat, canonicalPath, Commands.SupportedVersion);
        _writer.EndHeaders();
        _writer.Flush();

        ParsedResponse response = ProtocolMessageReader.ReadResponse(_reader);
        bool exists = response.Headers.GetString("Exists") == "yes";
        long partSize = response.Headers.GetInt64("Part-Size");
        long size = exists ? response.Headers.GetInt64("Size") : 0;
        string? hash = exists ? response.Headers.GetString("Hash") : null;

        return new StatResult(exists, size, hash, partSize);
    }

    public async Task<UploadResult> UploadAsync(string canonicalPath, Stream localFileStream, long offset, long totalLength, string hash, DateTime modifiedUtc)
    {
        localFileStream.Seek(offset, SeekOrigin.Begin);
        long contentLength = totalLength - offset;

        _writer.WriteRequestLine(Commands.Upload, canonicalPath, Commands.SupportedVersion);
        _writer.WriteHeader("Offset", offset.ToString());
        _writer.WriteHeader("Content-Length", contentLength.ToString());
        _writer.WriteHeader("Total-Length", totalLength.ToString());
        _writer.WriteHeader("Hash", hash);
        _writer.WriteHeader("Modified", Iso8601.Format(modifiedUtc));
        _writer.EndHeaders();

        byte[] buffer = new byte[BufferSize];
        long remaining = contentLength;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(BufferSize, remaining);
            int read = await localFileStream.ReadAsync(buffer.AsMemory(0, toRead));
            if (read == 0)
            {
                throw new SyncSessionException("Lokaal bestand werd korter tijdens het versturen.");
            }

            _writer.WriteBody(buffer, 0, read);
            remaining -= read;
        }

        _writer.Flush();

        ParsedResponse response = ProtocolMessageReader.ReadResponse(_reader);
        return new UploadResult(response.Line.StatusCode);
    }

    public async Task DownloadAsync(string canonicalPath, long offset, Stream destination)
    {
        _writer.WriteRequestLine(Commands.Download, canonicalPath, Commands.SupportedVersion);
        if (offset > 0)
        {
            _writer.WriteHeader("Offset", offset.ToString());
        }

        _writer.EndHeaders();
        _writer.Flush();

        ParsedResponse response = ProtocolMessageReader.ReadResponse(_reader);
        if (response.Line.StatusCode != StatusCodes.Ok)
        {
            throw new SyncSessionException($"DOWNLOAD mislukt: {response.Line.StatusCode} {response.Line.Reason}.");
        }

        long contentLength = response.Headers.GetInt64("Content-Length");
        byte[] buffer = new byte[BufferSize];
        long remaining = contentLength;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(BufferSize, remaining);
            _reader.ReadBody(buffer, 0, toRead);
            await destination.WriteAsync(buffer.AsMemory(0, toRead));
            remaining -= toRead;
        }
    }

    public bool Delete(string canonicalPath)
    {
        _writer.WriteRequestLine(Commands.Delete, canonicalPath, Commands.SupportedVersion);
        _writer.EndHeaders();
        _writer.Flush();

        ParsedResponse response = ProtocolMessageReader.ReadResponse(_reader);
        return response.Line.StatusCode == StatusCodes.Ok;
    }

    public void Bye()
    {
        _writer.WriteRequestLine(Commands.Bye, null, Commands.SupportedVersion);
        _writer.EndHeaders();
        _writer.Flush();
        ProtocolMessageReader.ReadResponse(_reader);
    }

    public async ValueTask DisposeAsync()
    {
        await _stream.DisposeAsync();
        _client.Dispose();
    }
}
