using System;

namespace ParaTool.App.Services;

public static class FontScale
{
    public static double Factor { get; set; } = 1.0;
    public static double Of(double baseSize) => Math.Round(baseSize * Factor);

    public static event Action? ScaleChanged;
    public static void NotifyChanged() => ScaleChanged?.Invoke();
}
