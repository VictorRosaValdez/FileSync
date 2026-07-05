using System.Text;

namespace FileSync.Client.Sync;

// Doorzoekt de syncmap recursief en zet Windows/Linux-paden om naar het canonieke,
// protocolconforme formaat: forward slashes en NFC-genormaliseerde Unicode, zodat
// hetzelfde bestand op beide besturingssystemen tot exact hetzelfde pad leidt.
public static class FolderScanner
{
    public static List<LocalFileInfo> Scan(string rootFolder, string cacheFileAbsolutePath)
    {
        var results = new List<LocalFileInfo>();

        foreach (string absolutePath in Directory.EnumerateFiles(rootFolder, "*", SearchOption.AllDirectories))
        {
            if (absolutePath.EndsWith(".part", StringComparison.Ordinal)
                || string.Equals(absolutePath, cacheFileAbsolutePath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            string relative = Path.GetRelativePath(rootFolder, absolutePath)
                .Replace(Path.DirectorySeparatorChar, '/')
                .Normalize(NormalizationForm.FormC);

            var info = new FileInfo(absolutePath);
            results.Add(new LocalFileInfo(relative, absolutePath, info.Length, info.LastWriteTimeUtc));
        }

        return results;
    }
}
