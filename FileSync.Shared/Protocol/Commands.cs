namespace FileSync.Shared.Protocol;

public static class Commands
{
    public const string Hello = "HELLO";
    public const string Manifest = "MANIFEST";
    public const string Stat = "STAT";
    public const string Upload = "UPLOAD";
    public const string Download = "DOWNLOAD";
    public const string Delete = "DELETE";
    public const string Bye = "BYE";

    public const string SupportedVersion = "SYNC/1.0";
}
