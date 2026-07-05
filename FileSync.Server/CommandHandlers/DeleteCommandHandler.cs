using FileSync.Server.Connection;
using FileSync.Shared.Protocol;

namespace FileSync.Server.CommandHandlers;

public static class DeleteCommandHandler
{
    public static void Handle(string canonicalPath, CommandContext ctx)
    {
        if (!ctx.LockRegistry.TryAcquire(canonicalPath))
        {
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Locked);
            return;
        }

        try
        {
            string absolutePath = ctx.FileStore.ToAbsolutePath(canonicalPath);
            if (!File.Exists(absolutePath))
            {
                ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.NotFound);
                return;
            }

            File.Delete(absolutePath);
            ctx.HashCache.Invalidate(absolutePath);

            string partPath = ctx.FileStore.ToPartPath(canonicalPath);
            if (File.Exists(partPath))
            {
                File.Delete(partPath);
            }

            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Ok);
        }
        finally
        {
            ctx.LockRegistry.Release(canonicalPath);
        }
    }
}
