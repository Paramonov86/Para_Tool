using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;

namespace ParaTool.App.Themes;

/// <summary>
/// Manages color themes. Updates SolidColorBrush resources at runtime.
/// </summary>
public static class ThemeManager
{
    // Default rarity colors (dark themes)
    private static readonly Color DefCommon = Color.Parse("#8A8494");
    private static readonly Color DefUncommon = Color.Parse("#2ECC71");
    private static readonly Color DefRare = Color.Parse("#3498DB");
    private static readonly Color DefVeryRare = Color.Parse("#9B59B6");
    private static readonly Color DefLegendary = Color.Parse("#C8A96E");

    // Light-friendly rarity colors (darker/more saturated for contrast)
    private static readonly Color LightCommon = Color.Parse("#6A6474");
    private static readonly Color LightUncommon = Color.Parse("#1B9E52");
    private static readonly Color LightRare = Color.Parse("#2070B0");
    private static readonly Color LightVeryRare = Color.Parse("#7A3E9D");
    private static readonly Color LightLegendary = Color.Parse("#A07830");

    public record ThemeDef(
        string Name,
        Color AppBg, Color PanelBg, Color CardBg, Color HoverBg, Color InputBg,
        Color TextPrimary, Color TextSecondary, Color TextMuted, Color TextDisabled,
        Color Accent, Color AccentLight, Color Gold,
        Color Success, Color Warning, Color Error, Color Info,
        Color BorderSubtle,
        bool IsLight = false)
    {
        public Color RarityCommon => IsLight ? LightCommon : DefCommon;
        public Color RarityUncommon => IsLight ? LightUncommon : DefUncommon;
        public Color RarityRare => IsLight ? LightRare : DefRare;
        public Color RarityVeryRare => IsLight ? LightVeryRare : DefVeryRare;
        public Color RarityLegendary => IsLight ? LightLegendary : DefLegendary;
    }

    public static readonly ThemeDef Paramonov = new("Paramonov",
        AppBg: Color.Parse("#1A1820"), PanelBg: Color.Parse("#2F2C3A"), CardBg: Color.Parse("#3D3A4D"),
        HoverBg: Color.Parse("#4A3F6B"), InputBg: Color.Parse("#252330"),
        TextPrimary: Color.Parse("#E0DDE6"), TextSecondary: Color.Parse("#C8B8DB"),
        TextMuted: Color.Parse("#8A8494"), TextDisabled: Color.Parse("#5A5465"),
        Accent: Color.Parse("#6C5CE7"), AccentLight: Color.Parse("#8A7FC1"), Gold: Color.Parse("#C8A96E"),
        Success: Color.Parse("#2ECC71"), Warning: Color.Parse("#F39C12"),
        Error: Color.Parse("#E74C5B"), Info: Color.Parse("#3498DB"),
        BorderSubtle: Color.Parse("#3D3A4D"));

    public static readonly ThemeDef Light = new("Light",
        AppBg: Color.Parse("#F0EFF4"), PanelBg: Color.Parse("#FFFFFF"), CardBg: Color.Parse("#F5F4F8"),
        HoverBg: Color.Parse("#E8E6F0"), InputBg: Color.Parse("#EEEDF2"),
        TextPrimary: Color.Parse("#1A1820"), TextSecondary: Color.Parse("#4A4458"),
        TextMuted: Color.Parse("#8A8494"), TextDisabled: Color.Parse("#B8B4C4"),
        Accent: Color.Parse("#6C5CE7"), AccentLight: Color.Parse("#5A4BD6"), Gold: Color.Parse("#B8962E"),
        Success: Color.Parse("#27AE60"), Warning: Color.Parse("#E67E22"),
        Error: Color.Parse("#E74C3C"), Info: Color.Parse("#2980B9"),
        BorderSubtle: Color.Parse("#D8D6E0"),
        IsLight: true);

    public static readonly ThemeDef Dota2 = new("Dota 2",
        AppBg: Color.Parse("#0C0A0E"), PanelBg: Color.Parse("#181620"), CardBg: Color.Parse("#222030"),
        HoverBg: Color.Parse("#2E2A3A"), InputBg: Color.Parse("#100E16"),
        TextPrimary: Color.Parse("#D8D0C4"), TextSecondary: Color.Parse("#A09888"),
        TextMuted: Color.Parse("#6A6258"), TextDisabled: Color.Parse("#3E3830"),
        Accent: Color.Parse("#C9302C"), AccentLight: Color.Parse("#E04438"), Gold: Color.Parse("#E2B53E"),
        Success: Color.Parse("#5EC45E"), Warning: Color.Parse("#E2B53E"),
        Error: Color.Parse("#C9302C"), Info: Color.Parse("#6CB4DC"),
        BorderSubtle: Color.Parse("#2E2A3A"));

    // ── Cyberpunk — neon yellow / cyan on deep dark ──────────
    public static readonly ThemeDef Cyberpunk = new("Cyberpunk",
        AppBg: Color.Parse("#0A0A12"), PanelBg: Color.Parse("#12121E"), CardBg: Color.Parse("#1A1A2E"),
        HoverBg: Color.Parse("#252540"), InputBg: Color.Parse("#0E0E1A"),
        TextPrimary: Color.Parse("#E0F0FF"), TextSecondary: Color.Parse("#A0C8E8"),
        TextMuted: Color.Parse("#5A7088"), TextDisabled: Color.Parse("#3A4858"),
        Accent: Color.Parse("#F7D731"), AccentLight: Color.Parse("#FFE95C"), Gold: Color.Parse("#F7D731"),
        Success: Color.Parse("#00FF9C"), Warning: Color.Parse("#FF9C00"),
        Error: Color.Parse("#FF3860"), Info: Color.Parse("#00D4FF"),
        BorderSubtle: Color.Parse("#2A2A44"));

    // ── WoW — Alliance blue & gold with a touch of Horde red ──
    public static readonly ThemeDef Wow = new("WoW",
        AppBg: Color.Parse("#0C1220"), PanelBg: Color.Parse("#141E30"), CardBg: Color.Parse("#1C2840"),
        HoverBg: Color.Parse("#263450"), InputBg: Color.Parse("#0A1018"),
        TextPrimary: Color.Parse("#D8E4F0"), TextSecondary: Color.Parse("#A0B8D0"),
        TextMuted: Color.Parse("#5A7898"), TextDisabled: Color.Parse("#3A4E68"),
        Accent: Color.Parse("#1E70BF"), AccentLight: Color.Parse("#3A90E0"), Gold: Color.Parse("#D4A520"),
        Success: Color.Parse("#4CAF50"), Warning: Color.Parse("#D4A520"),
        Error: Color.Parse("#8C1616"), Info: Color.Parse("#4A9AD4"),
        BorderSubtle: Color.Parse("#263450"));

    // ── Nord — cool arctic blue-gray ──────────────────────────
    public static readonly ThemeDef Nord = new("Nord",
        AppBg: Color.Parse("#2E3440"), PanelBg: Color.Parse("#3B4252"), CardBg: Color.Parse("#434C5E"),
        HoverBg: Color.Parse("#4C566A"), InputBg: Color.Parse("#2E3440"),
        TextPrimary: Color.Parse("#ECEFF4"), TextSecondary: Color.Parse("#D8DEE9"),
        TextMuted: Color.Parse("#7B88A0"), TextDisabled: Color.Parse("#5A6478"),
        Accent: Color.Parse("#88C0D0"), AccentLight: Color.Parse("#8FBCBB"), Gold: Color.Parse("#EBCB8B"),
        Success: Color.Parse("#A3BE8C"), Warning: Color.Parse("#EBCB8B"),
        Error: Color.Parse("#BF616A"), Info: Color.Parse("#81A1C1"),
        BorderSubtle: Color.Parse("#4C566A"));

    // ── Synthwave — hot pink / magenta neon on deep blue ──────
    public static readonly ThemeDef Synthwave = new("Synthwave",
        AppBg: Color.Parse("#0B0620"), PanelBg: Color.Parse("#150E30"), CardBg: Color.Parse("#1E1540"),
        HoverBg: Color.Parse("#2A1E55"), InputBg: Color.Parse("#0E0928"),
        TextPrimary: Color.Parse("#F0E0FF"), TextSecondary: Color.Parse("#C8A8E8"),
        TextMuted: Color.Parse("#7A5C99"), TextDisabled: Color.Parse("#4A3568"),
        Accent: Color.Parse("#FF2E97"), AccentLight: Color.Parse("#FF6EB4"), Gold: Color.Parse("#FFD700"),
        Success: Color.Parse("#72F1B8"), Warning: Color.Parse("#FEDE5D"),
        Error: Color.Parse("#FE4450"), Info: Color.Parse("#36F9F6"),
        BorderSubtle: Color.Parse("#2A1E55"));

    // ── BG3 — warm amber/gold, matches the game UI ─────────────
    public static readonly ThemeDef Bg3 = new("BG3",
        AppBg: Color.Parse("#1A1612"), PanelBg: Color.Parse("#221E18"), CardBg: Color.Parse("#2C2820"),
        HoverBg: Color.Parse("#3A3228"), InputBg: Color.Parse("#161210"),
        TextPrimary: Color.Parse("#E0D4C0"), TextSecondary: Color.Parse("#C8BCA4"),
        TextMuted: Color.Parse("#8A7E68"), TextDisabled: Color.Parse("#4A4238"),
        Accent: Color.Parse("#C8963E"), AccentLight: Color.Parse("#E0B05A"), Gold: Color.Parse("#C8A96E"),
        Success: Color.Parse("#5A9E4B"), Warning: Color.Parse("#D4903A"),
        Error: Color.Parse("#B03030"), Info: Color.Parse("#4A8AB5"),
        BorderSubtle: Color.Parse("#3A3228"));

    // ── Pinky — warm pastel pink, cozy & cute ──────────────────
    public static readonly ThemeDef Pinky = new("Pinky",
        AppBg: Color.Parse("#F5EEF0"), PanelBg: Color.Parse("#FFFFFF"), CardBg: Color.Parse("#FFF0F3"),
        HoverBg: Color.Parse("#FFE0E8"), InputBg: Color.Parse("#F8F2F4"),
        TextPrimary: Color.Parse("#4A3040"), TextSecondary: Color.Parse("#7A5A68"),
        TextMuted: Color.Parse("#B898A8"), TextDisabled: Color.Parse("#D4C0C8"),
        Accent: Color.Parse("#E8789A"), AccentLight: Color.Parse("#F09AB4"), Gold: Color.Parse("#D4A06A"),
        Success: Color.Parse("#7BC8A4"), Warning: Color.Parse("#E8A862"),
        Error: Color.Parse("#E06070"), Info: Color.Parse("#82AAD4"),
        BorderSubtle: Color.Parse("#EADCE0"),
        IsLight: true);

    // ── Hacker — acid green on black, 90s terminal rave ─────────
    public static readonly ThemeDef Hacker = new("Hacker",
        AppBg: Color.Parse("#000000"), PanelBg: Color.Parse("#0A0A0A"), CardBg: Color.Parse("#111111"),
        HoverBg: Color.Parse("#1A1A1A"), InputBg: Color.Parse("#050505"),
        TextPrimary: Color.Parse("#00FF41"), TextSecondary: Color.Parse("#00CC33"),
        TextMuted: Color.Parse("#008820"), TextDisabled: Color.Parse("#004410"),
        Accent: Color.Parse("#00FF41"), AccentLight: Color.Parse("#33FF66"), Gold: Color.Parse("#CCFF00"),
        Success: Color.Parse("#00FF41"), Warning: Color.Parse("#CCFF00"),
        Error: Color.Parse("#FF0040"), Info: Color.Parse("#00CCFF"),
        BorderSubtle: Color.Parse("#1A1A1A"));

    // ── Clown — chaotic circus madness ───────────────────────────
    public static readonly ThemeDef Clown = new("Clown",
        AppBg: Color.Parse("#1A0A2E"), PanelBg: Color.Parse("#220E3A"), CardBg: Color.Parse("#2E1448"),
        HoverBg: Color.Parse("#3E1A5E"), InputBg: Color.Parse("#140828"),
        TextPrimary: Color.Parse("#F0E840"), TextSecondary: Color.Parse("#E0D030"),
        TextMuted: Color.Parse("#A09020"), TextDisabled: Color.Parse("#605010"),
        Accent: Color.Parse("#FF2020"), AccentLight: Color.Parse("#FF5050"), Gold: Color.Parse("#FFD700"),
        Success: Color.Parse("#00FF88"), Warning: Color.Parse("#FF8C00"),
        Error: Color.Parse("#FF1493"), Info: Color.Parse("#00FFFF"),
        BorderSubtle: Color.Parse("#4A1870"));

    // ── Gov — generic government website, sterile blue-gray ────
    public static readonly ThemeDef Gov = new("Gov",
        AppBg: Color.Parse("#E8EDF2"), PanelBg: Color.Parse("#FFFFFF"), CardBg: Color.Parse("#F0F4F8"),
        HoverBg: Color.Parse("#D0DCE8"), InputBg: Color.Parse("#EEF1F5"),
        TextPrimary: Color.Parse("#1A2B3C"), TextSecondary: Color.Parse("#445566"),
        TextMuted: Color.Parse("#8899AA"), TextDisabled: Color.Parse("#B0BEC5"),
        Accent: Color.Parse("#0D4CD3"), AccentLight: Color.Parse("#2E6AE6"), Gold: Color.Parse("#B8860B"),
        Success: Color.Parse("#2E7D32"), Warning: Color.Parse("#E65100"),
        Error: Color.Parse("#C62828"), Info: Color.Parse("#0D4CD3"),
        BorderSubtle: Color.Parse("#C8D4E0"),
        IsLight: true);

    // ── Nature — warm wood & amber, cozy cabin vibes ───────────
    public static readonly ThemeDef Nature = new("Nature",
        AppBg: Color.Parse("#1C1610"), PanelBg: Color.Parse("#2A2018"), CardBg: Color.Parse("#362A1E"),
        HoverBg: Color.Parse("#443626"), InputBg: Color.Parse("#18120C"),
        TextPrimary: Color.Parse("#E8DCC8"), TextSecondary: Color.Parse("#C4B498"),
        TextMuted: Color.Parse("#8A7A60"), TextDisabled: Color.Parse("#5A4E3C"),
        Accent: Color.Parse("#C89840"), AccentLight: Color.Parse("#E0B050"), Gold: Color.Parse("#D4A830"),
        Success: Color.Parse("#7AAC56"), Warning: Color.Parse("#D4A030"),
        Error: Color.Parse("#B85040"), Info: Color.Parse("#6A9EB0"),
        BorderSubtle: Color.Parse("#443626"));

    public static readonly ThemeDef[] AllThemes = [Paramonov, Light, Dota2, Cyberpunk, Wow, Nord, Synthwave, Bg3, Pinky, Hacker, Clown, Gov, Nature];

    public static void ApplyTheme(Application app, ThemeDef theme)
    {
        SetBrush(app, "AppBgBrush", theme.AppBg);
        SetBrush(app, "PanelBgBrush", theme.PanelBg);
        SetBrush(app, "CardBgBrush", theme.CardBg);
        SetBrush(app, "HoverBgBrush", theme.HoverBg);
        SetBrush(app, "InputBgBrush", theme.InputBg);
        SetBrush(app, "TextPrimaryBrush", theme.TextPrimary);
        SetBrush(app, "TextSecondaryBrush", theme.TextSecondary);
        SetBrush(app, "TextMutedBrush", theme.TextMuted);
        SetBrush(app, "TextDisabledBrush", theme.TextDisabled);
        SetBrush(app, "AccentBrush", theme.Accent);
        SetBrush(app, "AccentLightBrush", theme.AccentLight);
        SetBrush(app, "GoldBrush", theme.Gold);
        SetBrush(app, "SuccessBrush", theme.Success);
        SetBrush(app, "WarningBrush", theme.Warning);
        SetBrush(app, "ErrorBrush", theme.Error);
        SetBrush(app, "InfoBrush", theme.Info);
        SetBrush(app, "BorderSubtleBrush", theme.BorderSubtle);

        // Also update Color resources
        SetColor(app, "AppBg", theme.AppBg);
        SetColor(app, "PanelBg", theme.PanelBg);
        SetColor(app, "CardBg", theme.CardBg);
        SetColor(app, "TextPrimary", theme.TextPrimary);
        SetColor(app, "Accent", theme.Accent);

        // Rarity colors
        SetBrush(app, "RarityCommonBrush", theme.RarityCommon);
        SetBrush(app, "RarityUncommonBrush", theme.RarityUncommon);
        SetBrush(app, "RarityRareBrush", theme.RarityRare);
        SetBrush(app, "RarityVeryRareBrush", theme.RarityVeryRare);
        SetBrush(app, "RarityLegendaryBrush", theme.RarityLegendary);

        // Switch FluentTheme variant for light/dark themes
        app.RequestedThemeVariant = theme.IsLight ? ThemeVariant.Light : ThemeVariant.Dark;
    }

    private static void SetBrush(Application app, string key, Color color)
    {
        if (app.Resources.TryGetResource(key, null, out var existing) && existing is SolidColorBrush brush)
            brush.Color = color;
        else
            app.Resources[key] = new SolidColorBrush(color);
    }

    private static void SetColor(Application app, string key, Color color)
    {
        app.Resources[key] = color;
    }
}
