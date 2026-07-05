namespace FileSync.Client.Sync;

public readonly record struct LocalFileInfo(string CanonicalPath, string AbsolutePath, long Size, DateTime ModifiedUtc);
