using Avalonia;
using Avalonia.Media;

namespace ParaTool.App.Themes;

/// <summary>
/// Dynamic brush getters that respect current theme.
/// Use these instead of hardcoded Color.Parse("#...") in code-behind.
/// </summary>
public static class ThemeBrushes
{
    public static SolidColorBrush Get(string key)
    {
        if (Application.Current?.Resources.TryGetResource(key, null, out var res) == true && res is SolidColorBrush b)
            return b;
        return new SolidColorBrush(Colors.Magenta); // Debug: visible if resource missing
    }

    public static SolidColorBrush AppBg => Get("AppBgBrush");
    public static SolidColorBrush PanelBg => Get("PanelBgBrush");
    public static SolidColorBrush CardBg => Get("CardBgBrush");
    public static SolidColorBrush HoverBg => Get("HoverBgBrush");
    public static SolidColorBrush InputBg => Get("InputBgBrush");
    public static SolidColorBrush TextPrimary => Get("TextPrimaryBrush");
    public static SolidColorBrush TextSecondary => Get("TextSecondaryBrush");
    public static SolidColorBrush TextMuted => Get("TextMutedBrush");
    public static SolidColorBrush Accent => Get("AccentBrush");
    public static SolidColorBrush AccentLight => Get("AccentLightBrush");
    public static SolidColorBrush BorderSubtle => Get("BorderSubtleBrush");

    public static SolidColorBrush GetRarity(string rarity) => rarity switch
    {
        "Common" => Get("RarityCommonBrush"),
        "Uncommon" => Get("RarityUncommonBrush"),
        "Rare" => Get("RarityRareBrush"),
        "VeryRare" => Get("RarityVeryRareBrush"),
        "Legendary" => Get("RarityLegendaryBrush"),
        _ => Get("RarityCommonBrush"),
    };

    public static Color GetRarityColor(string rarity) => GetRarity(rarity).Color;
}
