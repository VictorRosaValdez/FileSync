using FileSync.Client.Sync;

namespace FileSync.Tests.Client;

public class LocalHashCacheTests
{
    private string _cacheFilePath = string.Empty;
    private static readonly DateTime Modified = new(2026, 7, 5, 14, 3, 22, DateTimeKind.Utc);

    [SetUp]
    public void SetUp()
    {
        _cacheFilePath = Path.Combine(Path.GetTempPath(), "filesync-cache-tests-" + Guid.NewGuid() + ".tsv");
    }

    [TearDown]
    public void TearDown()
    {
        if (File.Exists(_cacheFilePath))
        {
            File.Delete(_cacheFilePath);
        }
    }

    [Test]
    public void TryGetHash_UnchangedSizeAndMtime_ReusesCachedHash()
    {
        var cache = new LocalHashCache(_cacheFilePath);
        cache.SetHash("a.txt", 100, Modified, "hash-a");

        bool found = cache.TryGetHash("a.txt", 100, Modified, out string hash);

        Assert.That(found, Is.True);
        Assert.That(hash, Is.EqualTo("hash-a"));
    }

    [Test]
    public void TryGetHash_ChangedMtime_ReturnsFalse()
    {
        var cache = new LocalHashCache(_cacheFilePath);
        cache.SetHash("a.txt", 100, Modified, "hash-a");

        bool found = cache.TryGetHash("a.txt", 100, Modified.AddMinutes(1), out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void TryGetHash_ChangedSize_ReturnsFalse()
    {
        var cache = new LocalHashCache(_cacheFilePath);
        cache.SetHash("a.txt", 100, Modified, "hash-a");

        bool found = cache.TryGetHash("a.txt", 200, Modified, out _);

        Assert.That(found, Is.False);
    }

    [Test]
    public void SetHash_NeverMarkedSynced_DoesNotAppearInLastSyncedHashes()
    {
        // Regressietest voor een echte bug uit de lokale integratietest: het enkel
        // berekenen van een hash (voor rehash-vermijding) mag een gloednieuw, nog nooit
        // geüpload bestand niet laten lijken op "eerder gesynchroniseerd en nu extern
        // verwijderd" — anders wordt het bij de eerstvolgende cyclus per ongeluk lokaal
        // verwijderd in plaats van geüpload.
        var cache = new LocalHashCache(_cacheFilePath);
        cache.SetHash("nieuw.txt", 100, Modified, "hash-a");

        Assert.That(cache.LastSyncedHashes.ContainsKey("nieuw.txt"), Is.False);
    }

    [Test]
    public void MarkSynced_AddsPathToLastSyncedHashes()
    {
        var cache = new LocalHashCache(_cacheFilePath);
        cache.MarkSynced("a.txt", 100, Modified, "hash-a");

        Assert.That(cache.LastSyncedHashes["a.txt"], Is.EqualTo("hash-a"));
    }

    [Test]
    public void SaveThenLoad_RoundTripsHashAndSyncedState()
    {
        var original = new LocalHashCache(_cacheFilePath);
        original.SetHash("ongesynchroniseerd.txt", 50, Modified, "hash-nieuw");
        original.MarkSynced("gesynchroniseerd.txt", 100, Modified, "hash-a");
        original.Save();

        var reloaded = new LocalHashCache(_cacheFilePath);
        reloaded.Load();

        Assert.That(reloaded.TryGetHash("ongesynchroniseerd.txt", 50, Modified, out string hash1), Is.True);
        Assert.That(hash1, Is.EqualTo("hash-nieuw"));
        Assert.That(reloaded.LastSyncedHashes.ContainsKey("ongesynchroniseerd.txt"), Is.False);

        Assert.That(reloaded.TryGetHash("gesynchroniseerd.txt", 100, Modified, out string hash2), Is.True);
        Assert.That(hash2, Is.EqualTo("hash-a"));
        Assert.That(reloaded.LastSyncedHashes["gesynchroniseerd.txt"], Is.EqualTo("hash-a"));
    }

    [Test]
    public void Remove_KnownPath_RemovesFromBothHashAndSyncedState()
    {
        var cache = new LocalHashCache(_cacheFilePath);
        cache.MarkSynced("a.txt", 100, Modified, "hash-a");

        cache.Remove("a.txt");

        Assert.That(cache.TryGetHash("a.txt", 100, Modified, out _), Is.False);
        Assert.That(cache.LastSyncedHashes.ContainsKey("a.txt"), Is.False);
    }

    [Test]
    public void Load_MissingFile_LeavesCacheEmpty()
    {
        var cache = new LocalHashCache(_cacheFilePath);
        cache.Load();

        Assert.That(cache.LastSyncedHashes, Is.Empty);
    }
}
