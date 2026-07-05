using System.Text;
using FileSync.Server.Connection;
using FileSync.Shared.Manifest;
using FileSync.Shared.Protocol;

namespace FileSync.Server.CommandHandlers;

public static class ManifestCommandHandler
{
    public static async Task HandleAsync(CommandContext ctx)
    {
        var entries = new List<ManifestEntry>();

        foreach (string canonicalPath in ctx.FileStore.EnumerateCanonicalPaths())
        {
            string absolutePath = ctx.FileStore.ToAbsolutePath(canonicalPath);
            var info = new FileInfo(absolutePath);
            string hash = await ctx.HashCache.GetOrComputeHashAsync(absolutePath);
            entries.Add(new ManifestEntry(hash, info.Length, info.LastWriteTimeUtc, canonicalPath));
        }

        byte[] bodyBytes = Encoding.UTF8.GetBytes(ManifestSerializer.Serialize(entries));

        ctx.Writer.WriteResponseLine(StatusCodes.Ok, StatusCodes.ReasonPhrase(StatusCodes.Ok));
        ctx.Writer.WriteHeader("Content-Length", bodyBytes.Length.ToString());
        ctx.Writer.EndHeaders();
        ctx.Writer.WriteBody(bodyBytes, 0, bodyBytes.Length);
        ctx.Writer.Flush();
    }
}
