using ParaTool.Core;
using ParaTool.Core.Services;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// A pak ParaTool patched carries its own additions; scanning it live would show them as the
/// mod's content. The scanner reads such a pak from its pristine backup.
/// </summary>
public class PakSourceTests
{
    /// <summary>Packs root/Mods/Sub.pak, so AmpBackupService puts its backup in root.</summary>
    private static string BuildPak(string root, string name, bool withMarker, string statText = "// stats\n")
    {
        var src = Path.Combine(root, "src-" + Guid.NewGuid().ToString("N"));
        var data = Path.Combine(src, "Public", "Sub", "Stats", "Generated", "Data");
        Directory.CreateDirectory(data);
        File.WriteAllText(Path.Combine(data, "Sub_Items.txt"), statText);
        if (withMarker)
            File.WriteAllText(Path.Combine(data, "ZZZ_ParaTool_Overrides.txt"), "// Patched by ParaTool\n");

        var modsDir = Path.Combine(root, "Mods");
        Directory.CreateDirectory(modsDir);
        var pak = Path.Combine(modsDir, name);
        PakWriter.CreatePak(src, pak);
        Directory.Delete(src, true);
        return pak;
    }

    private static void WithTempRoot(Action<string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "paratool-paksource-" + Guid.NewGuid().ToString("N"));
        try { body(root); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void PatchedPakWithBackup_ResolvesToBackup()
    {
        WithTempRoot(root =>
        {
            var pak = BuildPak(root, "Sub.pak", withMarker: true);
            File.WriteAllText(AmpBackupService.GetBackupPath(pak), "pristine");

            Assert.Equal(AmpBackupService.GetBackupPath(pak), PakSource.Resolve(pak));
        });
    }

    [Fact]
    public void UnpatchedPakWithBackup_ResolvesToLivePak()
    {
        // No marker: the pak is pristine or a newer release than the backup — the live one is right.
        WithTempRoot(root =>
        {
            var pak = BuildPak(root, "Sub.pak", withMarker: false);
            File.WriteAllText(AmpBackupService.GetBackupPath(pak), "older");

            Assert.Equal(pak, PakSource.Resolve(pak));
        });
    }

    [Fact]
    public void PatchedPakWithoutBackup_ResolvesToLivePak()
    {
        WithTempRoot(root =>
        {
            var pak = BuildPak(root, "Sub.pak", withMarker: true);

            Assert.Equal(pak, PakSource.Resolve(pak));
        });
    }

    [Fact]
    public void RestoredPak_IsNotServedFromStaleCache()
    {
        WithTempRoot(root =>
        {
            var pak = BuildPak(root, "Sub.pak", withMarker: true);
            File.WriteAllText(AmpBackupService.GetBackupPath(pak), "pristine");
            Assert.Equal(AmpBackupService.GetBackupPath(pak), PakSource.Resolve(pak));

            // Restore rewrites the pak without the marker; size/mtime change must drop the cache.
            var restored = BuildPak(root, "Restored.pak", withMarker: false, statText: "// restored, longer content\n");
            File.Copy(restored, pak, overwrite: true);
            File.SetLastWriteTimeUtc(pak, DateTime.UtcNow.AddMinutes(1));

            Assert.Equal(pak, PakSource.Resolve(pak));
        });
    }
}
