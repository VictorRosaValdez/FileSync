using FileSync.Server.Connection;
using FileSync.Shared.Protocol;

namespace FileSync.Server.CommandHandlers;

public static class HelloCommandHandler
{
    public static void Handle(ParsedRequest request, CommandContext ctx)
    {
        string clientId = request.Headers.TryGet("Client-Id", out string id) ? id : "(onbekend)";

        if (request.Line.Version != Commands.SupportedVersion)
        {
            ctx.Logger.Warn($"HELLO met niet-ondersteunde versie '{request.Line.Version}' van client '{clientId}'.");
            ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.VersionNotSupported);
            return;
        }

        ctx.Session.HelloReceived = true;
        ctx.Session.ClientId = clientId;
        ctx.Logger.Info($"HELLO geaccepteerd van client '{clientId}'.");
        ResponseWriter.WriteStatusOnly(ctx.Writer, StatusCodes.Ok);
    }
}
