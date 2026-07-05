using FileSync.Shared.Manifest;

namespace FileSync.Client.Sync;

// Vergelijkt de lokale bestandslijst (met hashes) tegen het MANIFEST van de server en
// bepaalt welke acties nodig zijn. Vergelijking gebeurt op inhoud (SHA-256), niet op
// naam of tijd (PROTOCOL.md §6): een pad met identieke hash aan beide kanten levert
// geen actie op — dat is de basis van "geen overdracht bij een ongewijzigd bestand".
public static class ManifestDiffer
{
    public static SyncPlan Diff(
        IReadOnlyDictionary<string, LocalFileState> local,
        IReadOnlyDictionary<string, ManifestEntry> remote,
        IReadOnlyDictionary<string, string> lastSyncedHashes)
    {
        var uploads = new List<UploadAction>();
        var downloads = new List<DownloadAction>();
        var serverDeletes = new List<DeleteAction>();
        var localDeletes = new List<LocalDeleteAction>();

        foreach ((string path, LocalFileState state) in local)
        {
            if (remote.TryGetValue(path, out ManifestEntry remoteEntry))
            {
                if (remoteEntry.Hash != state.Hash)
                {
                    uploads.Add(new UploadAction(path, state.AbsolutePath, state.Size, state.ModifiedUtc, state.Hash));
                }

                continue;
            }

            // Het pad staat niet (meer) op de server. Staat de huidige lokale hash nog
            // gelijk aan wat er bij de vorige cyclus stond, dan is er sinds toen niets
            // lokaal veranderd — de server-kant is dus door een ándere client verwijderd,
            // en spiegelen we dat hier lokaal. Wijkt de hash wél af (lokaal bewerkt of
            // opnieuw aangemaakt sinds de vorige cyclus), dan behandelen we het als nieuwe
            // inhoud en uploaden we het alsnog, in plaats van het per ongeluk te wissen.
            if (lastSyncedHashes.TryGetValue(path, out string? previousHash) && previousHash == state.Hash)
            {
                localDeletes.Add(new LocalDeleteAction(path, state.AbsolutePath));
            }
            else
            {
                uploads.Add(new UploadAction(path, state.AbsolutePath, state.Size, state.ModifiedUtc, state.Hash));
            }
        }

        foreach ((string path, ManifestEntry remoteEntry) in remote)
        {
            if (local.ContainsKey(path) || lastSyncedHashes.ContainsKey(path))
            {
                // Al lokaal aanwezig, of wij hebben dit pad zelf net lokaal verwijderd
                // (hieronder als DeleteAction naar de server gestuurd) — niet downloaden.
                continue;
            }

            downloads.Add(new DownloadAction(path, remoteEntry.Size, remoteEntry.Hash));
        }

        foreach (string path in lastSyncedHashes.Keys)
        {
            if (!local.ContainsKey(path) && remote.ContainsKey(path))
            {
                serverDeletes.Add(new DeleteAction(path));
            }
        }

        return new SyncPlan(uploads, downloads, serverDeletes, localDeletes);
    }
}
