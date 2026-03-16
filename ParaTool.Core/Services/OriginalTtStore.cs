using System.Text.Json;
using ParaTool.Core.Models;

namespace ParaTool.Core.Services;

public static class OriginalTtStore
{
    private static string TtPath => Path.Combine(ProfileService.GetStorageDir(), "original_tt.txt");
    private static string MetaPath => Path.Combine(ProfileService.GetStorageDir(), "original_tt_meta.json");

    public static bool HasValidOriginal(string ampPakPath)
    {
        if (!File.Exists(TtPath) || !File.Exists(MetaPath))
            return false;

        try
        {
            var json = File.ReadAllText(MetaPath);
            var meta = JsonSerializer.Deserialize<OriginalTtMeta>(json);
            if (meta == null) return false;

            var fi = new FileInfo(ampPakPath);
            return meta.PakFileName == fi.Name && meta.PakFileSize == fi.Length;
        }
        catch
        {
            return false;
        }
    }

    public static void Store(string ampPakPath, string ttText)
    {
        var dir = ProfileService.GetStorageDir();
        Directory.CreateDirectory(dir);

        File.WriteAllText(TtPath, ttText);

        var fi = new FileInfo(ampPakPath);
        var meta = new OriginalTtMeta
        {
            PakFileName = fi.Name,
            PakFileSize = fi.Length
        };
        File.WriteAllText(MetaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
    }

    public static string? Load()
    {
        return File.Exists(TtPath) ? File.ReadAllText(TtPath) : null;
    }
}
