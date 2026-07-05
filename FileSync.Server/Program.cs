using System.Net;
using System.Net.Sockets;
using FileSync.Server.Cli;
using FileSync.Server.Connection;
using FileSync.Server.Locking;
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
    Console.Error.WriteLine("Gebruik: FileSync.Server --storage <map> [--port <poort>]");
    return 1;
}

var logger = new ConsoleLogger();
var fileStore = new FileStore(options.StorageRoot);
var hashCache = new HashCache();
var lockRegistry = new PathLockRegistry();

var listener = new TcpListener(IPAddress.Any, options.Port);
listener.Start();
logger.Info($"Server luistert op poort {options.Port}, opslagmap '{options.StorageRoot}'.");

while (true)
{
    TcpClient client = await listener.AcceptTcpClientAsync();
    var handler = new ClientConnectionHandler(client, fileStore, hashCache, lockRegistry, logger);

    // Eigen Task per verbinding: de server bedient zo meerdere clients tegelijk, en een
    // onverwachte fout bij één verbinding (afgevangen binnen RunAsync) kan de accept-lus
    // van de server nooit onderuit halen.
    _ = Task.Run(handler.RunAsync);
}
