using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using FileSync.Server.Cli;
using FileSync.Server.Connection;
using FileSync.Server.Locking;
using FileSync.Server.Security;
using FileSync.Server.Storage;
using FileSync.Shared.Logging;

ServerOptions options;
try
{
    options = ServerOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Gebruik: FileSync.Server --storage <map> [--port <poort>] [--tls-port <poort>] [--cert <pad.pfx>] [--public-cert <pad.cer>]");
    return 1;
}

var logger = new ConsoleLogger();
var fileStore = new FileStore(options.StorageRoot);
var hashCache = new HashCache();
var lockRegistry = new PathLockRegistry();

var listenTasks = new List<Task> { RunListenerAsync(options.Port, serverCertificate: null) };

if (options.TlsPort is int tlsPort)
{
    // Optionele TLS-variant (PROTOCOL.md §8): apart poortnummer, protocol ongewijzigd.
    X509Certificate2 certificate = ServerCertificateProvider.LoadOrCreate(options.CertificatePath, options.PublicCertificatePath);
    logger.Info($"TLS ingeschakeld op poort {tlsPort}. Kopieer '{options.PublicCertificatePath}' naar elke client die --tls gebruikt.");
    listenTasks.Add(RunListenerAsync(tlsPort, certificate));
}

await Task.WhenAll(listenTasks);
return 0;

async Task RunListenerAsync(int port, X509Certificate2? serverCertificate)
{
    var listener = new TcpListener(IPAddress.Any, port);
    listener.Start();
    logger.Info($"Server luistert op poort {port}{(serverCertificate is not null ? " (TLS)" : string.Empty)}, opslagmap '{options.StorageRoot}'.");

    while (true)
    {
        TcpClient client = await listener.AcceptTcpClientAsync();
        var handler = new ClientConnectionHandler(client, fileStore, hashCache, lockRegistry, logger, serverCertificate);

        // Eigen Task per verbinding: de server bedient zo meerdere clients tegelijk, en een
        // onverwachte fout bij één verbinding (afgevangen binnen RunAsync) kan de accept-lus
        // van de server nooit onderuit halen.
        _ = Task.Run(handler.RunAsync);
    }
}
