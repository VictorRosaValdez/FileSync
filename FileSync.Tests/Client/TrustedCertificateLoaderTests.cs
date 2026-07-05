using FileSync.Client.Security;
using FileSync.Server.Security;

namespace FileSync.Tests.Client;

public class TrustedCertificateLoaderTests
{
    private string _tempDir = string.Empty;

    [SetUp]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "filesync-trust-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);
    }

    [TearDown]
    public void TearDown()
    {
        if (Directory.Exists(_tempDir))
        {
            Directory.Delete(_tempDir, recursive: true);
        }
    }

    [Test]
    public void Load_MissingFile_ThrowsFileNotFoundException()
    {
        string missingPath = Path.Combine(_tempDir, "does-not-exist.cer");
        Assert.Throws<FileNotFoundException>(() => TrustedCertificateLoader.Load(missingPath));
    }

    [Test]
    public void Load_ValidPublicCertFile_ReturnsCertificateWithoutPrivateKey()
    {
        string pfxPath = Path.Combine(_tempDir, "server.pfx");
        string publicCertPath = Path.Combine(_tempDir, "server.cer");
        ServerCertificateProvider.LoadOrCreate(pfxPath, publicCertPath);

        var loaded = TrustedCertificateLoader.Load(publicCertPath);

        Assert.That(loaded.HasPrivateKey, Is.False);
    }

    [Test]
    public void MatchesThumbprint_SamePresentedCertificate_ReturnsTrue()
    {
        string pfxPath = Path.Combine(_tempDir, "server.pfx");
        string publicCertPath = Path.Combine(_tempDir, "server.cer");
        var serverCertificate = ServerCertificateProvider.LoadOrCreate(pfxPath, publicCertPath);
        var trusted = TrustedCertificateLoader.Load(publicCertPath);

        bool matches = TrustedCertificateLoader.MatchesThumbprint(trusted, serverCertificate);

        Assert.That(matches, Is.True);
    }

    [Test]
    public void MatchesThumbprint_DifferentCertificate_ReturnsFalse()
    {
        string pfxPathA = Path.Combine(_tempDir, "a.pfx");
        string publicCertPathA = Path.Combine(_tempDir, "a.cer");
        string pfxPathB = Path.Combine(_tempDir, "b.pfx");
        string publicCertPathB = Path.Combine(_tempDir, "b.cer");

        var certificateA = ServerCertificateProvider.LoadOrCreate(pfxPathA, publicCertPathA);
        var certificateB = ServerCertificateProvider.LoadOrCreate(pfxPathB, publicCertPathB);
        var trustedA = TrustedCertificateLoader.Load(publicCertPathA);

        bool matches = TrustedCertificateLoader.MatchesThumbprint(trustedA, certificateB);

        Assert.That(matches, Is.False);
    }

    [Test]
    public void MatchesThumbprint_NullPresentedCertificate_ReturnsFalse()
    {
        string pfxPath = Path.Combine(_tempDir, "server.pfx");
        string publicCertPath = Path.Combine(_tempDir, "server.cer");
        ServerCertificateProvider.LoadOrCreate(pfxPath, publicCertPath);
        var trusted = TrustedCertificateLoader.Load(publicCertPath);

        bool matches = TrustedCertificateLoader.MatchesThumbprint(trusted, null);

        Assert.That(matches, Is.False);
    }
}
