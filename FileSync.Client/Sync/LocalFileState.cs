namespace FileSync.Client.Sync;

public readonly record struct LocalFileState(long Size, DateTime ModifiedUtc, string Hash, string AbsolutePath);
