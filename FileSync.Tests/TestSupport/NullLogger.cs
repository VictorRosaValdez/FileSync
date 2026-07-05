using FileSync.Shared.Logging;

namespace FileSync.Tests.TestSupport;

public sealed class NullLogger : IConsoleLogger
{
    public void Info(string message)
    {
    }

    public void Warn(string message)
    {
    }

    public void Error(string message)
    {
    }
}
