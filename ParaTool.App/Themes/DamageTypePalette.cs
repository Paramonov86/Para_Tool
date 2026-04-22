using Avalonia.Media;

namespace ParaTool.App.Themes;

/// <summary>
/// Single source of truth for damage-type colour mapping.
/// Used by BbCodeTextBlock preview, DamageType tumbler chip, and BoostBlocksEditor
/// damage-typed chips (WeaponDamage, DealDamage, etc.).
/// Some colours have light-theme variants (darker) — call RefreshForTheme when theme changes.
/// </summary>
public static class DamageTypePalette
{
    private static readonly Dictionary<string, Color> DarkColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Slashing"]    = Color.Parse("#C0C0C0"),
        ["Piercing"]    = Color.Parse("#C0C0C0"),
        ["Bludgeoning"] = Color.Parse("#C0C0C0"),
        ["Fire"]        = Color.Parse("#E8602A"),
        ["Cold"]        = Color.Parse("#48A8D0"),
        ["Lightning"]   = Color.Parse("#60B0E8"),
        ["Thunder"]     = Color.Parse("#B0A8D8"),
        ["Acid"]        = Color.Parse("#50E828"),
        ["Poison"]      = Color.Parse("#7AA030"),
        ["Necrotic"]    = Color.Parse("#4A7A3A"),
        ["Radiant"]     = Color.Parse("#E8C838"),
        ["Psychic"]     = Color.Parse("#F0B0D8"),
        ["Force"]       = Color.Parse("#E8D8E8"),
    };

    private static readonly Dictionary<string, Color> LightOverrides = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Slashing"]    = Color.Parse("#808080"),
        ["Piercing"]    = Color.Parse("#808080"),
        ["Bludgeoning"] = Color.Parse("#808080"),
        ["Thunder"]     = Color.Parse("#7868B0"),
        ["Psychic"]     = Color.Parse("#C860A0"),
        ["Force"]       = Color.Parse("#9888A0"),
        ["Radiant"]     = Color.Parse("#B89820"),
        ["Lightning"]   = Color.Parse("#2878B0"),
    };

    public static readonly Dictionary<string, Color> Colors =
        new(DarkColors, StringComparer.OrdinalIgnoreCase);

    public static readonly Dictionary<string, SolidColorBrush> Brushes =
        DarkColors.ToDictionary(kv => kv.Key, kv => new SolidColorBrush(kv.Value),
            StringComparer.OrdinalIgnoreCase);

    public static void RefreshForTheme(bool isLight)
    {
        foreach (var (key, darkColor) in DarkColors)
        {
            var c = isLight && LightOverrides.TryGetValue(key, out var lc) ? lc : darkColor;
            Colors[key] = c;
            if (Brushes.TryGetValue(key, out var brush))
                brush.Color = c;
        }
    }

    public static Color? TryGet(string? damageType)
    {
        if (string.IsNullOrEmpty(damageType)) return null;
        if (damageType.Equals("None", StringComparison.OrdinalIgnoreCase)) return null;
        if (damageType.Equals("All", StringComparison.OrdinalIgnoreCase)) return null;
        return Colors.TryGetValue(damageType, out var c) ? c : null;
    }
}
