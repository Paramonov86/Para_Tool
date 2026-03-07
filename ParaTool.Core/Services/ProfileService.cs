using System.Text.Json;
using ParaTool.Core.Models;

namespace ParaTool.Core.Services;

public static class ProfileService
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true
    };

    public static string GetStorageDir()
    {
        string baseDir;
        if (OperatingSystem.IsWindows())
            baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        else
            baseDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local", "share");

        return Path.Combine(baseDir, "ParaTool");
    }

    public static string GetLastSessionPath() =>
        Path.Combine(GetStorageDir(), "last-session.json");

    private static string GetProfilesDir() =>
        Path.Combine(GetStorageDir(), "profiles");

    public static string GetProfilePath(string name) =>
        Path.Combine(GetProfilesDir(), SanitizeName(name) + ".json");

    private static string SanitizeName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(name.Select(c => invalid.Contains(c) ? '_' : c));
    }

    // === Capture state from ModInfo/ItemEntry ===

    public static ProfileData CaptureState(IReadOnlyList<ModInfo> mods)
    {
        var data = new ProfileData();
        foreach (var mod in mods)
        {
            var selection = new ModSelection { ModName = mod.Name };
            foreach (var item in mod.Items)
            {
                selection.Items[item.StatId] = new ItemSettings
                {
                    Enabled = item.Enabled,
                    Pool = item.UserPool,
                    Rarity = item.UserRarity,
                    Themes = new List<string>(item.UserThemes)
                };
            }
            data.Mods[mod.UUID] = selection;
        }
        return data;
    }

    // === Apply profile to ModInfo/ItemEntry ===

    public static ApplyResult ApplyProfile(ProfileData profile, IReadOnlyList<ModInfo> mods)
    {
        int restored = 0;
        var missing = new List<MissingItem>();

        // Build lookup of existing items
        var existingItems = new Dictionary<(string uuid, string statId), ItemEntry>();
        foreach (var mod in mods)
            foreach (var item in mod.Items)
                existingItems[(mod.UUID, item.StatId)] = item;

        foreach (var (uuid, selection) in profile.Mods)
        {
            foreach (var (statId, settings) in selection.Items)
            {
                if (existingItems.TryGetValue((uuid, statId), out var item))
                {
                    item.Enabled = settings.Enabled;
                    item.UserPool = settings.Pool;
                    item.UserRarity = settings.Rarity;
                    item.UserThemes = new List<string>(settings.Themes);
                    restored++;
                }
                else
                {
                    missing.Add(new MissingItem
                    {
                        ModName = selection.ModName,
                        StatId = statId
                    });
                }
            }
        }

        return new ApplyResult { RestoredCount = restored, MissingItems = missing };
    }

    // === Last session ===

    public static void SaveLastSession(IReadOnlyList<ModInfo> mods)
    {
        var data = CaptureState(mods);
        var path = GetLastSessionPath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    public static ProfileData? LoadLastSession()
    {
        var path = GetLastSessionPath();
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProfileData>(json);
        }
        catch
        {
            return null;
        }
    }

    // === Named profiles ===

    public static void SaveProfile(string name, IReadOnlyList<ModInfo> mods)
    {
        var data = CaptureState(mods);
        var path = GetProfilePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(data, JsonOpts));
    }

    public static ProfileData? LoadProfile(string name)
    {
        var path = GetProfilePath(name);
        if (!File.Exists(path)) return null;
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<ProfileData>(json);
        }
        catch
        {
            return null;
        }
    }

    public static void DeleteProfile(string name)
    {
        var path = GetProfilePath(name);
        if (File.Exists(path))
            File.Delete(path);
    }

    public static List<string> ListProfiles()
    {
        var dir = GetProfilesDir();
        if (!Directory.Exists(dir)) return new();
        return Directory.GetFiles(dir, "*.json")
            .Select(f => Path.GetFileNameWithoutExtension(f))
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
