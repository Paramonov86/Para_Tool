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

    public static readonly ThemeDef[] AllThemes = [Paramonov, Light, Dota2];

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
