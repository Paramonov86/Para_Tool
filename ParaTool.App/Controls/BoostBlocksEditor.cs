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

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal, ClipToBounds = false };
    private bool _updating;

    private static SolidColorBrush BgDefault => Themes.ThemeBrushes.InputBg;
    private static SolidColorBrush FgMuted => Themes.ThemeBrushes.TextMuted;
    private static SolidColorBrush FgLight => Themes.ThemeBrushes.TextPrimary;
    private static SolidColorBrush InputBg => Themes.ThemeBrushes.CardBg;

    public BoostBlocksEditor()
    {
        Content = _panel;
        ClipToBounds = false;
        PropertyChanged += OnPropertyChanged;
    }

    private void OnPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == TextProperty && !_updating)
            Rebuild();
    }

    private void Rebuild()
    {
        _updating = true;
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
        _updating = false;
    }

    private Control? CreateBlock(string rawBoost)
    {
        var parsed = BoostMapping.ParseBoostCall(rawBoost);
        if (parsed == null) return CreateRawBlock(rawBoost);

        var (funcName, args) = parsed.Value;

        // Special: IF(...):Effect → yellow container block
        if (funcName.Equals("IF", StringComparison.OrdinalIgnoreCase) && args.Length > 0)
            return CreateIfBlock(rawBoost, args[0]);

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

        // Parameters — render as appropriate controls
        for (int i = 0; i < def.Params.Length && i < args.Length; i++)
        {
            var param = def.Params[i];
            var value = args[i];
            var paramIdx = i;

            if (param.Type == "hidden")
            {
                // Invisible constant — don't render, keep value as-is
                continue;
            }
            else if (param.Type == "int")
            {
                // Integer tumbler chip
                var chip = new TumblerChipEditor
                {
                    Text = value,
                    Step = 1,
                    MinValue = 0,
                    MaxValue = 999,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else if (param.Type == "enum" && param.EnumValues != null)
            {
                var chip = new TumblerChipEditor
                {
                    Text = value, Items = param.EnumValues,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else if (param.Type is "number" or "float")
            {
                var chip = new TumblerChipEditor
                {
                    Text = value,
                    Step = param.Type == "float" ? 0.1 : 1,
                    MinValue = 0, MaxValue = 999,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else if (param.Type == "dice")
            {
                var diceOptions = new[] { "1d4", "1d6", "1d8", "1d10", "1d12", "2d4", "2d6", "2d8", "2d10", "2d12", "3d6", "3d8", "4d6", "5d6", "6d6", "8d6", "10d6" };
                var chip = new TumblerChipEditor
                {
                    Text = value, Items = diceOptions,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else if (param.Type == "bool")
            {
                var chip = new TumblerChipEditor
                {
                    Text = value, Items = ["true", "false"],
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else if (param.Type == "formula")
            {
                var presets = new[] { "1", "2", "3", "4", "5", "1d4", "1d6", "1d8", "1d10", "1d12", "2d6", "2d8", "ProficiencyBonus", "Level" };
                // Add current value if not in presets
                var items = presets.Contains(value) ? presets : presets.Append(value).ToArray();
                var chip = new TumblerChipEditor
                {
                    Text = value, Items = items,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else
            {
                // String/guid — editable TextBox
                var tb = new TextBox
                {
                    Text = value, FontSize = 11,
                    Padding = new Thickness(4, 1), MinWidth = 60,
                    Background = InputBg, CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                tb.Tag = (rawBoost, paramIdx);
                tb.LostFocus += (s, _) =>
                {
                    if (s is TextBox t && t.Tag is (string rb2, int pi2))
                        UpdateParam(rb2, pi2, t.Text ?? "");
                };
                stack.Children.Add(tb);
            }
        }

        // Remove button
        var removeBtn = new Button
        {
            Content = "×", FontSize = 11,
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent, Foreground = FgMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        removeBtn.Tag = rawBoost;
        removeBtn.Click += OnRemoveClick;
        stack.Children.Add(removeBtn);

        // Use Panel so CornerRadius border doesn't clip tumbler drum overflow
        var bgBorder = new Border
        {
            Background = bgBrush, BorderBrush = colorBrush,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(8),
            IsHitTestVisible = false,
        };
        var wrapper = new Panel
        {
            Margin = new Thickness(2),
            ClipToBounds = false,
            Children = { bgBorder, new Border { Child = stack, Padding = new Thickness(8, 4), ClipToBounds = false } },
        };
        return wrapper;
    }

    /// <summary>Render IF(condition):effect as a yellow container block.</summary>
    private Border CreateIfBlock(string rawBoost, string content)
    {
        var ifColor = Color.Parse("#F1C40F");
        var ifBrush = new SolidColorBrush(ifColor);

        // Split condition:effect
        var colonIdx = content.IndexOf(')');
        string condition, effect;
        if (colonIdx >= 0 && colonIdx + 1 < content.Length && content[colonIdx + 1] == ':')
        {
            condition = content[1..colonIdx]; // inside (...)
            effect = content[(colonIdx + 2)..];
        }
        else
        {
            condition = content;
            effect = "";
        }

        var outer = new StackPanel { Spacing = 4 };

        // IF label + condition
        var condRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        condRow.Children.Add(new TextBlock
        {
            Text = "IF", FontSize = 11, FontWeight = FontWeight.Bold,
            Foreground = ifBrush, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
        });
        condRow.Children.Add(new Border
        {
            Child = new TextBlock
            {
                Text = SimplifyCondition(condition),
                FontSize = 10, Foreground = FgLight,
            },
            Background = new SolidColorBrush(ifColor, 0.1),
            CornerRadius = new CornerRadius(4),
            Padding = new Thickness(6, 2),
        });
        outer.Children.Add(condRow);

        // Effect as nested block
        if (!string.IsNullOrEmpty(effect))
        {
            var effectBlock = CreateBlock(effect);
            if (effectBlock != null)
            {
                effectBlock.Margin = new Thickness(16, 0, 0, 0);
                outer.Children.Add(effectBlock);
            }
        }

        // Remove
        var removeBtn = new Button
        {
            Content = "×", FontSize = 10,
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent, Foreground = FgMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
        };
        removeBtn.Tag = rawBoost;
        removeBtn.Click += OnRemoveClick;
        outer.Children.Add(removeBtn);

        return new Border
        {
            Child = outer,
            Background = new SolidColorBrush(ifColor, 0.08),
            BorderBrush = ifBrush,
            BorderThickness = new Thickness(2, 0, 0, 0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6), Margin = new Thickness(2),
        };
    }

    /// <summary>Simplify condition text for display.</summary>
    private static string SimplifyCondition(string cond)
    {
        // Replace common patterns with human text
        return cond
            .Replace("Tagged('PLAYABLE',context.Source)", "Is Playable")
            .Replace("context.Source", "source")
            .Replace("context.Target", "target")
            .Replace("Enemy()", "Is Enemy")
            .Replace("Ally()", "Is Ally")
            .Replace("not ", "NOT ")
            .Replace(" and ", " AND ")
            .Replace(" or ", " OR ");
    }

    /// <summary>Simplify a parameter value for display.</summary>
    private static string SimplifyName(string funcName, string value)
    {
        // For UnlockSpell: strip prefixes
        if (funcName.Equals("UnlockSpell", StringComparison.OrdinalIgnoreCase))
        {
            return value
                .Replace("Target_", "")
                .Replace("Projectile_", "")
                .Replace("Shout_", "")
                .Replace("Zone_", "")
                .Replace("Throw_", "");
        }
        return value;
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
        if (_updating) return; // prevent re-entrant updates during Rebuild
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

        if (IsFunctorMode)
        {
            var ifItem = new MenuItem { Header = "⚡ Conditional (IF)", FontWeight = FontWeight.Bold };
            ifItem.Click += (_, _) =>
            {
                var current = Text ?? "";
                var newIf = "IF(Enemy()):ApplyStatus(YOURSTATUS,100,1)";
                SyncText(string.IsNullOrEmpty(current) ? newIf : $"{current};{newIf}");
            };
            menu.Items.Add(ifItem);
            menu.Items.Add(new Separator());
        }

        foreach (var def in defs)
        {
            var item = new MenuItem { Header = def.Label, Tag = def };
            item.Click += (_, _) =>
            {
                var d = (BoostMapping.BlockDef)item.Tag!;
                var defaultArgs = d.Params.Select(p => p.Type switch
                {
                    "hidden" => "100",
                    "int" => "1",
                    "number" => "1",
                    "float" => "1",
                    "dice" => "1d6",
                    "formula" => "1",
                    "bool" => "true",
                    "enum" => p.EnumValues?.FirstOrDefault() ?? "None",
                    "string" => p.Name.Contains("Status") ? "YOURSTATUS" :
                                p.Name.Contains("Spell") ? "YourSpell" :
                                p.Name.Contains("Resource") ? "ActionPoint" : "Value",
                    _ => "0"
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
