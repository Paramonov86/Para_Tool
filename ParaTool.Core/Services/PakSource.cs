using System.Collections.Concurrent;

namespace ParaTool.Core.Services;

/// <summary>
/// Where to read a pak's original content from. A pak ParaTool patched carries its additions
/// (appended stats, extra tables, cloned templates, loca); reading it live would present them as
/// the mod's own content — duplicate items, edited names shown as the base. Such a pak is read
/// from its pristine backup instead. Identity (ModInfo.PakPath, backup/restore) keeps the live path.
/// </summary>
public static class PakSource
{
    private static readonly ConcurrentDictionary<string, (long Size, DateTime Mtime, string Source)> Cache =
        new(StringComparer.OrdinalIgnoreCase);

    public static string Resolve(string pakPath)
    {
        FileInfo info;
        try
        {
            info = new FileInfo(pakPath);
            if (!info.Exists) return pakPath;
        }
        catch { return pakPath; }

        // Size + mtime key: a patch or restore rewrites the pak, so a stale answer can't survive it.
        if (Cache.TryGetValue(pakPath, out var hit)
            && hit.Size == info.Length && hit.Mtime == info.LastWriteTimeUtc)
            return hit.Source;

        var source = AmpBackupService.HasBackup(pakPath) && AmpBackupService.IsPatchedPak(pakPath)
            ? AmpBackupService.GetBackupPath(pakPath)
            : pakPath;
        Cache[pakPath] = (info.Length, info.LastWriteTimeUtc, source);
        return source;
    }
}
