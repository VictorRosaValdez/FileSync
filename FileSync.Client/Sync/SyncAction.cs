namespace FileSync.Client.Sync;

public sealed record UploadAction(string CanonicalPath, string AbsolutePath, long Size, DateTime ModifiedUtc, string Hash);

public sealed record DownloadAction(string CanonicalPath, long Size, string Hash);

// Lokaal bestand is weg → wordt naar de server gestuurd zodat de verwijdering daar
// (en dus bij andere clients) ook doorgevoerd wordt.
public sealed record DeleteAction(string CanonicalPath);

// Pad bestaat niet meer op de server, terwijl de lokale inhoud nog ongewijzigd is sinds
// de vorige sync — iemand anders heeft dit bestand verwijderd; wij spiegelen dat lokaal
// in plaats van het per ongeluk terug te uploaden.
public sealed record LocalDeleteAction(string CanonicalPath, string AbsolutePath);

public sealed record SyncPlan(
    List<UploadAction> Uploads,
    List<DownloadAction> Downloads,
    List<DeleteAction> ServerDeletes,
    List<LocalDeleteAction> LocalDeletes);
