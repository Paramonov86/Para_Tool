
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Services;
using ParaTool.App.Localization;

namespace ParaTool.App.Controls;

/// <summary>
/// Multi-select toggle chips for semicolon-separated enum values.
/// Each option is a clickable chip that toggles on/off.
/// </summary>
public class ToggleChipsEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ToggleChipsEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string[]?> OptionsProperty =
        AvaloniaProperty.Register<ToggleChipsEditor, string[]?>(nameof(Options));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string[]? Options
    {
        get => GetValue(OptionsProperty);
        set => SetValue(OptionsProperty, value);
    }

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal };
    private bool _updating;

    private static SolidColorBrush OffBg => Themes.ThemeBrushes.InputBg;
    private static SolidColorBrush OnBg => Themes.ThemeBrushes.HoverBg;
    private static SolidColorBrush OffFg => Themes.ThemeBrushes.TextMuted;
    private static SolidColorBrush OnFg => Themes.ThemeBrushes.TextPrimary;
    private static SolidColorBrush OnBorder => Themes.ThemeBrushes.Accent;
    private static SolidColorBrush OffBorder => Themes.ThemeBrushes.BorderSubtle;

    public ToggleChipsEditor()
    {
        Content = _panel;
        PropertyChanged += (_, e) =>
        {
            if ((e.Property == TextProperty || e.Property == OptionsProperty) && !_updating)
                Rebuild();
        };
        FontScale.ScaleChanged += () => { if (!_updating) Rebuild(); };
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        var options = Options;
        if (options == null) return;

        var selected = new HashSet<string>(
            (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        var lang = Localization.Loc.Instance.Lang;
        foreach (var opt in options)
        {
            var isOn = selected.Contains(opt);
            var btn = new Button
            {
                Content = Loc.Instance[$"enum.{opt}"] is var locLabel && locLabel != $"enum.{opt}"
                    ? locLabel : ParaTool.Core.Schema.EnumLabels.GetLabel(opt, lang),
                Tag = opt,
                FontSize = FontScale.Of(11),
                Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 4, 4),
                CornerRadius = new CornerRadius(10),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = isOn ? OnBg : OffBg,
                Foreground = isOn ? OnFg : OffFg,
                BorderThickness = new Thickness(isOn ? 1.5 : 1),
                BorderBrush = isOn ? OnBorder : OffBorder,
            };
            btn.Click += OnChipClick;
            _panel.Children.Add(btn);
        }
    }

    private void OnChipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string opt) return;

        var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (parts.Contains(opt, StringComparer.OrdinalIgnoreCase))
            parts.RemoveAll(p => p.Equals(opt, StringComparison.OrdinalIgnoreCase));
        else
            parts.Add(opt);

        _updating = true;
        Text = string.Join(";", parts);
        _updating = false;
        Rebuild();
    }
}
