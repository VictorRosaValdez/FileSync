namespace FileSync.Client.Net;

public sealed class SyncSessionException : Exception
{
    public SyncSessionException(string message) : base(message)
    {
    }
}
