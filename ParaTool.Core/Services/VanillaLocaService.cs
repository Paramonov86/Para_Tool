using System.Reflection;

namespace ParaTool.Core.Services;

/// <summary>
/// Provides vanilla item and passive localization from embedded TSV files.
/// Loaded once on first access, cached.
/// </summary>
public static class VanillaLocaService
{
    private static readonly string ItemsResource = "ParaTool.Core.Resources.VanillaLoca.vanilla_items_loca.tsv";
    private static readonly string PassivesResource = "ParaTool.Core.Resources.VanillaLoca.vanilla_passives_loca.tsv";
    private static readonly string StatusesResource = "ParaTool.Core.Resources.VanillaLoca.vanilla_statuses_loca.tsv";
    private static readonly string SpellsResource = "ParaTool.Core.Resources.VanillaLoca.vanilla_spells_loca.tsv";

    public sealed class ItemLoca
    {
        public string DisplayName_en { get; init; } = "";
        public string Description_en { get; init; } = "";
        public string DisplayName_ru { get; init; } = "";
        public string Description_ru { get; init; } = "";
        public string IconName { get; init; } = "";
    }

    public sealed class PassiveLoca
    {
        public string Type { get; init; } = ""; // PassiveData, StatusData, SpellData
        public string DisplayName_en { get; init; } = "";
        public string Description_en { get; init; } = "";
        public string DisplayName_ru { get; init; } = "";
        public string Description_ru { get; init; } = "";
    }

    private static Dictionary<string, ItemLoca>? _items;
    private static Dictionary<string, PassiveLoca>? _passives;

    /// <summary>Get vanilla item localization by StatId.</summary>
    public static ItemLoca? GetItem(string statId)
    {
        EnsureLoaded();
        return _items!.TryGetValue(statId, out var item) ? item : null;
    }

    /// <summary>Get vanilla passive/status/spell localization by name.</summary>
    public static PassiveLoca? GetPassive(string name)
    {
        EnsureLoaded();
        return _passives!.TryGetValue(name, out var p) ? p : null;
    }

    /// <summary>Get localized display name for a stat entry (item or passive).</summary>
    public static string? GetDisplayName(string name, string langCode)
    {
        var item = GetItem(name);
        if (item != null)
            return langCode == "ru" ? (string.IsNullOrEmpty(item.DisplayName_ru) ? item.DisplayName_en : item.DisplayName_ru) : item.DisplayName_en;

        var passive = GetPassive(name);
        if (passive != null)
            return langCode == "ru" ? (string.IsNullOrEmpty(passive.DisplayName_ru) ? passive.DisplayName_en : passive.DisplayName_ru) : passive.DisplayName_en;

        return null;
    }

    /// <summary>Get localized description for a stat entry.</summary>
    public static string? GetDescription(string name, string langCode)
    {
        var item = GetItem(name);
        if (item != null)
            return langCode == "ru" ? (string.IsNullOrEmpty(item.Description_ru) ? item.Description_en : item.Description_ru) : item.Description_en;

        var passive = GetPassive(name);
        if (passive != null)
            return langCode == "ru" ? (string.IsNullOrEmpty(passive.Description_ru) ? passive.Description_en : passive.Description_ru) : passive.Description_en;

        return null;
    }

    /// <summary>Get icon name for a vanilla item.</summary>
    public static string? GetIconName(string statId)
    {
        var item = GetItem(statId);
        return item != null && !string.IsNullOrEmpty(item.IconName) ? item.IconName : null;
    }

    private static void EnsureLoaded()
    {
        if (_items != null) return;

        _items = new(StringComparer.OrdinalIgnoreCase);
        _passives = new(StringComparer.OrdinalIgnoreCase);

        var assembly = typeof(VanillaLocaService).Assembly;

        // Load items
        using (var stream = assembly.GetManifestResourceStream(ItemsResource))
        {
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                reader.ReadLine(); // skip header
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split('\t');
                    if (parts.Length >= 6)
                    {
                        _items.TryAdd(parts[0], new ItemLoca
                        {
                            DisplayName_en = parts[1],
                            Description_en = parts[2],
                            DisplayName_ru = parts[3],
                            Description_ru = parts[4],
                            IconName = parts[5],
                        });
                    }
                }
            }
        }

        // Load passives, statuses, spells (same format)
        foreach (var resource in new[] { PassivesResource, StatusesResource, SpellsResource })
        {
            using var stream = assembly.GetManifestResourceStream(resource);
            if (stream == null) continue;
            using var reader = new StreamReader(stream);
            reader.ReadLine(); // skip header
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                var parts = line.Split('\t');
                if (parts.Length >= 6)
                {
                    _passives.TryAdd(parts[0], new PassiveLoca
                    {
                        Type = parts[1],
                        DisplayName_en = parts[2],
                        Description_en = parts[3],
                        DisplayName_ru = parts[4],
                        Description_ru = parts[5],
                    });
                }
            }
        }
    }
}
