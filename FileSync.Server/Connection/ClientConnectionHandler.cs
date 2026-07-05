using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using FileSync.Server.CommandHandlers;
using FileSync.Server.Locking;
using FileSync.Server.Storage;
using FileSync.Shared.Logging;
using FileSync.Shared.Protocol;
using FileSync.Shared.Validation;

namespace FileSync.Server.Connection;

// Verwerkt precies één TCP-verbinding: leest achtereenvolgens verzoeken, dispatcht ze
// naar de juiste commandhandler, en blijft dat doen tot BYE of een verbindingsfout.
// Draait op een eigen Task per binnenkomende verbinding (zie Program.cs), zodat één
// slechte of langzame client de andere verbindingen niet blokkeert en de server nooit
// crasht door één kapotte client.
public sealed class ClientConnectionHandler
{
    private readonly TcpClient _client;
    private readonly FileStore _fileStore;
    private readonly HashCache _hashCache;
    private readonly PathLockRegistry _lockRegistry;
    private readonly IConsoleLogger _logger;
    private readonly X509Certificate2? _serverCertificate;

    public ClientConnectionHandler(
        TcpClient client,
        FileStore fileStore,
        HashCache hashCache,
        PathLockRegistry lockRegistry,
        IConsoleLogger logger,
        X509Certificate2? serverCertificate = null)
    {
        _client = client;
        _fileStore = fileStore;
        _hashCache = hashCache;
        _lockRegistry = lockRegistry;
        _logger = logger;
        _serverCertificate = serverCertificate;
    }

    public async Task RunAsync()
    {
        string remoteEndpoint = _client.Client.RemoteEndPoint?.ToString() ?? "onbekend";
        _logger.Info($"Nieuwe verbinding van {remoteEndpoint}{(_serverCertificate is not null ? " (TLS)" : string.Empty)}.");

        try
        {
            using TcpClient client = _client;
            await using NetworkStream networkStream = client.GetStream();
            await using Stream stream = await UpgradeToTlsIfNeededAsync(networkStream, remoteEndpoint);

            var ctx = new CommandContext(
                new ProtocolStreamReader(stream),
                new ProtocolWriter(stream),
                new ServerSessionState(),
                _fileStore,
                _hashCache,
                _lockRegistry,
                _logger);

            await RunRequestLoopAsync(ctx, remoteEndpoint);
        }
        catch (Exception ex) when (ex is IOException or SocketException or AuthenticationException)
        {
            _logger.Info($"Verbinding met {remoteEndpoint} verbroken: {ex.Message}");
        }
        catch (Exception ex)
        {
            _logger.Error($"Onverwachte fout bij verbinding met {remoteEndpoint}: {ex}");
        }
    }

    // Op de TLS-poort (zie Program.cs) vindt de handshake direct na de TCP-connect plaats,
    // vóór het eerste protocolcommando (PROTOCOL.md §8). Het protocol zelf blijft
    // ongewijzigd: alleen de onderliggende Stream verandert van een kale NetworkStream in
    // een versleutelde SslStream — ProtocolStreamReader/ProtocolWriter werken op beide
    // identiek, omdat ze tegen de abstracte Stream-klasse geprogrammeerd zijn.
    private async Task<Stream> UpgradeToTlsIfNeededAsync(NetworkStream networkStream, string remoteEndpoint)
    {
        if (_serverCertificate is null)
        {
            return networkStream;
        }

        var sslStream = new SslStream(networkStream, leaveInnerStreamOpen: false);
        await sslStream.AuthenticateAsServerAsync(_serverCertificate, clientCertificateRequired: false, checkCertificateRevocation: false);
        _logger.Info($"TLS-handshake geslaagd met {remoteEndpoint}.");
        return sslStream;
    }

    private async Task RunRequestLoopAsync(CommandContext ctx, string remoteEndpoint)
    {
        while (true)
        {
            ParsedRequest request;
            try
            {
                request = ProtocolMessageReader.ReadRequest(ctx.Reader);
            }
            catch (ProtocolException ex)
            {
                _logger.Warn($"Ongeldig verzoek van {remoteEndpoint}: {ex.Message}");
                ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
                return;
            }

            _logger.Info($"{remoteEndpoint} -> {request.Line.Command} {request.Line.Path}");

            if (!ctx.Session.HelloReceived && request.Line.Command != Commands.Hello)
            {
                ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
                continue;
            }

            bool shouldCloseConnection;
            try
            {
                shouldCloseConnection = await DispatchAsync(request, ctx);
            }
            catch (ProtocolException ex)
            {
                // Bv. een ontbrekende/ongeldige Content-Length bij UPLOAD: we weten dan
                // niet meer hoeveel bytes de client nog stuurt, dus kan de verbinding niet
                // betrouwbaar gesynchroniseerd blijven voor een volgend verzoek.
                _logger.Warn($"Kon verzoek van {remoteEndpoint} niet verwerken: {ex.Message}");
                ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
                return;
            }

            if (shouldCloseConnection)
            {
                return;
            }
        }
    }

    private static async Task<bool> DispatchAsync(ParsedRequest request, CommandContext ctx)
    {
        switch (request.Line.Command)
        {
            case Commands.Hello:
                HelloCommandHandler.Handle(request, ctx);
                return false;

            case Commands.Manifest:
                await ManifestCommandHandler.HandleAsync(ctx);
                return false;

            case Commands.Stat:
                if (TryValidatePath(request, ctx, out string statPath))
                {
                    await StatCommandHandler.HandleAsync(statPath, ctx);
                }
                return false;

            case Commands.Upload:
                await UploadCommandHandler.HandleAsync(request, ctx);
                return false;

            case Commands.Download:
                if (TryValidatePath(request, ctx, out string downloadPath))
                {
                    await DownloadCommandHandler.HandleAsync(request, downloadPath, ctx);
                }
                return false;

            case Commands.Delete:
                if (TryValidatePath(request, ctx, out string deletePath))
                {
                    DeleteCommandHandler.Handle(deletePath, ctx);
                }
                return false;

            case Commands.Bye:
                ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Ok);
                return true;

            default:
                ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
                return false;
        }
    }

    // STAT/DOWNLOAD/DELETE hebben geen request-body, dus bij een ongeldig pad kan de
    // server meteen 400 antwoorden zonder de verbinding uit sync te brengen.
    private static bool TryValidatePath(ParsedRequest request, CommandContext ctx, out string canonicalPath)
    {
        if (PathValidator.TryValidate(request.Line.Path, out canonicalPath, out _))
        {
            return true;
        }

        ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
        return false;
    }
}
