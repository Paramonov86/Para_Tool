using System.Globalization;
using Avalonia.Data.Converters;
using ParaTool.App.Localization;

namespace ParaTool.App.Converters;

public class PoolNameConverter : IValueConverter
{
    public static readonly PoolNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string pool ? Loc.Instance.PoolName(pool) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class RarityNameConverter : IValueConverter
{
    public static readonly RarityNameConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is string rarity ? Loc.Instance.RarityName(rarity) : value;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}

public class ExpandArrowConverter : IValueConverter
{
    public static readonly ExpandArrowConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is true ? "\u25BC" : "\u25B6"; // ▼ / ▶

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException();
}
