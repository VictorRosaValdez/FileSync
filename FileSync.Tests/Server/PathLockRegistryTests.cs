using System.Diagnostics;
using FileSync.Server.Locking;

namespace FileSync.Tests.Server;

public class PathLockRegistryTests
{
    [Test]
    public void TryAcquire_UnlockedPath_ReturnsTrue()
    {
        var registry = new PathLockRegistry();
        Assert.That(registry.TryAcquire("a.txt"), Is.True);
    }

    [Test]
    public void TryAcquire_AlreadyLockedPath_ReturnsFalseImmediately()
    {
        var registry = new PathLockRegistry();
        registry.TryAcquire("a.txt");

        var stopwatch = Stopwatch.StartNew();
        bool result = registry.TryAcquire("a.txt");
        stopwatch.Stop();

        Assert.That(result, Is.False);
        Assert.That(stopwatch.ElapsedMilliseconds, Is.LessThan(50));
    }

    [Test]
    public void Release_ThenTryAcquireAgain_Succeeds()
    {
        var registry = new PathLockRegistry();
        registry.TryAcquire("a.txt");
        registry.Release("a.txt");

        Assert.That(registry.TryAcquire("a.txt"), Is.True);
    }

    [Test]
    public void TryAcquire_DifferentPaths_BothSucceedConcurrently()
    {
        var registry = new PathLockRegistry();
        Assert.That(registry.TryAcquire("a.txt"), Is.True);
        Assert.That(registry.TryAcquire("b.txt"), Is.True);
    }
}
