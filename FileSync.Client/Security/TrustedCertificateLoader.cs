using System.Security.Cryptography.X509Certificates;

namespace FileSync.Client.Security;

// Laadt het publieke, zelfondertekende servercertificaat dat buiten het protocol om
// (bv. via een kopie op een USB-stick of netwerkshare) vooraf naar de client is gebracht,
// en vergelijkt een tijdens de TLS-handshake aangeboden certificaat daar expliciet mee.
// Dit vervangt de normale CA-keten-validatie (die voor een zelfondertekend certificaat
// altijd zou falen) door "vertrouw precies dít ene, vooraf bekende certificaat" —
// vergelijkbaar met certificate pinning.
public static class TrustedCertificateLoader
{
    public static X509Certificate2 Load(string publicCertPath)
    {
        if (!File.Exists(publicCertPath))
        {
            throw new FileNotFoundException(
                $"Vertrouwd servercertificaat niet gevonden: '{publicCertPath}'. Kopieer het " +
                "publieke certificaat (bv. server-cert.cer) van de server naar deze locatie.",
                publicCertPath);
        }

        return new X509Certificate2(publicCertPath);
    }

    public static bool MatchesThumbprint(X509Certificate2 trustedCertificate, X509Certificate? presentedCertificate)
    {
        if (presentedCertificate is null)
        {
            return false;
        }

        return string.Equals(
            presentedCertificate.GetCertHashString(),
            trustedCertificate.GetCertHashString(),
            StringComparison.OrdinalIgnoreCase);
    }
}
