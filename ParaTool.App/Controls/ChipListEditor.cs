
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Themes;
using ParaTool.App.Services;
using ParaTool.App.Localization;

namespace ParaTool.App.Controls;

/// <summary>
/// Semicolon-separated string displayed as removable colored chips.
/// If SearchItems is set, "+" opens a SearchPickerChip popup instead of text input.
/// </summary>
public class ChipListEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ChipListEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<string> ChipColorProperty =
        AvaloniaProperty.Register<ChipListEditor, string>(nameof(ChipColor), "#E67E22");

    public static readonly StyledProperty<string[]?> SearchItemsProperty =
        AvaloniaProperty.Register<ChipListEditor, string[]?>(nameof(SearchItems));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public string ChipColor
    {
        get => GetValue(ChipColorProperty);
        set => SetValue(ChipColorProperty, value);
    }

    /// <summary>If set, "+" opens a search picker instead of text input.</summary>
    public string[]? SearchItems
    {
        get => GetValue(SearchItemsProperty);
        set => SetValue(SearchItemsProperty, value);
    }

    /// <summary>Fired when user requests rename via context menu. Args: (statId, chipEditor).</summary>
    public event Action<string>? RenameRequested;

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal };
    private readonly TextBox _input;
    private readonly Button _addBtn;
    private bool _updating;

    public ChipListEditor()
    {
        _input = new TextBox
        {
            FontSize = FontScale.Of(12),
            Padding = new Thickness(6, 4),
            MinWidth = 80,
            Watermark = Loc.Instance.WmTypeAndEnter,
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Thickness(0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _input.KeyUp += OnInputKeyUp;

        _addBtn = new Button
        {
            Content = "+",
            FontSize = FontScale.Of(14),
            FontWeight = FontWeight.Bold,
            Padding = new Thickness(6, 0),
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = new SolidColorBrush(Color.Parse("#E06040")),
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        _addBtn.Click += OnAddClick;

        _panel.Children.Add(_input);
        _panel.Children.Add(_addBtn);

        Content = new Border
        {
            Child = _panel,
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(4, 2),
        };

        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty && !_updating)
                Rebuild();
            if (e.Property == SearchItemsProperty)
                UpdateInputVisibility();
        };
        Action scaleHandler = () =>
        {
            _input.FontSize = FontScale.Of(12);
            _addBtn.FontSize = FontScale.Of(14);
            if (!_updating) Rebuild();
        };
        FontScale.ScaleChanged += scaleHandler;
        bool resolverWasNull = true;
        AttachedToVisualTree += (_, _) =>
        {
            if (BoostBlocksEditor.GlobalResolver != null && resolverWasNull)
            {
                resolverWasNull = false;
                if (!_updating) Rebuild();
            }
        };
        BoostBlocksEditor.GlobalResolverReady += () =>
        {
            resolverWasNull = false;
            Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (!_updating) Rebuild(); });
        };
        DetachedFromVisualTree += (_, _) => FontScale.ScaleChanged -= scaleHandler;

        UpdateInputVisibility();
    }

    private void UpdateInputVisibility()
    {
        // If search items available, hide text input — use "+" button with picker
        _input.IsVisible = SearchItems == null || SearchItems.Length == 0;
    }

    private void OnAddClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var items = SearchItems;
        if (items is { Length: > 0 })
        {
            OpenSearchPicker(null);
        }
        else
        {
            // Fallback: add from text input
            AddFromInput();
        }
    }

    private void OpenSearchPicker(string? replaceValue)
    {
        var items = SearchItems;
        if (items == null || items.Length == 0) return;

        var picker = new SearchPickerChip
        {
            Text = replaceValue ?? "",
            Items = items,
            Watermark = Loc.Instance.WmSearch,
            VerticalAlignment = VerticalAlignment.Center,
        };
        picker.PropertyChanged += (s, ev) =>
        {
            if (ev.Property.Name == "Text" && s is SearchPickerChip sp && !string.IsNullOrEmpty(sp.Text))
            {
                if (replaceValue != null)
                {
                    var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
                    var idx = parts.IndexOf(replaceValue);
                    if (idx >= 0) parts[idx] = sp.Text;
                    else parts.Add(sp.Text);
                    _updating = true;
                    Text = string.Join(";", parts);
                    _updating = false;
                }
                else
                {
                    var current = Text ?? "";
                    _updating = true;
                    Text = string.IsNullOrEmpty(current) ? sp.Text : $"{current};{sp.Text}";
                    _updating = false;
                }
                Rebuild();
            }
        };

        // Add picker to panel so it has a visual parent, then open
        _panel.Children.Insert(_panel.Children.Count - 1, picker);
        Avalonia.Threading.Dispatcher.UIThread.Post(() => picker.OpenPicker(),
            Avalonia.Threading.DispatcherPriority.Loaded);
    }

    private void OnInputKeyUp(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            AddFromInput();
            e.Handled = true;
            return;
        }
        // Check if text ends with ';' (works with any keyboard layout)
        var text = _input.Text;
        if (text != null && text.EndsWith(';'))
        {
            _input.Text = text.TrimEnd(';');
            AddFromInput();
        }
    }

    private void AddFromInput()
    {
        var text = _input.Text?.Trim().TrimEnd(';');
        if (!string.IsNullOrEmpty(text))
        {
            var current = Text ?? "";
            _updating = true;
            Text = string.IsNullOrEmpty(current) ? text : $"{current};{text}";
            _updating = false;
            _input.Text = "";
            Rebuild();
        }
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        var raw = Text ?? "";
        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var chipColor = Color.Parse(ChipColor);

        foreach (var part in parts)
        {
            var chip = CreateChip(part, chipColor);
            _panel.Children.Add(chip);
        }

        if (_input.IsVisible)
            _panel.Children.Add(_input);
        _panel.Children.Add(_addBtn);
    }

    private Border CreateChip(string value, Color color)
    {
        var colorBrush = new SolidColorBrush(color);
        var lang = Localization.Loc.Instance.Lang;
        var displayName = SearchPickerChip.ResolveStatDisplayName(value, lang,
            BoostBlocksEditor.GlobalResolver, BoostBlocksEditor.GlobalLocaService);

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        var textBlock = new TextBlock
        {
            Text = displayName ?? value,
            FontSize = FontScale.Of(11), FontWeight = FontWeight.SemiBold,
            Foreground = colorBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        if (displayName != null)
            ToolTip.SetTip(textBlock, value);

        stack.Children.Add(textBlock);

        var removeBtn = new Button
        {
            Content = "×", FontSize = FontScale.Of(10),
            Padding = new Thickness(3, 0),
            Background = Avalonia.Media.Brushes.Transparent,
            Foreground = ThemeBrushes.TextMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        removeBtn.Click += (_, _) =>
        {
            var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            parts.Remove(value);
            _updating = true;
            Text = string.Join(";", parts);
            _updating = false;
            Rebuild();
        };
        stack.Children.Add(removeBtn);

        var border = new Border
        {
            Child = stack,
            Background = new SolidColorBrush(color, 0.15),
            BorderBrush = colorBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 3),
            Margin = new Thickness(2),
        };

        // Context menu: Rename + Replace
        var renameItem = new MenuItem { Header = Localization.Loc.Instance.CtxRename };
        renameItem.Click += (_, _) => RenameRequested?.Invoke(value);
        var ctxMenu = new ContextMenu();
        ctxMenu.Items.Add(renameItem);
        if (SearchItems is { Length: > 0 })
        {
            var replaceItem = new MenuItem { Header = Localization.Loc.Instance.CtxReplace };
            replaceItem.Click += (_, _) => OpenSearchPicker(value);
            ctxMenu.Items.Add(replaceItem);
        }
        textBlock.ContextMenu = ctxMenu;

        return border;
    }
}
