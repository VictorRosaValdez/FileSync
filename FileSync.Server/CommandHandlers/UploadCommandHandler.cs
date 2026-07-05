using FileSync.Server.Connection;
using FileSync.Shared.Hashing;
using FileSync.Shared.Protocol;
using FileSync.Shared.Validation;

namespace FileSync.Server.CommandHandlers;

// Verwerkt UPLOAD: schrijft binnenkomende bytes streamend naar <pad>.part, en zet dat
// bestand pas na een geslaagde SHA-256-verificatie in één keer atomisch om naar het
// echte doelpad. Zo laat een netwerkstoring nooit een half bestand als "echt" bestand
// achter (PROTOCOL.md §5).
public static class UploadCommandHandler
{
    private const int BufferSize = 64 * 1024;

    public static async Task HandleAsync(ParsedRequest request, CommandContext ctx)
    {
        // Content-Length moet als eerste bekend zijn: zonder dat getal weten we niet
        // hoeveel bytes er nog aankomen en kan de verbinding niet meer betrouwbaar
        // gesynchroniseerd worden voor een volgend verzoek. Deze uitzondering laten we
        // daarom bewust doorlopen naar de verbindingslus, die de verbinding na een 400 sluit.
        long contentLength = request.Headers.GetInt64("Content-Length");

        bool pathValid = PathValidator.TryValidate(request.Line.Path, out string canonicalPath, out _);
        if (!pathValid)
        {
            await DrainBodyAsync(ctx.Reader, contentLength);
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
            return;
        }

        if (!ctx.LockRegistry.TryAcquire(canonicalPath))
        {
            await DrainBodyAsync(ctx.Reader, contentLength);
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Locked);
            return;
        }

        try
        {
            await HandleLockedAsync(request, canonicalPath, contentLength, ctx);
        }
        finally
        {
            ctx.LockRegistry.Release(canonicalPath);
        }
    }

    private static async Task HandleLockedAsync(ParsedRequest request, string canonicalPath, long contentLength, CommandContext ctx)
    {
        long offset;
        long totalLength;
        string expectedHash;

        try
        {
            offset = request.Headers.GetInt64("Offset");
            totalLength = request.Headers.GetInt64("Total-Length");
            expectedHash = request.Headers.GetString("Hash");
            request.Headers.GetString("Modified"); // verplicht aanwezig, verder niet gebruikt door de server
        }
        catch (MalformedMessageException)
        {
            await DrainBodyAsync(ctx.Reader, contentLength);
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
            return;
        }

        string absolutePath = ctx.FileStore.ToAbsolutePath(canonicalPath);
        string partPath = ctx.FileStore.ToPartPath(canonicalPath);

        // Dedup (PROTOCOL.md §6): als op dit pad al precies dezelfde inhoud staat, hoeft
        // er niets overgedragen te worden. We antwoorden 204 vóórdat de body écht nodig
        // is, en lezen 'm daarna alsnog blokkerend weg zodat de verbinding gesynchroniseerd
        // blijft voor het volgende commando — ook als een client de body toch al verstuurt.
        // In de normale sync-volgorde gaat aan elke UPLOAD altijd een STAT vooraf, dus onze
        // eigen client stuurt in de praktijk nooit een UPLOAD bij een hash-match; dit pad
        // bestaat voor spec-volledigheid en eventuele andere clients.
        if (offset == 0 && File.Exists(absolutePath))
        {
            string existingHash = await ctx.HashCache.GetOrComputeHashAsync(absolutePath);
            if (string.Equals(existingHash, expectedHash, StringComparison.OrdinalIgnoreCase))
            {
                ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Identical);
                await DrainBodyAsync(ctx.Reader, contentLength);
                return;
            }
        }

        ctx.FileStore.EnsureParentDirectoryExists(absolutePath);

        FileStream? partStream = TryOpenPartFileForWriting(partPath, offset);
        if (partStream is null)
        {
            await DrainBodyAsync(ctx.Reader, contentLength);
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.BadRequest);
            return;
        }

        await using (partStream)
        {
            await CopyBodyToPartFileAsync(ctx.Reader, partStream, contentLength);
        }

        bool isFinalChunk = offset + contentLength == totalLength;
        if (!isFinalChunk)
        {
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Ok);
            return;
        }

        string actualHash = await ComputeHashOfPartFileAsync(partPath);
        if (!string.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
        {
            File.Delete(partPath);
            ctx.Logger.Warn($"Hash-mismatch voor '{canonicalPath}': verwacht {expectedHash}, berekend {actualHash}.");
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.HashMismatch);
            return;
        }

        // Atomische rename: het doelbestand ontstaat in één enkele bestandssysteemoperatie
        // op hetzelfde volume, dus er is nooit een moment waarop een half of verminkt
        // bestand zichtbaar is onder het echte doelpad.
        File.Move(partPath, absolutePath, overwrite: true);
        ctx.HashCache.Set(absolutePath, totalLength, File.GetLastWriteTimeUtc(absolutePath), actualHash);
        ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Created);
    }

    // Bij Offset 0 beginnen we een nieuw .part-bestand (overschrijft een eventuele
    // verweesde .part van een eerdere mislukte poging). Bij Offset > 0 (hervatting) moet
    // de huidige lengte van het .part-bestand exact overeenkomen met de opgegeven Offset;
    // zo niet, dan behandelen we dit als een ongeldig verzoek in plaats van te gokken
    // over waar de nieuwe bytes moeten komen.
    private static FileStream? TryOpenPartFileForWriting(string partPath, long offset)
    {
        if (offset == 0)
        {
            return new FileStream(partPath, FileMode.Create, FileAccess.Write);
        }

        if (!File.Exists(partPath) || new FileInfo(partPath).Length != offset)
        {
            return null;
        }

        var stream = new FileStream(partPath, FileMode.Open, FileAccess.Write);
        stream.Seek(offset, SeekOrigin.Begin);
        return stream;
    }

    private static async Task CopyBodyToPartFileAsync(ProtocolStreamReader reader, FileStream partStream, long contentLength)
    {
        byte[] buffer = new byte[BufferSize];
        long remaining = contentLength;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(BufferSize, remaining);
            reader.ReadBody(buffer, 0, toRead);
            await partStream.WriteAsync(buffer.AsMemory(0, toRead));
            remaining -= toRead;
        }
    }

    private static async Task<string> ComputeHashOfPartFileAsync(string partPath)
    {
        await using FileStream stream = File.OpenRead(partPath);
        return await FileHasher.ComputeSha256HexAsync(stream);
    }

    private static async Task DrainBodyAsync(ProtocolStreamReader reader, long contentLength)
    {
        byte[] buffer = new byte[BufferSize];
        long remaining = contentLength;

        while (remaining > 0)
        {
            int toRead = (int)Math.Min(BufferSize, remaining);
            reader.ReadBody(buffer, 0, toRead);
            remaining -= toRead;
        }
    }
}
