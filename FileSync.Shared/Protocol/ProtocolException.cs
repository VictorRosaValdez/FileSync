namespace FileSync.Shared.Protocol;

// Basisklasse voor alles wat misgaat tijdens het ontleden van het SYNC/1.0-protocol.
// Commandhandlers vangen dit type af en vertalen het naar 400 Bad Request.
public class ProtocolException : Exception
{
    public ProtocolException(string message) : base(message)
    {
    }
}

public sealed class MalformedMessageException : ProtocolException
{
    public MalformedMessageException(string message) : base(message)
    {
    }
}

public sealed class LineTooLongException : ProtocolException
{
    public LineTooLongException(string message) : base(message)
    {
    }
}
