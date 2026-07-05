using FileSync.Server.Connection;
using FileSync.Shared.Protocol;

namespace FileSync.Server.CommandHandlers;

public static class DownloadCommandHandler
{
    private const int BufferSize = 64 * 1024;

    public static async Task HandleAsync(ParsedRequest request, string canonicalPath, CommandContext ctx)
    {
        string absolutePath = ctx.FileStore.ToAbsolutePath(canonicalPath);
        if (!File.Exists(absolutePath))
        {
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.NotFound);
            return;
        }

        var info = new FileInfo(absolutePath);
        long offset = request.Headers.TryGetInt64("Offset", out long requestedOffset) ? requestedOffset : 0;

        if (offset < 0 || offset > info.Length)
        {
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
            return;
        }

        string hash = await ctx.HashCache.GetOrComputeHashAsync(absolutePath);
        long contentLength = info.Length - offset;

        ctx.Writer.WriteResponseLine(StatusCodes.Ok, StatusCodes.ReasonPhrase(StatusCodes.Ok));
        ctx.Writer.WriteHeader("Content-Length", contentLength.ToString());
        ctx.Writer.WriteHeader("Hash", hash);
        ctx.Writer.EndHeaders();

        await using FileStream fileStream = File.OpenRead(absolutePath);
        fileStream.Seek(offset, SeekOrigin.Begin);

        byte[] buffer = new byte[BufferSize];
        long remaining = contentLength;
        while (remaining > 0)
        {
            int toRead = (int)Math.Min(BufferSize, remaining);
            int read = await fileStream.ReadAsync(buffer.AsMemory(0, toRead));
            if (read == 0)
            {
                break;
            }

            ctx.Writer.WriteBody(buffer, 0, read);
            remaining -= read;
        }

        ctx.Writer.Flush();
    }
}
