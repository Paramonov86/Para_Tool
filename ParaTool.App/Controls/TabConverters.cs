using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

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
