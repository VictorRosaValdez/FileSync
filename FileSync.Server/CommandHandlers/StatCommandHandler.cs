using FileSync.Server.Connection;
using FileSync.Shared.Protocol;

namespace FileSync.Server.CommandHandlers;

public static class StatCommandHandler
{
    public static async Task HandleAsync(string canonicalPath, CommandContext ctx)
    {
        string absolutePath = ctx.FileStore.ToAbsolutePath(canonicalPath);
        string partPath = ctx.FileStore.ToPartPath(canonicalPath);

        bool exists = File.Exists(absolutePath);
        long partSize = File.Exists(partPath) ? new FileInfo(partPath).Length : 0;

        ctx.Writer.WriteResponseLine(StatusCodes.Ok, StatusCodes.ReasonPhrase(StatusCodes.Ok));
        ctx.Writer.WriteHeader("Exists", exists ? "yes" : "no");
        ctx.Writer.WriteHeader("Part-Size", partSize.ToString());

        if (exists)
        {
            var info = new FileInfo(absolutePath);
            string hash = await ctx.HashCache.GetOrComputeHashAsync(absolutePath);
            ctx.Writer.WriteHeader("Size", info.Length.ToString());
            ctx.Writer.WriteHeader("Hash", hash);
        }

        ctx.Writer.EndHeaders();
        ctx.Writer.Flush();
    }
}
