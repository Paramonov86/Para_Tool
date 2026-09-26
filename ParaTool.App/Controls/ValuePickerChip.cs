using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ParaTool.App.Services;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

/// <summary>One choice of a <see cref="ValuePickerChip"/>: the value written, its name, a detail line.</summary>
public sealed record PickOption(string Value, string Label, string? Detail = null);

/// <summary>
/// A chip for a field that can be inherited, turned off or set: <see cref="Text"/> null means
/// "as the original has it" (shown as <see cref="Inherited"/>), "" means off, anything else is the
/// value. Clicking opens a searchable list of <see cref="Options"/>; with <see cref="FreeText"/> a
/// typed value not on the list can be used too.
/// </summary>
public class ValuePickerChip : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ValuePickerChip, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);
    public static readonly StyledProperty<IReadOnlyList<PickOption>?> OptionsProperty =
        AvaloniaProperty.Register<ValuePickerChip, IReadOnlyList<PickOption>?>(nameof(Options));
    public static readonly StyledProperty<string?> InheritedProperty =
        AvaloniaProperty.Register<ValuePickerChip, string?>(nameof(Inherited));
    public static readonly StyledProperty<bool> AllowInheritProperty =
        AvaloniaProperty.Register<ValuePickerChip, bool>(nameof(AllowInherit));
    public static readonly StyledProperty<bool> FreeTextProperty =
        AvaloniaProperty.Register<ValuePickerChip, bool>(nameof(FreeText));

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }
    public IReadOnlyList<PickOption>? Options { get => GetValue(OptionsProperty); set => SetValue(OptionsProperty, value); }
    /// <summary>The value the original gives the field (its base's), shown while the card leaves it alone.</summary>
    public string? Inherited { get => GetValue(InheritedProperty); set => SetValue(InheritedProperty, value); }
    /// <summary>The card has an original to inherit from (a copy or an edited original).</summary>
    public bool AllowInherit { get => GetValue(AllowInheritProperty); set => SetValue(AllowInheritProperty, value); }
    public bool FreeText { get => GetValue(FreeTextProperty); set => SetValue(FreeTextProperty, value); }

    private readonly Border _chip;
    private readonly TextBlock _valueText;
    private Panel? _overlay;

    public ValuePickerChip()
    {
        _valueText = new TextBlock
        {
            FontSize = FontScale.Of(11),
            VerticalAlignment = VerticalAlignment.Center,
            TextTrimming = TextTrimming.CharacterEllipsis,
        };
        _chip = new Border
        {
            Child = _valueText,
            MinWidth = 60, MinHeight = 26,
            Padding = new Thickness(8, 3),
            CornerRadius = new CornerRadius(6),
            Background = ThemeBrushes.InputBg,
            BorderBrush = ThemeBrushes.BorderSubtle,
            BorderThickness = new Thickness(1),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = HorizontalAlignment.Left,
        };
        _chip.PointerPressed += (_, e) =>
        {
            if (!e.GetCurrentPoint(_chip).Properties.IsLeftButtonPressed) return;
            OpenPicker();
            e.Handled = true;
        };
        _chip.PointerEntered += (_, _) => _chip.Background = ThemeBrushes.HoverBg;
        _chip.PointerExited += (_, _) => _chip.Background = ThemeBrushes.InputBg;
        Content = _chip;

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty || e.Property == OptionsProperty || e.Property == InheritedProperty || e.Property == AllowInheritProperty)
                UpdateDisplay();
        };
        Action scale = () => _valueText.FontSize = FontScale.Of(11);
        FontScale.ScaleChanged += scale;
        DetachedFromVisualTree += (_, _) => FontScale.ScaleChanged -= scale;
        UpdateDisplay();
    }

    private PickOption? Find(string value) =>
        Options?.FirstOrDefault(o => o.Value.Equals(value, StringComparison.OrdinalIgnoreCase));

    private string LabelOf(string value) => Find(value)?.Label ?? value;

    /// <summary>What the chip reads: "↳ inherited", "Off" or the value's name.</summary>
    public string DisplayText
    {
        get
        {
            var loc = Localization.Loc.Instance;
            if (Text == null)
                return AllowInherit
                    ? "↳ " + (string.IsNullOrEmpty(Inherited) ? loc.ValNone : LabelOf(Inherited))
                    : loc.ValNone;
            return Text.Length == 0 ? loc.ValOff : LabelOf(Text);
        }
    }

    private void UpdateDisplay()
    {
        _valueText.Text = DisplayText;
        var set = !string.IsNullOrEmpty(Text);
        _valueText.Foreground = set ? ThemeBrushes.TextPrimary : ThemeBrushes.TextMuted;
        _valueText.FontStyle = Text == null && AllowInherit ? FontStyle.Italic : FontStyle.Normal;
        var shown = Text ?? Inherited;
        var detail = string.IsNullOrEmpty(shown) ? null : Find(shown)?.Detail ?? shown;
        ToolTip.SetTip(_chip, Text == null && AllowInherit
            ? Localization.Loc.Instance.ValInherited + (detail != null ? "\n" + detail : "")
            : detail);
    }

    private sealed record Row(string Label, string? Detail, Func<string?> Pick, bool Special);

    public void OpenPicker()
    {
        if (_overlay != null) return;
        if (TopLevel.GetTopLevel(this) is not Window w || w.Content is not Panel rootPanel) return;
        var loc = Localization.Loc.Instance;
        var options = Options ?? [];

        var search = new TextBox
        {
            Watermark = loc.WmSearch,
            FontSize = FontScale.Of(13), Padding = new Thickness(10, 8),
            Background = ThemeBrushes.InputBg, Foreground = ThemeBrushes.TextPrimary,
            CornerRadius = new CornerRadius(6), Margin = new Thickness(0, 0, 0, 8),
        };

        var list = new ListBox
        {
            MaxHeight = 420,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ItemTemplate = new FuncDataTemplate<Row>((row, _) =>
            {
                if (row == null) return new TextBlock();
                var stack = new StackPanel { Spacing = 1 };
                stack.Children.Add(new TextBlock
                {
                    Text = row.Label, FontSize = FontScale.Of(12),
                    Foreground = row.Special ? ThemeBrushes.AccentLight : ThemeBrushes.TextPrimary,
                    TextTrimming = TextTrimming.CharacterEllipsis,
                });
                if (!string.IsNullOrEmpty(row.Detail))
                    stack.Children.Add(new TextBlock
                    {
                        Text = row.Detail, FontSize = FontScale.Of(10), Foreground = ThemeBrushes.TextMuted,
                        TextTrimming = TextTrimming.CharacterEllipsis,
                    });
                return stack;
            }),
        };

        List<Row> Rows(string query)
        {
            var rows = new List<Row>();
            if (AllowInherit)
                rows.Add(new Row(loc.ValInherited + (string.IsNullOrEmpty(Inherited) ? "" : ": " + LabelOf(Inherited)),
                    null, () => null, true));
            rows.Add(new Row(loc.ValOff, null, () => "", true));
            IEnumerable<PickOption> hits = options;
            if (query.Length > 0)
                hits = options.Where(o => o.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                                          || o.Value.Contains(query, StringComparison.OrdinalIgnoreCase)
                                          || (o.Detail?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false));
            if (FreeText && query.Length > 0 && !options.Any(o => o.Value.Equals(query, StringComparison.OrdinalIgnoreCase)))
            {
                var typed = query;
                rows.Add(new Row(string.Format(loc.ValUseTyped, typed), null, () => typed, true));
            }
            rows.AddRange(hits.Select(o => new Row(o.Label, o.Detail ?? (o.Label == o.Value ? null : o.Value), () => o.Value, false)));
            return rows;
        }

        list.ItemsSource = Rows("");
        list.SelectionChanged += (_, _) =>
        {
            if (list.SelectedItem is not Row row) return;
            Text = row.Pick();
            UpdateDisplay();
            ClosePicker();
        };

        DispatcherTimer? debounce = null;
        search.TextChanged += (_, _) =>
        {
            debounce?.Stop();
            debounce = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            debounce.Tick += (_, _) => { debounce.Stop(); list.ItemsSource = Rows(search.Text?.Trim() ?? ""); };
            debounce.Start();
        };

        var border = new Border
        {
            Child = new StackPanel { Children = { search, list } },
            Background = ThemeBrushes.PanelBg,
            BorderBrush = ThemeBrushes.Accent,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(16),
            MinWidth = 420, MaxWidth = 640, MaxHeight = 520,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            BoxShadow = BoxShadows.Parse("0 8 32 0 #60000000"),
        };
        var dimmer = new Border
        {
            Background = new SolidColorBrush(Colors.Black, 0.45),
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment = VerticalAlignment.Stretch,
        };
        dimmer.PointerPressed += (_, e) => { ClosePicker(); e.Handled = true; };
        _overlay = new Panel
        {
            ZIndex = 9500,
            [Grid.RowSpanProperty] = 99,
            [Grid.ColumnSpanProperty] = 99,
            Children = { dimmer, border },
        };
        rootPanel.Children.Add(_overlay);
        Dispatcher.UIThread.Post(() => search.Focus());
    }

    private void ClosePicker()
    {
        if (_overlay?.Parent is Panel panel) panel.Children.Remove(_overlay);
        _overlay = null;
    }
}
