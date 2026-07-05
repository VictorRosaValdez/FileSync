namespace FileSync.Client.Net;

public sealed record StatResult(bool Exists, long Size, string? Hash, long PartSize);

public sealed record UploadResult(int StatusCode);
