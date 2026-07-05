namespace FileSync.Server.Cli;

public sealed record ServerOptions(int Port, string StorageRoot)
{
    public const int DefaultPort = 4711;

    public static ServerOptions Parse(string[] args)
    {
        int port = DefaultPort;
        string? storageRoot = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--port":
                    port = int.Parse(RequireValue(args, ref i));
                    break;
                case "--storage":
                    storageRoot = RequireValue(args, ref i);
                    break;
                default:
                    throw new ArgumentException($"Onbekend argument: '{args[i]}'.");
            }
        }

        if (storageRoot is null)
        {
            throw new ArgumentException("Verplicht argument ontbreekt: --storage <map>.");
        }

        return new ServerOptions(port, Path.GetFullPath(storageRoot));
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
