
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ParaTool.App.Themes;
using ParaTool.App.Services;

namespace ParaTool.App.Controls;

/// <summary>
/// Chip that opens a large searchable picker overlay when clicked.
/// For selecting from potentially unlimited lists (statuses, spells, etc.)
/// with dimmer background and search filter.
/// </summary>
public class SearchPickerChip : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<SearchPickerChip, string?>(nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string[]?> ItemsProperty =
        AvaloniaProperty.Register<SearchPickerChip, string[]?>(nameof(Items));

    public static readonly StyledProperty<string?> WatermarkProperty =
        AvaloniaProperty.Register<SearchPickerChip, string?>(nameof(Watermark), "Search...");

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public string[]? Items { get => GetValue(ItemsProperty); set => SetValue(ItemsProperty, value); }
    public string? Watermark { get => GetValue(WatermarkProperty); set => SetValue(WatermarkProperty, value); }

    private readonly Border _chip;
    private readonly TextBlock _valueText;
    private Panel? _overlay;

    public SearchPickerChip()
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

        _chip.PointerPressed += (_, e) => { OpenPicker(); e.Handled = true; };
        _chip.PointerEntered += (_, _) => _chip.Background = ThemeBrushes.HoverBg;
        _chip.PointerExited += (_, _) => _chip.Background = ThemeBrushes.InputBg;

        Content = _chip;

        PropertyChanged += (_, e) => { if (e.Property == TextProperty) UpdateDisplay(); };
        Action scaleHandler = () => _valueText.FontSize = FontScale.Of(11);
        FontScale.ScaleChanged += scaleHandler;
        DetachedFromVisualTree += (_, _) => FontScale.ScaleChanged -= scaleHandler;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var val = Text?.Trim() ?? "";
        _valueText.Text = string.IsNullOrEmpty(val) ? "..." : val;
        _valueText.Foreground = string.IsNullOrEmpty(val)
            ? ThemeBrushes.TextMuted : ThemeBrushes.TextPrimary;
    }

    public void OpenPicker()
    {
        if (_overlay != null) return;
        if (TopLevel.GetTopLevel(this) is not Window w || w.Content is not Panel rootPanel) return;

        var items = Items ?? [];

        // Search box
        var searchBox = new TextBox
        {
            Watermark = Watermark ?? Localization.Loc.Instance.WmSearch,
            FontSize = FontScale.Of(13), Padding = new Thickness(10, 8),
            Background = ThemeBrushes.InputBg,
            Foreground = ThemeBrushes.TextPrimary,
            CornerRadius = new CornerRadius(6),
            Margin = new Thickness(0, 0, 0, 8),
        };

        // List
        var listBox = new ListBox
        {
            MaxHeight = 400,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ItemsSource = items,
        };

        // Style list items
        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is string selected)
            {
                Text = selected;
                ClosePicker();
            }
        };

        // Search filter
        searchBox.TextChanged += (_, _) =>
        {
            var query = searchBox.Text?.Trim() ?? "";
            if (string.IsNullOrEmpty(query))
                listBox.ItemsSource = items;
            else
                listBox.ItemsSource = items.Where(i =>
                    i.Contains(query, StringComparison.OrdinalIgnoreCase)).ToArray();
        };

        var pickerPanel = new StackPanel
        {
            Children = { searchBox, listBox },
        };

        var pickerBorder = new Border
        {
            Child = pickerPanel,
            Background = ThemeBrushes.PanelBg,
            BorderBrush = ThemeBrushes.Accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            Width = 350, MaxHeight = 500,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BoxShadow = BoxShadows.Parse("0 8 32 0 #60000000"),
        };

        // Dimmer
        var dimmer = new Border
        {
            Background = new SolidColorBrush(Colors.Black, 0.45),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
            IsHitTestVisible = true,
        };
        dimmer.PointerPressed += (_, e) => { ClosePicker(); e.Handled = true; };

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
            Duration = TimeSpan.FromMilliseconds(150),
        }];
        Dispatcher.UIThread.Post(() =>
        {
            if (_overlay != null) _overlay.Opacity = 1;
            searchBox.Focus();
        });
    }

    private void ClosePicker()
    {
        if (_overlay == null) return;
        if (_overlay.Parent is Panel panel)
            panel.Children.Remove(_overlay);
        _overlay = null;
    }
}
