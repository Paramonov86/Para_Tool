using System.Text.Json;
using ParaTool.Core.Services;

namespace ParaTool.App.Services;

public sealed class UiSettings
{
    public string Theme { get; set; } = "BG3";
    public int FontSizeIndex { get; set; } = 1; // 0=S, 1=M, 2=L
    public string DefaultTab { get; set; } = "Patcher"; // "Patcher" or "Constructor"
    public string? Language { get; set; } // null = system default

    // Remembered sort state (per list)
    public string ConstructorSort { get; set; } = "Name";
    public bool ConstructorSortDesc { get; set; }
    public string PatcherSort { get; set; } = "Name";
    public string PatcherSecondarySort { get; set; } = "Name";
    public bool PatcherSortDesc { get; set; }

    /// <summary>Remembered order of the Constructor editor sections (by section key).</summary>
    public List<string>? EditorSectionOrder { get; set; }
}

public static class UiSettingsService
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private static string GetPath() =>
        Path.Combine(ProfileService.GetStorageDir(), "ui-settings.json");

    public static UiSettings Load()
    {
        var path = GetPath();
        if (!File.Exists(path)) return new();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<UiSettings>(json) ?? new();
        }
        catch { return new(); }
    }

    public static void Save(UiSettings settings)
    {
        try
        {
            var path = GetPath();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            File.WriteAllText(path, JsonSerializer.Serialize(settings, JsonOpts));
        }
        catch { /* best effort */ }
    }
}
