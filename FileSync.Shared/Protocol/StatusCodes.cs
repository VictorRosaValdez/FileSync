namespace FileSync.Shared.Protocol;

public static class StatusCodes
{
    public const int Ok = 200;
    public const int Created = 201;
    public const int Identical = 204;
    public const int BadRequest = 400;
    public const int NotFound = 404;
    public const int HashMismatch = 409;
    public const int Locked = 423;
    public const int ServerError = 500;
    public const int VersionNotSupported = 505;

    public static string ReasonPhrase(int statusCode) => statusCode switch
    {
        Ok => "OK",
        Created => "Created",
        Identical => "Identical",
        BadRequest => "Bad Request",
        NotFound => "Not Found",
        HashMismatch => "Hash Mismatch",
        Locked => "Locked",
        ServerError => "Server Error",
        VersionNotSupported => "Version Not Supported",
        _ => "Unknown",
    };
}
