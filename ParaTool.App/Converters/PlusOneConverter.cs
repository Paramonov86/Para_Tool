using System.Globalization;
using Avalonia.Data.Converters;

namespace ParaTool.App.ViewModels;

public class PlusOneConverter : IValueConverter
{
    public static readonly PlusOneConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is int i) return i + 1;
        return value ?? 0;
    }

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotSupportedException();
    }
}
