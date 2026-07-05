using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace FileSync.Server.Security;

// Regelt het zelfondertekende certificaat voor de optionele TLS-variant (PROTOCOL.md §8).
// Bij de allereerste start met --tls-port wordt een nieuw certificaat aangemaakt en naar
// schijf geschreven (privé .pfx + publiek .cer); latere starts hergebruiken hetzelfde
// bestand, zodat het publieke certificaat maar één keer naar clients gekopieerd hoeft te
// worden. Clients vertrouwen dit certificaat expliciet (zie FileSync.Client.Security),
// in plaats van de normale CA-keten te controleren, die voor een zelfondertekend
// certificaat toch altijd zou falen.
public static class ServerCertificateProvider
{
    private const string SubjectName = "CN=FileSync-Server";
    private static readonly TimeSpan Validity = TimeSpan.FromDays(365 * 5);

    public static X509Certificate2 LoadOrCreate(string pfxPath, string publicCertPath)
    {
        if (File.Exists(pfxPath))
        {
            return new X509Certificate2(pfxPath, (string?)null, X509KeyStorageFlags.Exportable);
        }

        X509Certificate2 certificate = CreateSelfSigned();

        File.WriteAllBytes(pfxPath, certificate.Export(X509ContentType.Pfx));
        File.WriteAllBytes(publicCertPath, certificate.Export(X509ContentType.Cert));

        return certificate;
    }

    private static X509Certificate2 CreateSelfSigned()
    {
        using RSA rsa = RSA.Create(2048);
        var request = new CertificateRequest(SubjectName, rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, critical: false));

        DateTimeOffset notBefore = DateTimeOffset.UtcNow.AddDays(-1);
        DateTimeOffset notAfter = notBefore.Add(Validity);

        using X509Certificate2 generated = request.CreateSelfSigned(notBefore, notAfter);

        // CreateSelfSigned levert een certificaat waarvan de private key nog gebonden is
        // aan de (using-gescoopte) RSA-instantie hierboven; via een pfx-export+import krijgt
        // het certificaat zijn eigen, zelfstandig bruikbare kopie van de sleutel.
        return new X509Certificate2(generated.Export(X509ContentType.Pfx), (string?)null, X509KeyStorageFlags.Exportable);
    }
}
