using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.Core.Schema;

namespace ParaTool.App.Controls;

/// <summary>
/// Renders a semicolon-separated boost/functor string as visual colored blocks.
/// Each block shows the human-readable label + editable parameters.
/// Changes are synced back to the bound Text property.
/// </summary>
public class BoostBlocksEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<BoostBlocksEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public static readonly StyledProperty<bool> IsFunctorModeProperty =
        AvaloniaProperty.Register<BoostBlocksEditor, bool>(nameof(IsFunctorMode));

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    public bool IsFunctorMode
    {
        get => GetValue(IsFunctorModeProperty);
        set => SetValue(IsFunctorModeProperty, value);
    }

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal };
    private bool _updating;

    private static readonly SolidColorBrush BgDefault = new(Color.Parse("#252330"));
    private static readonly SolidColorBrush FgMuted = new(Color.Parse("#8A8494"));
    private static readonly SolidColorBrush FgLight = new(Color.Parse("#E0DDE6"));
    private static readonly SolidColorBrush InputBg = new(Color.Parse("#1A1820"));

    public BoostBlocksEditor()
    {
        Content = _panel;
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextProperty && !_updating)
            Rebuild();
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        var raw = Text ?? "";
        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var part in parts)
        {
            var block = CreateBlock(part);
            if (block != null)
                _panel.Children.Add(block);
        }

        // Add button
        var addBtn = new Button
        {
            Content = "+",
            FontSize = 14, FontWeight = FontWeight.Bold,
            Padding = new Thickness(8, 4),
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Background = BgDefault,
            Foreground = new SolidColorBrush(Color.Parse("#6C5CE7")),
            BorderThickness = new Thickness(1),
            BorderBrush = new SolidColorBrush(Color.Parse("#6C5CE7")),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        addBtn.Click += OnAddClick;
        _panel.Children.Add(addBtn);
    }

    private Border? CreateBlock(string rawBoost)
    {
        var parsed = BoostMapping.ParseBoostCall(rawBoost);
        if (parsed == null) return CreateRawBlock(rawBoost);

        var (funcName, args) = parsed.Value;
        var defs = IsFunctorMode ? BoostMapping.Functors : BoostMapping.Boosts;
        var def = defs.FirstOrDefault(d => d.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));

        if (def == null)
            return CreateRawBlock(rawBoost);

        var color = Color.Parse(def.Color);
        var colorBrush = new SolidColorBrush(color);
        var bgBrush = new SolidColorBrush(color, 0.15);

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        // Label
        stack.Children.Add(new TextBlock
        {
            Text = def.Label,
            FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = colorBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });

        // Parameters
        for (int i = 0; i < def.Params.Length && i < args.Length; i++)
        {
            var param = def.Params[i];
            var value = args[i];
            var paramIdx = i;

            if (param.Type == "enum" && param.EnumValues != null)
            {
                var combo = new ComboBox
                {
                    ItemsSource = param.EnumValues,
                    SelectedItem = param.EnumValues.FirstOrDefault(v => v.Equals(value, StringComparison.OrdinalIgnoreCase)) ?? value,
                    FontSize = 11, Padding = new Thickness(4, 1),
                    MinWidth = 60,
                    Background = InputBg,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                combo.Tag = (rawBoost, paramIdx);
                combo.SelectionChanged += OnParamChanged;
                stack.Children.Add(combo);
            }
            else
            {
                var tb = new TextBox
                {
                    Text = value,
                    FontSize = 11, Padding = new Thickness(4, 2),
                    MinWidth = 30, MaxWidth = 80,
                    Background = InputBg,
                    Foreground = FgLight,
                    BorderThickness = new Thickness(0),
                    CornerRadius = new CornerRadius(3),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                tb.Tag = (rawBoost, paramIdx);
                tb.LostFocus += OnParamTextChanged;
                stack.Children.Add(tb);
            }
        }

        // Remove button
        var removeBtn = new Button
        {
            Content = "×", FontSize = 11,
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent,
            Foreground = FgMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        removeBtn.Tag = rawBoost;
        removeBtn.Click += OnRemoveClick;
        stack.Children.Add(removeBtn);

        return new Border
        {
            Child = stack,
            Background = bgBrush,
            BorderBrush = colorBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(2),
        };
    }

    private Border CreateRawBlock(string raw)
    {
        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        stack.Children.Add(new TextBlock
        {
            Text = raw,
            FontSize = 11,
            Foreground = FgMuted,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var removeBtn = new Button
        {
            Content = "×", FontSize = 11,
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent, Foreground = FgMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        removeBtn.Tag = raw;
        removeBtn.Click += OnRemoveClick;
        stack.Children.Add(removeBtn);

        return new Border
        {
            Child = stack,
            Background = BgDefault,
            BorderBrush = FgMuted,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            Padding = new Thickness(8, 4),
            Margin = new Thickness(2),
        };
    }

    private void OnRemoveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string rawBoost)
        {
            var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
            parts.Remove(rawBoost);
            SyncText(string.Join(";", parts));
        }
    }

    private void OnParamChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox combo && combo.Tag is (string rawBoost, int paramIdx))
        {
            var newValue = combo.SelectedItem?.ToString();
            if (newValue != null)
                UpdateParam(rawBoost, paramIdx, newValue);
        }
    }

    private void OnParamTextChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (sender is TextBox tb && tb.Tag is (string rawBoost, int paramIdx))
        {
            UpdateParam(rawBoost, paramIdx, tb.Text ?? "");
        }
    }

    private void UpdateParam(string rawBoost, int paramIdx, string newValue)
    {
        var parsed = BoostMapping.ParseBoostCall(rawBoost);
        if (parsed == null) return;

        var (funcName, args) = parsed.Value;
        if (paramIdx >= args.Length) return;
        args[paramIdx] = newValue;

        var newBoost = args.Length > 0 ? $"{funcName}({string.Join(",", args)})" : funcName;

        var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var idx = parts.IndexOf(rawBoost);
        if (idx >= 0)
            parts[idx] = newBoost;
        SyncText(string.Join(";", parts));
    }

    private void OnAddClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        // Open a simple popup to select which boost to add
        var defs = IsFunctorMode ? BoostMapping.Functors : BoostMapping.Boosts;
        var menu = new ContextMenu();
        foreach (var def in defs)
        {
            var item = new MenuItem { Header = def.Label, Tag = def };
            item.Click += (_, _) =>
            {
                var d = (BoostMapping.BlockDef)item.Tag!;
                var defaultArgs = d.Params.Select(p => p.Type switch
                {
                    "number" => "1",
                    "dice" => "1d6",
                    "enum" => p.EnumValues?.FirstOrDefault() ?? "",
                    _ => ""
                }).ToArray();
                var newBoost = defaultArgs.Length > 0 ? $"{d.FuncName}({string.Join(",", defaultArgs)})" : d.FuncName;
                var current = Text ?? "";
                SyncText(string.IsNullOrEmpty(current) ? newBoost : $"{current};{newBoost}");
            };
            menu.Items.Add(item);
        }
        menu.Open(this);
    }

    private void SyncText(string value)
    {
        _updating = true;
        Text = value;
        _updating = false;
        Rebuild();
    }
}
