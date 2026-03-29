
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using ParaTool.Core.Schema;
using ParaTool.App.Services;
using ParaTool.App.Themes;
using ParaTool.App.Localization;

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

    public static readonly StyledProperty<string[]?> StatusListProperty =
        AvaloniaProperty.Register<BoostBlocksEditor, string[]?>(nameof(StatusList));
    public static readonly StyledProperty<string[]?> SpellListProperty =
        AvaloniaProperty.Register<BoostBlocksEditor, string[]?>(nameof(SpellList));

    public string[]? StatusList { get => GetValue(StatusListProperty); set => SetValue(StatusListProperty, value); }
    public string[]? SpellList { get => GetValue(SpellListProperty); set => SetValue(SpellListProperty, value); }

    public bool IsFunctorMode
    {
        get => GetValue(IsFunctorModeProperty);
        set => SetValue(IsFunctorModeProperty, value);
    }

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal, ClipToBounds = false };
    private bool _updating;

    private static string GetBlockLabel(ParaTool.Core.Schema.BoostMapping.BlockDef def)
    {
        var locaKey = $"boost.{def.FuncName}";
        var locaVal = Localization.Loc.Instance[locaKey];
        if (locaVal != locaKey) return locaVal;
        return Localization.Loc.Instance.Lang == "ru" ? def.LabelRu : def.Label;
    }

    private static SolidColorBrush BgDefault => Themes.ThemeBrushes.InputBg;
    private static SolidColorBrush FgMuted => Themes.ThemeBrushes.TextMuted;
    private static SolidColorBrush FgLight => Themes.ThemeBrushes.TextPrimary;
    private static SolidColorBrush InputBg => Themes.ThemeBrushes.CardBg;

    public BoostBlocksEditor()
    {
        Content = _panel;
        ClipToBounds = false;
        PropertyChanged += OnPropertyChanged;
        Localization.Loc.Instance.PropertyChanged += (_, _) => { if (!_updating) Rebuild(); };
        FontScale.ScaleChanged += () => { if (!_updating) Rebuild(); };
        // Auto-populate Status/Spell lists from ConstructorViewModel when attached
        AttachedToVisualTree += (_, _) => TryLoadPickerLists();
    }

    /// <summary>Global status/spell/passive lists, set once by ConstructorViewModel.</summary>
    public static string[]? GlobalStatusList { get; set; }
    public static string[]? GlobalSpellList { get; set; }
    public static string[]? GlobalPassiveList { get; set; }

    private void TryLoadPickerLists()
    {
        StatusList ??= GlobalStatusList;
        SpellList ??= GlobalSpellList;
        if (StatusList != null && SpellList != null && GlobalPassiveList != null) return;
        // Walk up visual tree to find ConstructorViewModel
        Control? parent = this;
        while (parent != null)
        {
            if (parent.DataContext is ViewModels.ConstructorViewModel cvm)
            {
                StatusList ??= [..ConditionSchema.StatusGroups, ..cvm.AllStatuses];
                SpellList ??= cvm.AllSpells;
                GlobalStatusList ??= StatusList;
                GlobalSpellList ??= SpellList;
                GlobalPassiveList ??= cvm.AllPassives;
                return;
            }
            parent = parent.GetVisualParent() as Control;
        }
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
            FontSize = FontScale.Of(14), FontWeight = FontWeight.Bold,
            Padding = new Thickness(8, 4),
            Margin = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Background = BgDefault,
            Foreground = ThemeBrushes.Accent,
            BorderThickness = new Thickness(1),
            BorderBrush = ThemeBrushes.Accent,
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

        // Detect target context prefix (SELF, SWAP, OBSERVER_TARGET, OBSERVER_SOURCE)
        string? targetCtx = null;
        if (args.Length > 0 && TargetContextValues.Contains(args[0].Trim(), StringComparer.OrdinalIgnoreCase))
        {
            targetCtx = args[0].Trim();
            args = args[1..]; // shift args past the target context
        }

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };

        // Label
        stack.Children.Add(new TextBlock
        {
            Text = GetBlockLabel(def),
            FontSize = FontScale.Of(11), FontWeight = FontWeight.SemiBold,
            Foreground = colorBrush,
            VerticalAlignment = VerticalAlignment.Center,
        });

        // Target context tumbler (only shown if data already contains one)
        if (targetCtx != null)
        {
            var ctxItems = new[] { "—" }.Concat(TargetContextValues).ToArray();
            var ctxChip = new TumblerChipEditor
            {
                Text = targetCtx ?? "—",
                Items = ctxItems,
                VerticalAlignment = VerticalAlignment.Center,
            };
            ctxChip.Tag = (rawBoost, -1); // -1 = target context slot
            ctxChip.PropertyChanged += (s, e2) =>
            {
                if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int _) && !_updating)
                    UpdateTargetContext(rb, tc.Text);
            };
            stack.Children.Add(ctxChip);
        }

        // Parameters — render as appropriate controls (show all params, even if args is shorter)
        for (int i = 0; i < def.Params.Length; i++)
        {
            var param = def.Params[i];
            var isOptional = i >= args.Length;
            var value = !isOptional ? args[i] : "";
            var paramIdx = i;

            if (param.Type == "hidden")
            {
                // Invisible constant — don't render, keep value as-is
                continue;
            }

            // RollBonus: 3rd param (AbilityOrSkill) only needed for SavingThrow/SkillCheck/RawAbility
            // Always show as optional for these, skip for attack types
            if (def.FuncName == "RollBonus" && i == 2 && args.Length > 0
                && args[0].Trim() is not ("SavingThrow" or "SkillCheck" or "RawAbility"))
                continue;
            else if (param.Type == "int")
            {
                // Integer tumbler chip (allow -1 for infinite duration etc.)
                var chip = new TumblerChipEditor
                {
                    Text = value,
                    Step = 1,
                    MinValue = -1,
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
            else if (param.Type == "flags" && param.EnumValues != null)
            {
                var lang = Localization.Loc.Instance.Lang;
                var picker = new ChecklistPickerChip
                {
                    Text = value,
                    Options = param.EnumValues,
                    Labels = Localization.Loc.Instance.GetEnumDisplayLabels(param.EnumValues),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                picker.Tag = (rawBoost, paramIdx);
                picker.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is ChecklistPickerChip cp && cp.Tag is (string rb, int pi) && !_updating)
                        UpdateParam(rb, pi, cp.Text ?? "");
                };
                stack.Children.Add(picker);
            }
            else if (param.Type == "enum" && param.EnumValues != null)
            {
                // Optional params get "—" (none) option at the start
                var lang = Localization.Loc.Instance.Lang;
                var items = isOptional || string.IsNullOrEmpty(value)
                    ? new[] { "—" }.Concat(param.EnumValues).ToArray()
                    : param.EnumValues;
                var displayItems = isOptional || string.IsNullOrEmpty(value)
                    ? new[] { "—" }.Concat(Localization.Loc.Instance.GetEnumDisplayLabels(param.EnumValues)).ToArray()
                    : Localization.Loc.Instance.GetEnumDisplayLabels(param.EnumValues);
                var chip = new TumblerChipEditor
                {
                    Text = string.IsNullOrEmpty(value) ? "—" : value,
                    Items = items,
                    DisplayItems = displayItems,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                var capturedDef = def;
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                    {
                        UpdateParam(rb, pi, tc.Text ?? "");
                        // Changing first param may affect visibility of later params → deferred rebuild
                        if (pi == 0 && capturedDef.FuncName == "RollBonus")
                            Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
                    }
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
                var chip = new TumblerChipEditor
                {
                    Text = value, Items = BoostMapping.FormulaValues,
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
                var items = BoostMapping.FormulaValues.Contains(value)
                    ? BoostMapping.FormulaValues
                    : BoostMapping.FormulaValues.Append(value).ToArray();
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
                // String params: use SearchPickerChip for Status/Spell if list available
                var isStatus = param.Name.Contains("Status", StringComparison.OrdinalIgnoreCase);
                var isSpell = param.Name.Contains("Spell", StringComparison.OrdinalIgnoreCase)
                              || param.Name.Contains("Resource", StringComparison.OrdinalIgnoreCase);
                var isPassive = param.Name.Contains("Passive", StringComparison.OrdinalIgnoreCase);
                var pickerItems = isStatus ? (StatusList ?? GlobalStatusList)
                    : isSpell ? (SpellList ?? GlobalSpellList)
                    : isPassive ? GlobalPassiveList
                    : null;

                if (pickerItems is { Length: > 0 })
                {
                    var picker = new SearchPickerChip
                    {
                        Text = value,
                        Items = pickerItems,
                        Watermark = isStatus ? "Search status..." : "Search...",
                        VerticalAlignment = VerticalAlignment.Center,
                    };
                    picker.Tag = (rawBoost, paramIdx);
                    picker.PropertyChanged += (s, e2) =>
                    {
                        if (e2.Property.Name == "Text" && s is SearchPickerChip sp && sp.Tag is (string rb, int pi) && !_updating)
                            UpdateParam(rb, pi, sp.Text ?? "");
                    };
                    stack.Children.Add(picker);
                }
                else
                {
                    var tb = new TextBox
                    {
                        Text = value, FontSize = FontScale.Of(11),
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
        }

        // Remove button
        var removeBtn = new Button
        {
            Content = "×", FontSize = FontScale.Of(11),
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

    /// <summary>Render IF(condition):effect as a container with live sub-editors.</summary>
    private Border CreateIfBlock(string rawBoost, string content)
    {
        var ifColor = Color.Parse("#F1C40F");
        var ifBrush = new SolidColorBrush(ifColor);

        // Split condition:effect — find matching ) then :
        string condition, effect;
        int depth = 0, splitAt = -1;
        for (int ci = 0; ci < content.Length; ci++)
        {
            if (content[ci] == '(') depth++;
            else if (content[ci] == ')') depth--;
            if (depth < 0 || (depth == 0 && content[ci] == ')'))
            {
                // Check for ): pattern
                if (ci + 1 < content.Length && content[ci + 1] == ':')
                    splitAt = ci;
                break;
            }
        }

        if (splitAt >= 0)
        {
            condition = content[1..splitAt]; // inside outer (...)
            effect = content[(splitAt + 2)..];
        }
        else
        {
            condition = content.Length > 1 ? content[1..].TrimEnd(')') : content;
            effect = "";
        }

        var outer = new StackPanel { Spacing = 4, ClipToBounds = false };

        // Row 1: IF label + × button
        var headerRow = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        headerRow.Children.Add(new TextBlock
        {
            Text = "IF", FontSize = FontScale.Of(12), FontWeight = FontWeight.Bold,
            Foreground = ifBrush, VerticalAlignment = VerticalAlignment.Center,
        });
        var removeBtn = new Button
        {
            Content = "×", FontSize = FontScale.Of(10),
            Padding = new Thickness(4, 0),
            Background = Brushes.Transparent, Foreground = FgMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        removeBtn.Tag = rawBoost;
        removeBtn.Click += OnRemoveClick;
        headerRow.Children.Add(removeBtn);
        outer.Children.Add(headerRow);

        // Mutable state for cross-editor updates
        var currentCond = condition;
        var currentEffect = effect;

        // Row 2: Embedded condition editor (live chips)
        var condEditor = new ConditionBlocksEditor
        {
            Text = condition,
            Margin = new Thickness(12, 0, 0, 0),
            ClipToBounds = false,
        };
        condEditor.PropertyChanged += (s, e2) =>
        {
            if (e2.Property.Name == "Text" && s is ConditionBlocksEditor ce && !_updating)
            {
                currentCond = ce.Text ?? "";
                UpdateIfBlock(rawBoost, currentCond, currentEffect);
            }
        };
        outer.Children.Add(condEditor);

        // Row 3: "THEN" label
        outer.Children.Add(new TextBlock
        {
            Text = Loc.Instance["LblThen"], FontSize = FontScale.Of(10), FontWeight = FontWeight.SemiBold,
            Foreground = new SolidColorBrush(ifColor, 0.6),
            Margin = new Thickness(12, 2, 0, 0),
        });

        // Row 4: Embedded functor editor (live chip blocks)
        var capturedEffect = effect;
        var functorEditor = new BoostBlocksEditor
        {
            IsFunctorMode = true,
            Margin = new Thickness(12, 0, 0, 0),
            ClipToBounds = false,
        };
        // Set Text after IsFunctorMode to ensure correct parsing
        functorEditor.Text = capturedEffect;
        functorEditor.PropertyChanged += (s, e2) =>
        {
            if (e2.Property.Name == "Text" && s is BoostBlocksEditor be && !_updating)
            {
                currentEffect = be.Text ?? "";
                UpdateIfBlock(rawBoost, currentCond, currentEffect);
            }
        };
        outer.Children.Add(functorEditor);

        return new Border
        {
            Child = outer,
            Background = new SolidColorBrush(ifColor, 0.08),
            BorderBrush = ifBrush,
            BorderThickness = new Thickness(2, 0, 0, 0),
            CornerRadius = new CornerRadius(6),
            Padding = new Thickness(8, 6), Margin = new Thickness(2),
            ClipToBounds = false,
        };
    }

    private void UpdateIfBlock(string oldRawBoost, string newCondition, string newEffect)
    {
        if (_updating) return;
        var newIf = $"IF({newCondition}):{newEffect}";
        var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var idx = parts.IndexOf(oldRawBoost);
        if (idx >= 0)
            parts[idx] = newIf;
        SyncText(string.Join(";", parts));
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
            FontSize = FontScale.Of(11),
            Foreground = FgMuted,
            VerticalAlignment = VerticalAlignment.Center,
        });
        var removeBtn = new Button
        {
            Content = "×", FontSize = FontScale.Of(11),
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

        // Account for target context prefix (SELF, SWAP, etc.) shifting indices
        int offset = 0;
        if (args.Length > 0 && TargetContextValues.Contains(args[0].Trim(), StringComparer.OrdinalIgnoreCase))
            offset = 1;

        var actualIdx = paramIdx + offset;

        // Extend args array if param index is beyond current length
        if (actualIdx >= args.Length)
        {
            var extended = new string[actualIdx + 1];
            args.CopyTo(extended, 0);
            for (int ai = args.Length; ai < extended.Length; ai++) extended[ai] = "";
            args = extended;
        }
        args[actualIdx] = newValue == "—" ? "" : newValue;

        // Trim trailing empty args
        var trimmedArgs = args.AsEnumerable().ToList();
        while (trimmedArgs.Count > 0 && string.IsNullOrEmpty(trimmedArgs[^1]))
            trimmedArgs.RemoveAt(trimmedArgs.Count - 1);

        var newBoost = trimmedArgs.Count > 0 ? $"{funcName}({string.Join(",", trimmedArgs)})" : funcName;

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
            var item = new MenuItem { Header = GetBlockLabel(def), Tag = def };
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
                    "flags" => p.EnumValues?.FirstOrDefault() ?? "None",
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

    private static readonly string[] TargetContextValues = ["SELF", "SWAP", "OBSERVER_TARGET", "OBSERVER_SOURCE"];

    private void UpdateTargetContext(string rawBoost, string? newCtx)
    {
        if (_updating) return;
        var parsed = BoostMapping.ParseBoostCall(rawBoost);
        if (parsed == null) return;

        var (funcName, args) = parsed.Value;

        // Strip existing target context if present
        if (args.Length > 0 && TargetContextValues.Contains(args[0].Trim(), StringComparer.OrdinalIgnoreCase))
            args = args[1..];

        // Prepend new context if not "—"
        var finalArgs = (newCtx != null && newCtx != "—")
            ? new[] { newCtx }.Concat(args).ToArray()
            : args;

        // Trim trailing empty
        var trimmed = finalArgs.ToList();
        while (trimmed.Count > 0 && string.IsNullOrEmpty(trimmed[^1]))
            trimmed.RemoveAt(trimmed.Count - 1);

        var newBoost = trimmed.Count > 0 ? $"{funcName}({string.Join(",", trimmed)})" : funcName;

        var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var idx = parts.IndexOf(rawBoost);
        if (idx >= 0) parts[idx] = newBoost;
        SyncText(string.Join(";", parts));
    }

    private void SyncText(string value)
    {
        _updating = true;
        Text = value;
        _updating = false;
        Rebuild();
    }

    private static string DefaultForParam(BoostMapping.ParamDef p) => p.Type switch
    {
        "enum" => p.EnumValues?.FirstOrDefault() ?? "",
        "flags" => p.EnumValues?.FirstOrDefault() ?? "",
        "bool" => "true",
        "number" or "int" => "0",
        "float" => "0",
        "formula" => "1",
        "dice" => "1d4",
        _ => "",
    };
}
