using Avalonia;
using Avalonia.Media;

namespace ParaTool.App.Themes;

/// <summary>
/// Manages color themes. Updates SolidColorBrush resources at runtime.
/// </summary>
public static class ThemeManager
{
    public record ThemeDef(
        string Name,
        Color AppBg, Color PanelBg, Color CardBg, Color HoverBg, Color InputBg,
        Color TextPrimary, Color TextSecondary, Color TextMuted, Color TextDisabled,
        Color Accent, Color AccentLight, Color Gold,
        Color Success, Color Warning, Color Error, Color Info,
        Color BorderSubtle);

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
        BorderSubtle: Color.Parse("#D8D6E0"));

    public static readonly ThemeDef Dota2 = new("Dota 2",
        AppBg: Color.Parse("#0D0D0D"), PanelBg: Color.Parse("#1A1A1A"), CardBg: Color.Parse("#242424"),
        HoverBg: Color.Parse("#333333"), InputBg: Color.Parse("#151515"),
        TextPrimary: Color.Parse("#D4D4D4"), TextSecondary: Color.Parse("#999999"),
        TextMuted: Color.Parse("#666666"), TextDisabled: Color.Parse("#444444"),
        Accent: Color.Parse("#C23B22"), AccentLight: Color.Parse("#E04830"), Gold: Color.Parse("#DAA520"),
        Success: Color.Parse("#4CAF50"), Warning: Color.Parse("#FF9800"),
        Error: Color.Parse("#F44336"), Info: Color.Parse("#2196F3"),
        BorderSubtle: Color.Parse("#333333"));

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

    // ── Dracula — purple/pink/green pastel glow ───────────────
    public static readonly ThemeDef Dracula = new("Dracula",
        AppBg: Color.Parse("#282A36"), PanelBg: Color.Parse("#2D2F3D"), CardBg: Color.Parse("#343746"),
        HoverBg: Color.Parse("#44475A"), InputBg: Color.Parse("#21222C"),
        TextPrimary: Color.Parse("#F8F8F2"), TextSecondary: Color.Parse("#D0CCE0"),
        TextMuted: Color.Parse("#6272A4"), TextDisabled: Color.Parse("#4A5070"),
        Accent: Color.Parse("#BD93F9"), AccentLight: Color.Parse("#D4AAFF"), Gold: Color.Parse("#F1FA8C"),
        Success: Color.Parse("#50FA7B"), Warning: Color.Parse("#FFB86C"),
        Error: Color.Parse("#FF5555"), Info: Color.Parse("#8BE9FD"),
        BorderSubtle: Color.Parse("#44475A"));

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

    // ── BG3 Dark — amber / gold inspired by the game UI ───────
    public static readonly ThemeDef Bg3Dark = new("BG3 Dark",
        AppBg: Color.Parse("#111015"), PanelBg: Color.Parse("#1C1A22"), CardBg: Color.Parse("#26232E"),
        HoverBg: Color.Parse("#332E3C"), InputBg: Color.Parse("#15131B"),
        TextPrimary: Color.Parse("#E8DDD0"), TextSecondary: Color.Parse("#BCA88A"),
        TextMuted: Color.Parse("#7A6E5C"), TextDisabled: Color.Parse("#4A4238"),
        Accent: Color.Parse("#C8963E"), AccentLight: Color.Parse("#E0B05A"), Gold: Color.Parse("#C8963E"),
        Success: Color.Parse("#5A9E4B"), Warning: Color.Parse("#D4903A"),
        Error: Color.Parse("#B03030"), Info: Color.Parse("#4A8AB5"),
        BorderSubtle: Color.Parse("#332E3C"));

    public static readonly ThemeDef[] AllThemes = [Paramonov, Light, Dota2, Cyberpunk, Dracula, Nord, Synthwave, Bg3Dark];

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
