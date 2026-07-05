using FileSync.Client.Cli;
using FileSync.Client.Sync;
using FileSync.Shared.Logging;

ClientOptions options;
try
{
    options = ClientOptions.Parse(args);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    Console.Error.WriteLine("Gebruik: FileSync.Client --host <adres> --folder <map> [--port <poort>] [--interval <seconden>] [--client-id <naam>]");
    return 1;
}

var logger = new ConsoleLogger();
var engine = new SyncEngine(options, logger);

logger.Info($"Start synchronisatie van '{options.Folder}' met {options.Host}:{options.Port} (elke {options.IntervalSeconds}s).");

while (true)
{
    try
    {
        await engine.RunOneCycleAsync();
    }
    catch (Exception ex)
    {
        // Eén mislukte cyclus (bv. verbinding verbroken tijdens een grote upload) mag de
        // client niet laten crashen: de volgende cyclus hervat vanzelf via STAT/Part-Size.
        logger.Warn($"Sync-cyclus mislukt: {ex.Message}");
    }

    await Task.Delay(TimeSpan.FromSeconds(options.IntervalSeconds));
}
