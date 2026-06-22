using System.Collections.Generic;
using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

// ── Item-tab active state = IsSelected AND NOT showing the Journal ──────────
// (so opening the Journal tab clears the item tab's active highlight)

file static class TabActive
{
    public static bool Of(IList<object?> v) =>
        v.Count > 0 && v[0] is true && !(v.Count > 1 && v[1] is true);
}

public class TabActiveBrushConverter : IMultiValueConverter
{
    public static readonly TabActiveBrushConverter Instance = new();
    public object Convert(IList<object?> v, Type t, object? p, CultureInfo c) =>
        TabActive.Of(v) ? ThemeBrushes.Accent : Brushes.Transparent;
}

public class TabActiveBgConverter : IMultiValueConverter
{
    public static readonly TabActiveBgConverter Instance = new();
    public object Convert(IList<object?> v, Type t, object? p, CultureInfo c) =>
        TabActive.Of(v) ? ThemeBrushes.CardBg : ThemeBrushes.InputBg;
}

public class TabActiveFgConverter : IMultiValueConverter
{
    public static readonly TabActiveFgConverter Instance = new();
    public object Convert(IList<object?> v, Type t, object? p, CultureInfo c) =>
        TabActive.Of(v) ? ThemeBrushes.TextPrimary : ThemeBrushes.TextMuted;
}

/// <summary>Bool → full opacity when active (Journal shown), dimmed when not.</summary>
public class BoolToActiveOpacityConverter : IValueConverter
{
    public static readonly BoolToActiveOpacityConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? 1.0 : 0.55;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => false;
}

/// <summary>Bool → accent border brush for active tab, transparent for inactive.</summary>
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? ThemeBrushes.Accent : Brushes.Transparent;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => false;
}

/// <summary>Bool → card bg for active tab, input bg for inactive.</summary>
public class BoolToBgConverter : IValueConverter
{
    public static readonly BoolToBgConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? ThemeBrushes.CardBg : ThemeBrushes.InputBg;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => false;
}

/// <summary>Bool → primary text for active tab, muted for inactive.</summary>
public class BoolToFgConverter : IValueConverter
{
    public static readonly BoolToFgConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? ThemeBrushes.TextPrimary : ThemeBrushes.TextMuted;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => false;
}

/// <summary>Bool → filled diamond when pinned, hollow when not.</summary>
public class BoolToPinGlyphConverter : IValueConverter
{
    public static readonly BoolToPinGlyphConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? "◆" : "◇"; // ◆ / ◇
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => false;
}

/// <summary>Bool → accent brush when pinned, muted otherwise.</summary>
public class BoolToPinBrushConverter : IValueConverter
{
    public static readonly BoolToPinBrushConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? ThemeBrushes.Accent : ThemeBrushes.TextMuted;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => false;
}

/// <summary>Bool → dimmed opacity (0.45) when true (undone/future), full otherwise.</summary>
public class BoolToDimConverter : IValueConverter
{
    public static readonly BoolToDimConverter Instance = new();
    public object Convert(object? value, Type t, object? p, CultureInfo c) =>
        value is true ? 0.45 : 1.0;
    public object ConvertBack(object? value, Type t, object? p, CultureInfo c) => false;
}
