namespace FileSync.Shared.Manifest;

public readonly record struct ManifestEntry(string Hash, long Size, DateTime ModifiedUtc, string Path);
