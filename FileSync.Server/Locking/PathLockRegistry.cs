using System.Collections.Concurrent;

namespace FileSync.Server.Locking;

// Per-pad schrijfvergrendeling zonder wachten: TryAcquire geeft direct false terug als
// het pad al in gebruik is (→ 423 Locked), in plaats van te blokkeren tot het vrijkomt.
// Lees-commando's (STAT/DOWNLOAD/MANIFEST) gebruiken dit register niet en lopen dus
// altijd door, ongeacht een lopende schrijfactie op hetzelfde pad.
public sealed class PathLockRegistry
{
    private readonly ConcurrentDictionary<string, byte> _lockedPaths = new();

    public bool TryAcquire(string canonicalPath) => _lockedPaths.TryAdd(canonicalPath, 0);

    public void Release(string canonicalPath) => _lockedPaths.TryRemove(canonicalPath, out _);
}
