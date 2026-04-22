using Avalonia.Media;

namespace ParaTool.App.Themes;

/// <summary>
/// Single source of truth for damage-type colour mapping.
/// Used by BbCodeTextBlock preview, DamageType tumbler chip, and anywhere
/// else we need to tint UI by elemental school.
/// </summary>
public static class DamageTypePalette
{
    public static readonly Dictionary<string, Color> Colors = new(StringComparer.OrdinalIgnoreCase)
    {
        // Physical — neutral silver for all three weapon-types
        ["Slashing"]    = Color.Parse("#C0C0C0"),
        ["Piercing"]    = Color.Parse("#C0C0C0"),
        ["Bludgeoning"] = Color.Parse("#C0C0C0"),

        // Elemental
        ["Fire"]        = Color.Parse("#E8602A"),
        ["Cold"]        = Color.Parse("#48A8D0"),
        ["Lightning"]   = Color.Parse("#60B0E8"),
        ["Thunder"]     = Color.Parse("#8868C8"),
        ["Acid"]        = Color.Parse("#50E828"),
        ["Poison"]      = Color.Parse("#A8B840"),

        // Divine / dark
        ["Necrotic"]    = Color.Parse("#8E4FB8"),
        ["Radiant"]     = Color.Parse("#E8C838"),
        ["Psychic"]     = Color.Parse("#C850C0"),
        ["Force"]       = Color.Parse("#E03030"),
    };

    public static readonly Dictionary<string, SolidColorBrush> Brushes =
        Colors.ToDictionary(kv => kv.Key, kv => new SolidColorBrush(kv.Value), StringComparer.OrdinalIgnoreCase);
}
