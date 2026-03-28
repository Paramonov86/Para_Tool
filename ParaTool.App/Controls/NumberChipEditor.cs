using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

/// <summary>
/// Compact square number selector — like a calendar day picker.
/// Shows current value in a small square chip. Click → popup grid 1-99.
/// </summary>
public class NumberChipEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<NumberChipEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<int> MinValueProperty =
        AvaloniaProperty.Register<NumberChipEditor, int>(nameof(MinValue), 0);

    public static readonly StyledProperty<int> MaxValueProperty =
        AvaloniaProperty.Register<NumberChipEditor, int>(nameof(MaxValue), 30);

    public static readonly StyledProperty<string?> LabelProperty =
        AvaloniaProperty.Register<NumberChipEditor, string?>(nameof(Label));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public int MinValue
    {
        get => GetValue(MinValueProperty);
        set => SetValue(MinValueProperty, value);
    }

    public int MaxValue
    {
        get => GetValue(MaxValueProperty);
        set => SetValue(MaxValueProperty, value);
    }

    public string? Label
    {
        get => GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    private static SolidColorBrush ChipBg => Themes.ThemeBrushes.InputBg;
    private static SolidColorBrush ChipBgHover => Themes.ThemeBrushes.CardBg;
    private static SolidColorBrush ChipFg => Themes.ThemeBrushes.TextPrimary;
    private static SolidColorBrush AccentBrush => Themes.ThemeBrushes.Accent;
    private static SolidColorBrush MutedBrush => Themes.ThemeBrushes.TextMuted;

    private readonly Button _chip;
    private readonly TextBlock _valueText;
    private readonly TextBlock _labelText;

    public NumberChipEditor()
    {
        _valueText = new TextBlock
        {
            FontSize = 14, FontWeight = FontWeight.Bold,
            Foreground = ChipFg,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        _labelText = new TextBlock
        {
            FontSize = 9, Foreground = MutedBrush,
            HorizontalAlignment = HorizontalAlignment.Center,
        };

        var stack = new StackPanel
        {
            HorizontalAlignment = HorizontalAlignment.Center,
            Spacing = 1,
            Children = { _valueText, _labelText }
        };

        _chip = new Button
        {
            Content = stack,
            MinWidth = 48, MinHeight = 40,
            Padding = new Thickness(6, 4),
            CornerRadius = new CornerRadius(8),
            Background = ChipBg,
            BorderBrush = new SolidColorBrush(Color.Parse("#3D3A4D")),
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalContentAlignment = HorizontalAlignment.Center,
            VerticalContentAlignment = VerticalAlignment.Center,
        };

        _chip.Click += OnChipClick;
        Content = _chip;

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty || e.Property == LabelProperty)
                UpdateDisplay();
        };

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var val = Text?.Trim() ?? "";
        _valueText.Text = string.IsNullOrEmpty(val) ? "—" : val;
        _labelText.Text = Label ?? "";
        _labelText.IsVisible = !string.IsNullOrEmpty(Label);
    }

    private void OnChipClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        int currentVal = int.TryParse(Text, out var cv) ? cv : -1;

        for (int i = MinValue; i <= MaxValue; i++)
        {
            var num = i;
            var item = new MenuItem
            {
                Header = num.ToString(),
                FontWeight = num == currentVal ? FontWeight.Bold : FontWeight.Normal,
            };
            item.Click += (_, _) => Text = num.ToString();
            menu.Items.Add(item);
        }

        menu.Open(_chip);
    }
}
