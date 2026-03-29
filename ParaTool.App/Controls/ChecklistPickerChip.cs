
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ParaTool.App.Themes;
using ParaTool.App.Services;
using ParaTool.App.Localization;

namespace ParaTool.App.Controls;

/// <summary>
/// Compact chip that shows selected count. Click → popup with checkboxes + scrollbar.
/// For multi-select from a known list (e.g. trigger events).
/// </summary>
public class ChecklistPickerChip : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ChecklistPickerChip, string?>(nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string[]?> OptionsProperty =
        AvaloniaProperty.Register<ChecklistPickerChip, string[]?>(nameof(Options));

    /// <summary>Optional display labels for options (same order). If null, raw option names shown.</summary>
    public static readonly StyledProperty<string[]?> LabelsProperty =
        AvaloniaProperty.Register<ChecklistPickerChip, string[]?>(nameof(Labels));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string[]? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    public string[]? Labels { get => GetValue(LabelsProperty); set => SetValue(LabelsProperty, value); }

    private readonly Border _chip;
    private readonly TextBlock _valueText;
    private Panel? _overlay;

    public ChecklistPickerChip()
    {
        _valueText = new TextBlock
        {
            FontSize = FontScale.Of(11), FontWeight = FontWeight.SemiBold,
            Foreground = ThemeBrushes.TextPrimary,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            TextAlignment = TextAlignment.Center,
        };

        _chip = new Border
        {
            Child = _valueText,
            MinWidth = 60, Height = 28,
            Padding = new Thickness(8, 0),
            CornerRadius = new CornerRadius(6),
            Background = ThemeBrushes.InputBg,
            BorderBrush = ThemeBrushes.BorderSubtle,
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
        };

        _chip.PointerPressed += (_, e) => { OpenChecklist(); e.Handled = true; };
        _chip.PointerEntered += (_, _) => _chip.Background = ThemeBrushes.HoverBg;
        _chip.PointerExited += (_, _) => _chip.Background = ThemeBrushes.InputBg;

        Content = _chip;
        PropertyChanged += (_, e) => { if (e.Property == TextProperty) UpdateDisplay(); };
        FontScale.ScaleChanged += () => _valueText.FontSize = FontScale.Of(11);
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var selected = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (selected.Length == 0)
            _valueText.Text = "—";
        else if (selected.Length == 1)
            _valueText.Text = GetDisplayLabel(selected[0]);
        else
            _valueText.Text = string.Join(", ", selected.Select(GetDisplayLabel));
        _valueText.Foreground = selected.Length > 0 ? ThemeBrushes.TextPrimary : ThemeBrushes.TextMuted;
    }

    private string GetDisplayLabel(string rawValue)
    {
        var options = Options;
        var labels = Labels;
        if (options != null && labels != null)
        {
            var idx = Array.FindIndex(options, o => o.Equals(rawValue, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0 && idx < labels.Length) return labels[idx];
        }
        // Fallback: loca
        var locaKey = $"enum.{rawValue}";
        var locaVal = Localization.Loc.Instance[locaKey];
        return locaVal != locaKey ? locaVal : rawValue;
    }

    private void OpenChecklist()
    {
        if (_overlay != null) return;
        if (TopLevel.GetTopLevel(this) is not Window w || w.Content is not Panel rootPanel) return;

        var options = Options ?? [];
        var labels = Labels;
        var selected = new HashSet<string>(
            (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            StringComparer.OrdinalIgnoreCase);

        var checkStack = new StackPanel { Spacing = 2 };

        for (int i = 0; i < options.Length; i++)
        {
            var opt = options[i];
            var label = labels != null && i < labels.Length ? labels[i] : opt;
            var cb = new CheckBox
            {
                Content = label,
                IsChecked = selected.Contains(opt),
                FontSize = FontScale.Of(12),
                Foreground = ThemeBrushes.TextPrimary,
                Tag = opt,
            };
            cb.IsCheckedChanged += (s, _) =>
            {
                if (s is CheckBox c && c.Tag is string o)
                {
                    if (c.IsChecked == true)
                        selected.Add(o);
                    else
                        selected.Remove(o);
                    Text = string.Join(";", selected);
                }
            };
            checkStack.Children.Add(cb);
        }

        var scroll = new ScrollViewer
        {
            Content = checkStack,
            MaxHeight = 350,
        };

        var pickerBorder = new Border
        {
            Child = scroll,
            Background = ThemeBrushes.PanelBg,
            BorderBrush = ThemeBrushes.Accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(12, 10),
            Width = 280,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BoxShadow = BoxShadows.Parse("0 6 24 0 #50000000"),
        };

        var dimmer = new Border
        {
            Background = new SolidColorBrush(Colors.Black, 0.4),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = true,
        };
        dimmer.PointerPressed += (_, e) => { CloseChecklist(); e.Handled = true; };

        _overlay = new Panel
        {
            ZIndex = 9500,
            [Grid.RowSpanProperty] = 99,
            [Grid.ColumnSpanProperty] = 99,
            Children = { dimmer, pickerBorder },
            Opacity = 0,
        };

        rootPanel.Children.Add(_overlay);
        _overlay.Transitions = [new Avalonia.Animation.DoubleTransition
        {
            Property = OpacityProperty,
            Duration = TimeSpan.FromMilliseconds(120),
        }];
        Dispatcher.UIThread.Post(() => { if (_overlay != null) _overlay.Opacity = 1; });
    }

    private void CloseChecklist()
    {
        if (_overlay == null) return;
        if (_overlay.Parent is Panel panel)
            panel.Children.Remove(_overlay);
        _overlay = null;
    }
}
