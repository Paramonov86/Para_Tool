namespace ParaTool.Core.Services;

public static class AmpBackupService
{
    private const string BackupSuffix = ".paratool_backup";

    /// <summary>
    /// Gets the backup path: parent of Mods folder + original pak filename + suffix.
    /// e.g. .../Data/REL_Full_Ancient_xxx.pak.paratool_backup
    /// </summary>
    public static string GetBackupPath(string ampPakPath)
    {
        var modsDir = Path.GetDirectoryName(ampPakPath)!;
        var parentDir = Path.GetDirectoryName(modsDir)!;
        var fileName = Path.GetFileName(ampPakPath);
        return Path.Combine(parentDir, fileName + BackupSuffix);
    }

    /// <summary>
    /// Creates a backup of the AMP pak before patching.
    /// If a backup already exists and matches the current file size, skip.
    /// If the AMP was updated (different size from backup), recreate backup.
    /// </summary>
    public static void EnsureBackup(string ampPakPath)
    {
        var backupPath = GetBackupPath(ampPakPath);

        if (File.Exists(backupPath))
        {
            var currentSize = new FileInfo(ampPakPath).Length;
            var backupSize = new FileInfo(backupPath).Length;

            if (currentSize != backupSize)
            {
                // AMP was updated — the current pak differs from backup.
                // But only recreate if the current pak is NOT already patched.
                // If it IS patched, the size difference is expected (patched > original).
                // We check for the marker inside the pak to determine this.
                if (!IsPatchedPak(ampPakPath))
                {
                    // Fresh AMP, different from backup → recreate
                    File.Copy(ampPakPath, backupPath, true);
                }
                // If patched + different size → backup is from original, keep it
            }
            // Same size → backup is current, nothing to do
        }
        else
        {
            // No backup exists — create one
            File.Copy(ampPakPath, backupPath, true);
        }
    }

    /// <summary>
    /// Returns true if a valid backup exists for the given AMP pak.
    /// </summary>
    public static bool HasBackup(string ampPakPath)
    {
        var backupPath = GetBackupPath(ampPakPath);
        return File.Exists(backupPath);
    }

    /// <summary>
    /// Returns true if the backup is stale (AMP was updated to a newer version).
    /// A stale backup means restore would downgrade AMP — we should recreate.
    /// </summary>
    public static bool IsBackupStale(string ampPakPath)
    {
        var backupPath = GetBackupPath(ampPakPath);
        if (!File.Exists(backupPath)) return false;

        // If current is NOT patched and differs from backup → AMP was updated
        if (!IsPatchedPak(ampPakPath))
        {
            var currentSize = new FileInfo(ampPakPath).Length;
            var backupSize = new FileInfo(backupPath).Length;
            return currentSize != backupSize;
        }

        return false;
    }

    /// <summary>
    /// Restores the AMP pak from backup, replacing the current (patched) version.
    /// Also clears the stored original TT so next scan reads fresh data.
    /// </summary>
    public static bool Restore(string ampPakPath)
    {
        var backupPath = GetBackupPath(ampPakPath);
        if (!File.Exists(backupPath)) return false;

        File.Copy(backupPath, ampPakPath, true);
        OriginalTtStore.Clear();
        return true;
    }

    /// <summary>
    /// Removes all REL_Full_Ancient_*.pak files from Mods folder except the specified one.
    /// Returns the number of removed files.
    /// </summary>
    public static int CleanOldAmpPaks(string modsFolder, string keepPakPath)
    {
        var ampPaks = Directory.GetFiles(modsFolder, "*.pak")
            .Where(p => Path.GetFileName(p)
                .StartsWith("REL_Full_Ancient_", StringComparison.OrdinalIgnoreCase))
            .Where(p => !string.Equals(p, keepPakPath, StringComparison.OrdinalIgnoreCase))
            .ToArray();

        int removed = 0;
        foreach (var pak in ampPaks)
        {
            try
            {
                File.Delete(pak);
                removed++;
            }
            catch { /* skip locked files */ }
        }
        return removed;
    }

    /// <summary>
    /// Checks if the pak contains a ParaTool marker file (patched by us).
    /// </summary>
    private static bool IsPatchedPak(string pakPath)
    {
        try
        {
            using var fs = File.OpenRead(pakPath);
            var header = PakReader.ReadHeader(fs);
            var entries = PakReader.ReadFileList(fs, header);
            return entries.Any(e =>
                e.Path.EndsWith("ZZZ_ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase) ||
                e.Path.EndsWith("ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
