using FileSync.Client.Sync;
using FileSync.Shared.Manifest;

namespace FileSync.Tests.Client;

public class ManifestDifferTests
{
    private static readonly DateTime SampleModified = new(2026, 7, 5, 14, 3, 22, DateTimeKind.Utc);

    [Test]
    public void Diff_LocalHashMatchesServer_NoActionPlanned()
    {
        var local = new Dictionary<string, LocalFileState>
        {
            ["a.txt"] = new LocalFileState(10, SampleModified, "aaa", "C:/sync/a.txt"),
        };
        var remote = new Dictionary<string, ManifestEntry>
        {
            ["a.txt"] = new ManifestEntry("aaa", 10, SampleModified, "a.txt"),
        };
        var lastSynced = new Dictionary<string, string> { ["a.txt"] = "aaa" };

        SyncPlan plan = ManifestDiffer.Diff(local, remote, lastSynced);

        Assert.That(plan.Uploads, Is.Empty);
        Assert.That(plan.Downloads, Is.Empty);
        Assert.That(plan.ServerDeletes, Is.Empty);
        Assert.That(plan.LocalDeletes, Is.Empty);
    }

    [Test]
    public void Diff_LocalHashDiffersFromServer_PlansUpload()
    {
        var local = new Dictionary<string, LocalFileState>
        {
            ["a.txt"] = new LocalFileState(10, SampleModified, "nieuwe-hash", "C:/sync/a.txt"),
        };
        var remote = new Dictionary<string, ManifestEntry>
        {
            ["a.txt"] = new ManifestEntry("oude-hash", 10, SampleModified, "a.txt"),
        };
        var lastSynced = new Dictionary<string, string> { ["a.txt"] = "oude-hash" };

        SyncPlan plan = ManifestDiffer.Diff(local, remote, lastSynced);

        Assert.That(plan.Uploads, Has.Count.EqualTo(1));
        Assert.That(plan.Uploads[0].CanonicalPath, Is.EqualTo("a.txt"));
    }

    [Test]
    public void Diff_LocalOnlyFile_PlansUpload()
    {
        var local = new Dictionary<string, LocalFileState>
        {
            ["nieuw.txt"] = new LocalFileState(5, SampleModified, "hash", "C:/sync/nieuw.txt"),
        };
        var remote = new Dictionary<string, ManifestEntry>();

        SyncPlan plan = ManifestDiffer.Diff(local, remote, new Dictionary<string, string>());

        Assert.That(plan.Uploads, Has.Count.EqualTo(1));
    }

    [Test]
    public void Diff_ServerOnlyPath_PlansDownload()
    {
        var local = new Dictionary<string, LocalFileState>();
        var remote = new Dictionary<string, ManifestEntry>
        {
            ["nieuw-op-server.txt"] = new ManifestEntry("hash", 5, SampleModified, "nieuw-op-server.txt"),
        };

        SyncPlan plan = ManifestDiffer.Diff(local, remote, new Dictionary<string, string>());

        Assert.That(plan.Downloads, Has.Count.EqualTo(1));
        Assert.That(plan.Downloads[0].CanonicalPath, Is.EqualTo("nieuw-op-server.txt"));
    }

    [Test]
    public void Diff_LocallyDeletedSincePreviousSync_PlansServerDelete()
    {
        var local = new Dictionary<string, LocalFileState>();
        var remote = new Dictionary<string, ManifestEntry>
        {
            ["verwijderd.txt"] = new ManifestEntry("hash", 5, SampleModified, "verwijderd.txt"),
        };
        var lastSynced = new Dictionary<string, string> { ["verwijderd.txt"] = "hash" };

        SyncPlan plan = ManifestDiffer.Diff(local, remote, lastSynced);

        Assert.That(plan.ServerDeletes, Has.Count.EqualTo(1));
        Assert.That(plan.Downloads, Is.Empty);
    }

    [Test]
    public void Diff_DeletedOnBothSides_NoActionPlanned()
    {
        var local = new Dictionary<string, LocalFileState>();
        var remote = new Dictionary<string, ManifestEntry>();
        var lastSynced = new Dictionary<string, string> { ["al-lang-weg.txt"] = "hash" };

        SyncPlan plan = ManifestDiffer.Diff(local, remote, lastSynced);

        Assert.That(plan.ServerDeletes, Is.Empty);
        Assert.That(plan.Downloads, Is.Empty);
        Assert.That(plan.Uploads, Is.Empty);
        Assert.That(plan.LocalDeletes, Is.Empty);
    }

    [Test]
    public void Diff_RemotelyDeletedByAnotherClient_UnchangedLocalCopy_PlansLocalDelete()
    {
        // Regressietest voor een echte bug uit de lokale integratietest: client B heeft
        // nog een ongewijzigde lokale kopie van een bestand dat client A inmiddels op de
        // server heeft laten verwijderen. B mag dit NIET terug-uploaden (dat zou de
        // verwijdering ongedaan maken); B moet de lokale kopie juist ook verwijderen.
        var local = new Dictionary<string, LocalFileState>
        {
            ["gedeeld.txt"] = new LocalFileState(5, SampleModified, "ongewijzigde-hash", "C:/sync/gedeeld.txt"),
        };
        var remote = new Dictionary<string, ManifestEntry>(); // op de server al verwijderd door client A
        var lastSynced = new Dictionary<string, string> { ["gedeeld.txt"] = "ongewijzigde-hash" };

        SyncPlan plan = ManifestDiffer.Diff(local, remote, lastSynced);

        Assert.That(plan.LocalDeletes, Has.Count.EqualTo(1));
        Assert.That(plan.LocalDeletes[0].CanonicalPath, Is.EqualTo("gedeeld.txt"));
        Assert.That(plan.Uploads, Is.Empty);
    }

    [Test]
    public void Diff_LocallyModifiedAfterRemoteDeletion_PlansUploadInsteadOfLocalDelete()
    {
        // Als de gebruiker het bestand lokaal heeft bewerkt (nieuwe hash) ná de vorige
        // sync, mag een verwijdering elders die lokale wijziging niet zomaar wegvegen —
        // dan uploaden we de nieuwe inhoud in plaats van lokaal te verwijderen.
        var local = new Dictionary<string, LocalFileState>
        {
            ["gedeeld.txt"] = new LocalFileState(8, SampleModified, "nieuwe-lokale-hash", "C:/sync/gedeeld.txt"),
        };
        var remote = new Dictionary<string, ManifestEntry>();
        var lastSynced = new Dictionary<string, string> { ["gedeeld.txt"] = "oude-hash" };

        SyncPlan plan = ManifestDiffer.Diff(local, remote, lastSynced);

        Assert.That(plan.Uploads, Has.Count.EqualTo(1));
        Assert.That(plan.LocalDeletes, Is.Empty);
    }
}
