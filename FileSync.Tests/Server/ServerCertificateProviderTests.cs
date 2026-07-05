using FileSync.Server.Security;

namespace FileSync.Tests.Server;

public class ServerCertificateProviderTests
{
    private string _pfxPath = string.Empty;
    private string _publicCertPath = string.Empty;

    [SetUp]
    public void SetUp()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "filesync-cert-tests-" + Guid.NewGuid());
        Directory.CreateDirectory(tempDir);
        _pfxPath = Path.Combine(tempDir, "server-cert.pfx");
        _publicCertPath = Path.Combine(tempDir, "server-cert.cer");
    }

    [TearDown]
    public void TearDown()
    {
        string? directory = Path.GetDirectoryName(_pfxPath);
        if (directory is not null && Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void LoadOrCreate_NoExistingFile_GeneratesCertificateWithPrivateKey()
    {
        var certificate = ServerCertificateProvider.LoadOrCreate(_pfxPath, _publicCertPath);

        Assert.That(certificate.HasPrivateKey, Is.True);
        Assert.That(File.Exists(_pfxPath), Is.True);
        Assert.That(File.Exists(_publicCertPath), Is.True);
    }

    [Test]
    public void LoadOrCreate_CalledTwice_SecondCallReusesSameCertificate()
    {
        var first = ServerCertificateProvider.LoadOrCreate(_pfxPath, _publicCertPath);
        var second = ServerCertificateProvider.LoadOrCreate(_pfxPath, _publicCertPath);

        Assert.That(second.Thumbprint, Is.EqualTo(first.Thumbprint));
    }

    [Test]
    public void LoadOrCreate_GeneratedCertificate_IsValidForCurrentDate()
    {
        var certificate = ServerCertificateProvider.LoadOrCreate(_pfxPath, _publicCertPath);

        Assert.That(certificate.NotBefore, Is.LessThanOrEqualTo(DateTime.Now));
        Assert.That(certificate.NotAfter, Is.GreaterThan(DateTime.Now));
    }
}
