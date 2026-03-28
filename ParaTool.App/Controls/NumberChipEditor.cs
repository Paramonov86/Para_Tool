using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

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

    private static readonly SolidColorBrush ChipBg = new(Color.Parse("#252330"));
    private static readonly SolidColorBrush ChipBgHover = new(Color.Parse("#3D3A4D"));
    private static readonly SolidColorBrush ChipFg = new(Color.Parse("#E0DDE6"));
    private static readonly SolidColorBrush AccentBrush = new(Color.Parse("#6C5CE7"));
    private static readonly SolidColorBrush MutedBrush = new(Color.Parse("#8A8494"));

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
        var popup = new Popup
        {
            PlacementTarget = _chip,
            Placement = PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            MaxHeight = 300,
        };

        var wrapPanel = new WrapPanel
        {
            ItemWidth = 36, ItemHeight = 32,
            MaxWidth = 260,
        };

        int currentVal = int.TryParse(Text, out var cv) ? cv : -1;

        for (int i = MinValue; i <= MaxValue; i++)
        {
            var num = i;
            var isSelected = num == currentVal;
            var btn = new Button
            {
                Content = num.ToString(),
                FontSize = 11, FontWeight = isSelected ? FontWeight.Bold : FontWeight.Normal,
                Padding = new Thickness(2),
                Margin = new Thickness(1),
                MinWidth = 32, MinHeight = 28,
                CornerRadius = new CornerRadius(6),
                Background = isSelected ? AccentBrush : ChipBg,
                Foreground = isSelected ? Brushes.White : ChipFg,
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
                HorizontalContentAlignment = HorizontalAlignment.Center,
            };
            btn.Click += (_, _) =>
            {
                Text = num.ToString();
                popup.IsOpen = false;
            };
            wrapPanel.Children.Add(btn);
        }

        popup.Child = new Border
        {
            Child = new ScrollViewer { Content = wrapPanel, MaxHeight = 250 },
            Background = new SolidColorBrush(Color.Parse("#1E1B26")),
            BorderBrush = new SolidColorBrush(Color.Parse("#33FFFFFF")),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(6),
        };

        if (_chip.Parent is Avalonia.Controls.Panel panel)
        {
            panel.Children.Add(popup);
            popup.IsOpen = true;
            popup.Closed += (_, _) => panel.Children.Remove(popup);
        }
    }
}
