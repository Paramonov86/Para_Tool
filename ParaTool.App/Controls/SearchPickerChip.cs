
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

    /// <summary>Optional resolver + loca for resolving display names of stats entries.</summary>
    public Core.Parsing.StatsResolver? Resolver { get; set; }
    public Core.Services.LocaService? LocaService { get; set; }

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

        _chip.PointerPressed += (_, e) => { if (e.GetCurrentPoint(_chip).Properties.IsLeftButtonPressed) { OpenPicker(); e.Handled = true; } };
        _chip.PointerEntered += (_, _) => _chip.Background = ThemeBrushes.HoverBg;
        _chip.PointerExited += (_, _) => _chip.Background = ThemeBrushes.InputBg;

        Content = _chip;

        PropertyChanged += (_, e) => { if (e.Property == TextProperty) UpdateDisplay(); };
        AttachedToVisualTree += (_, _) => UpdateDisplay();
        BoostBlocksEditor.GlobalResolverReady += () => Avalonia.Threading.Dispatcher.UIThread.Post(UpdateDisplay);
        // If resolver already set (event already fired before subscription)
        if (BoostBlocksEditor.GlobalResolver != null)
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateDisplay);
        Action scaleHandler = () => _valueText.FontSize = FontScale.Of(11);
        FontScale.ScaleChanged += scaleHandler;
        DetachedFromVisualTree += (_, _) => FontScale.ScaleChanged -= scaleHandler;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        var val = Text?.Trim() ?? "";
        if (string.IsNullOrEmpty(val))
        {
            _valueText.Text = "...";
            _valueText.Foreground = ThemeBrushes.TextMuted;
            ToolTip.SetTip(_chip, null);
            return;
        }
        var displayName = ResolveDisplayName(val);
        _valueText.Text = displayName ?? val;
        _valueText.Foreground = ThemeBrushes.TextPrimary;
        if (displayName != null)
            ToolTip.SetTip(_chip, val);
        else
            ToolTip.SetTip(_chip, null);
    }

    /// <summary>Resolve localized display name for a StatId (spell/status/passive).
    /// Tries current lang, then EN fallback, then walks using-chain.</summary>
    public static string? ResolveStatDisplayName(string statId, string lang,
        Core.Parsing.StatsResolver? resolver, Core.Services.LocaService? locaSvc)
    {
        // 0. Check active renames from current artifact
        var spellRenames = BoostBlocksEditor.ActiveSpellRenames;
        if (spellRenames != null && spellRenames.TryGetValue(statId, out var spRn))
        {
            if (spRn.TryGetValue(lang, out var rnName) && !string.IsNullOrEmpty(rnName)) return rnName;
            if (spRn.TryGetValue("en", out var rnEn) && !string.IsNullOrEmpty(rnEn)) return rnEn;
        }
        if (BoostBlocksEditor.ActiveStatusRenames?.TryGetValue(statId, out var stRn) == true)
        {
            if (stRn.TryGetValue(lang, out var rnName) && !string.IsNullOrEmpty(rnName)) return rnName;
            if (stRn.TryGetValue("en", out var rnEn) && !string.IsNullOrEmpty(rnEn)) return rnEn;
        }

        // 1. Vanilla loca (current lang, then EN fallback)
        var name = Core.Services.VanillaLocaService.GetDisplayName(statId, lang);
        if (name != null) return name;
        if (lang != "en")
        {
            name = Core.Services.VanillaLocaService.GetDisplayName(statId, "en");
            if (name != null) return name;
        }

        if (resolver == null) return null;

        // 2. Resolver + LocaService (mod entries with own DisplayName handle)
        var fields = resolver.ResolveAll(statId);
        if (fields.TryGetValue("DisplayName", out var handle))
        {
            var resolved = locaSvc?.ResolveHandle(handle, lang)
                        ?? locaSvc?.ResolveHandle(handle, "en");
            if (resolved != null)
                return Core.Localization.BbCode.FromBg3Xml(resolved);
        }

        // 3. Walk using-chain for inherited entries
        var cur = statId;
        var allEntries = resolver.AllEntries;
        for (int d = 0; d < 20 && cur != null; d++)
        {
            if (!allEntries.TryGetValue(cur, out var entry) || entry.Using == null) break;
            cur = entry.Using;
            name = Core.Services.VanillaLocaService.GetDisplayName(cur, lang);
            if (name != null) return name;
            if (lang != "en")
            {
                name = Core.Services.VanillaLocaService.GetDisplayName(cur, "en");
                if (name != null) return name;
            }
        }
        return null;
    }

    private string? ResolveDisplayName(string statId)
    {
        var lang = Localization.Loc.Instance.Lang;
        return ResolveStatDisplayName(statId, lang,
            Resolver ?? BoostBlocksEditor.GlobalResolver,
            LocaService ?? BoostBlocksEditor.GlobalLocaService);
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

        // Build display entries: StatId → "LocalizedName (StatId)" for search
        var lang = Localization.Loc.Instance.Lang;
        var resolver = Resolver;
        var locaSvc = LocaService;
        var entries = items.Select(id =>
        {
            var displayName = ResolveStatDisplayName(id, lang, resolver, locaSvc);
            var label = displayName != null ? $"{displayName}  ({id})" : id;
            return (Id: id, Label: label);
        }).ToArray();

        // List
        var listBox = new ListBox
        {
            MaxHeight = 400,
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            ItemsSource = entries.Select(e => e.Label).ToArray(),
        };

        // Style list items
        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedIndex >= 0)
            {
                // Find original entry from filtered list
                var selectedLabel = listBox.SelectedItem as string;
                var match = entries.FirstOrDefault(e => e.Label == selectedLabel);
                Text = match.Id ?? selectedLabel;
                ClosePicker();
            }
        };

        // Search filter — match both localized name and StatId (debounced)
        DispatcherTimer? pickerDebounce = null;
        searchBox.TextChanged += (_, _) =>
        {
            pickerDebounce?.Stop();
            pickerDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            pickerDebounce.Tick += FilterTick;
            pickerDebounce.Start();

            void FilterTick(object? s, EventArgs ev)
            {
                pickerDebounce?.Stop();
                pickerDebounce!.Tick -= FilterTick;
                var query = searchBox.Text?.Trim() ?? "";
                if (string.IsNullOrEmpty(query))
                    listBox.ItemsSource = entries.Select(e => e.Label).ToArray();
                else
                    listBox.ItemsSource = entries
                        .Where(e => e.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                                 || e.Id.Contains(query, StringComparison.OrdinalIgnoreCase))
                        .Select(e => e.Label).ToArray();
            }
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
            MinWidth = 400, MaxWidth = 600, MaxHeight = 500,
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
