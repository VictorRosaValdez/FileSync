using FileSync.Server.Locking;
using FileSync.Server.Storage;
using FileSync.Shared.Logging;
using FileSync.Shared.Protocol;

namespace FileSync.Server.Connection;

// Bundelt alles wat een commandhandler nodig heeft, zodat handlers geen kennis
// hoeven te hebben van de verbinding of de rest van de server.
public sealed record CommandContext(
    ProtocolStreamReader Reader,
    ProtocolWriter Writer,
    ServerSessionState Session,
    FileStore FileStore,
    HashCache HashCache,
    PathLockRegistry LockRegistry,
    IConsoleLogger Logger);
