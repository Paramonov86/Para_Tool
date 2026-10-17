using System.Text.Json;

namespace ParaTool.Core.Patching;

/// <summary>
/// The RootTemplates the patcher has given edited items that had none of their own (AMP tiers share
/// their base item's template). An item picked up in game while one was in the pak is an instance
/// of it, and the game deletes an instance whose template is gone — so once issued, a template is
/// written on every patch, even after the edit is removed: the item then falls back to its original
/// values instead of vanishing from the save.
/// </summary>
public sealed class OverrideTemplateRegistry
{
    public sealed class Entry
    {
        public string StatId { get; set; } = "";
        public string StatType { get; set; } = "Armor";
        public string TemplateUuid { get; set; } = "";
        public string ParentTemplateUuid { get; set; } = "";
    }

    public List<Entry> Entries { get; set; } = [];

    /// <summary>The registry next to the user's artifacts.</summary>
    public static string DefaultPath =>
        Path.Combine(Services.ProfileService.GetStorageDir(), "override_templates.json");

    public Entry? Find(string statId) =>
        Entries.FirstOrDefault(e => e.StatId.Equals(statId, StringComparison.Ordinal));

    /// <summary>Records the template issued for <paramref name="statId"/>; the first one issued stays.</summary>
    public Entry Register(string statId, string statType, string templateUuid, string parentTemplateUuid)
    {
        if (Find(statId) is { } known) return known;
        var entry = new Entry
        {
            StatId = statId, StatType = statType,
            TemplateUuid = templateUuid, ParentTemplateUuid = parentTemplateUuid,
        };
        Entries.Add(entry);
        return entry;
    }

    public static OverrideTemplateRegistry Load(string path)
    {
        try
        {
            if (File.Exists(path))
                return JsonSerializer.Deserialize<OverrideTemplateRegistry>(File.ReadAllText(path)) ?? new();
        }
        catch (Exception ex) { Services.AppLogger.Warn($"Override template registry unreadable: {ex.Message}"); }
        return new();
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
    }
}
