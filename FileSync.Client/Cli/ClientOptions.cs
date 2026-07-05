namespace FileSync.Client.Cli;

public sealed record ClientOptions(
    string Host,
    int Port,
    string Folder,
    int IntervalSeconds,
    string ClientId,
    string CacheFilePath,
    string? TrustedServerCertificatePath)
{
    public const int DefaultPort = 4711;
    public const int DefaultIntervalSeconds = 5;

    public static ClientOptions Parse(string[] args)
    {
        string? host = null;
        int port = DefaultPort;
        string? folder = null;
        int interval = DefaultIntervalSeconds;
        string? clientId = null;
        string? cacheFilePath = null;
        string? trustedServerCertificatePath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--host":
                    host = RequireValue(args, ref i);
                    break;
                case "--port":
                    port = int.Parse(RequireValue(args, ref i));
                    break;
                case "--folder":
                    folder = RequireValue(args, ref i);
                    break;
                case "--interval":
                    interval = int.Parse(RequireValue(args, ref i));
                    break;
                case "--client-id":
                    clientId = RequireValue(args, ref i);
                    break;
                case "--cache-file":
                    cacheFilePath = RequireValue(args, ref i);
                    break;
                case "--server-cert":
                    // Aanwezigheid van dit argument is meteen ook de TLS-schakelaar: geen
                    // apart --tls-argument nodig, zie SyncEngine/SyncSession.
                    trustedServerCertificatePath = RequireValue(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"Onbekend argument: '{args[i]}'.");
            }
        }

        if (host is null)
        {
            throw new ArgumentException("Verplicht argument ontbreekt: --host <adres>.");
        }

        if (folder is null)
        {
            throw new ArgumentException("Verplicht argument ontbreekt: --folder <map>.");
        }

        folder = Path.GetFullPath(folder);
        Directory.CreateDirectory(folder);
        clientId ??= Environment.MachineName;
        cacheFilePath ??= Path.Combine(folder, ".filesync-cache.tsv");

        return new ClientOptions(host, port, folder, interval, clientId, cacheFilePath, trustedServerCertificatePath);
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
        {
            throw new ArgumentException($"Argument '{args[i]}' verwacht een waarde.");
        }

        return args[++i];
    }
}
