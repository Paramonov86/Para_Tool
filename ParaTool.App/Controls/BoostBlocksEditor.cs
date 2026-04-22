
using System.ComponentModel;
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

public enum BoostEditorContext
{
    ItemBoosts,
    WeaponDefaultBoosts,
    Functors,
}

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

    public static readonly StyledProperty<BoostEditorContext> EditorContextProperty =
        AvaloniaProperty.Register<BoostBlocksEditor, BoostEditorContext>(
            nameof(EditorContext), BoostEditorContext.ItemBoosts);

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
        get => EditorContext == BoostEditorContext.Functors || GetValue(IsFunctorModeProperty);
        set
        {
            SetValue(IsFunctorModeProperty, value);
            if (value) EditorContext = BoostEditorContext.Functors;
        }
    }

    public BoostEditorContext EditorContext
    {
        get => GetValue(EditorContextProperty);
        set => SetValue(EditorContextProperty, value);
    }

    /// <summary>Fired when user requests rename of a spell/status via context menu.</summary>
    public event Action<string>? RenameRequested;

    /// <summary>Global event for rename requests from any BoostBlocksEditor instance.</summary>
    public static event Action<string>? GlobalRenameRequested;

    /// <summary>Force all live BoostBlocksEditor instances to rebuild their chips.</summary>
    public static event Action? GlobalForceRebuild;

    public static void InvokeGlobalForceRebuild() => GlobalForceRebuild?.Invoke();

    private void OnGlobalForceRebuild()
    {
        if (!_updating && !_rebuilding && !string.IsNullOrEmpty(Text))
            Rebuild();
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

    private readonly PropertyChangedEventHandler _locHandler;
    private readonly Action _scaleHandler;

    public BoostBlocksEditor()
    {
        Content = _panel;
        ClipToBounds = false;
        PropertyChanged += OnPropertyChanged;
        _locHandler = (_, _) => { if (!_updating && IsLoaded) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (IsLoaded) Rebuild(); }); };
        _scaleHandler = () => { if (!_updating && IsLoaded) Rebuild(); };
        Localization.Loc.Instance.PropertyChanged += _locHandler;
        FontScale.ScaleChanged += _scaleHandler;
        GlobalForceRebuild += OnGlobalForceRebuild;
        // Auto-populate Status/Spell lists from ConstructorViewModel when attached
        AttachedToVisualTree += (_, _) => { TryLoadPickerLists(); if (!_updating && !_rebuilding && !string.IsNullOrEmpty(Text)) Rebuild(); };
    }

    /// <summary>Global status/spell/passive lists, set once by ConstructorViewModel.</summary>
    public static string[]? GlobalStatusList { get; set; }
    public static string[]? GlobalSpellList { get; set; }
    public static string[]? GlobalPassiveList { get; set; }
    /// <summary>Active spell/status renames from current artifact.</summary>
    public static Dictionary<string, Dictionary<string, string>>? ActiveSpellRenames { get; set; }
    public static Dictionary<string, Dictionary<string, string>>? ActiveStatusRenames { get; set; }
    /// <summary>Current editing language from constructor (for chip display).</summary>
    public static string? ActiveEditingLang { get; set; }
    /// <summary>Global resolver + loca for resolving AMP/mod display names in pickers.</summary>
    private static Core.Parsing.StatsResolver? _globalResolver;
    public static Core.Parsing.StatsResolver? GlobalResolver
    {
        get => _globalResolver;
        set { _globalResolver = value; if (value != null) GlobalResolverReady?.Invoke(); }
    }
    public static Core.Services.LocaService? GlobalLocaService { get; set; }
    /// <summary>Fired once when GlobalResolver becomes available.</summary>
    public static event Action? GlobalResolverReady;

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

    private bool _rebuilding;
    private void Rebuild()
    {
        if (_rebuilding) return;
        _rebuilding = true;
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

        // Sort button — only shown when there are 2+ chips, orders them by category
        // then alphabetically by label. Preserves IF() blocks by leaving them where
        // they are (they're control-flow, not data effects).
        if (!string.IsNullOrEmpty(Text))
        {
            var sortableParts = Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (sortableParts.Length >= 2)
            {
                var sortBtn = new Button
                {
                    Content = "↕",
                    FontSize = FontScale.Of(12), FontWeight = FontWeight.Bold,
                    Padding = new Thickness(8, 4),
                    Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(10),
                    Background = BgDefault,
                    Foreground = ThemeBrushes.TextSecondary,
                    BorderThickness = new Thickness(1),
                    BorderBrush = ThemeBrushes.BorderSubtle,
                    Cursor = new Cursor(StandardCursorType.Hand),
                };
                ToolTip.SetTip(sortBtn, Loc.Instance.SortChips);
                sortBtn.Click += OnSortClick;
                _panel.Children.Add(sortBtn);
            }
        }

        _updating = false;
        _rebuilding = false;
    }

    private void OnSortClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(Text)) return;
        var parts = Text.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (parts.Count < 2) return;

        var categoryOrder = IsFunctorMode
            ? BoostCategories.FunctorCategoryOrder
            : BoostCategories.BoostCategoryOrder;
        var categoryRank = categoryOrder
            .Select((k, i) => (k, i))
            .ToDictionary(p => p.k, p => p.i, StringComparer.OrdinalIgnoreCase);

        var isRu = Loc.Instance.Lang == "ru";
        var defs = IsFunctorMode ? BoostMapping.Functors : BoostMapping.Boosts;

        int RankOf(string raw)
        {
            // Keep IF blocks at their original spot by ranking them high (end)
            if (raw.StartsWith("IF(", StringComparison.OrdinalIgnoreCase)) return 9999;
            var parsed = BoostMapping.ParseBoostCall(raw);
            if (parsed == null) return 9998;
            var fn = parsed.Value.Item1;
            var cat = BoostCategories.GetCategory(fn);
            return categoryRank.GetValueOrDefault(cat, 500);
        }

        string LabelOf(string raw)
        {
            var parsed = BoostMapping.ParseBoostCall(raw);
            if (parsed == null) return raw;
            var def = defs.FirstOrDefault(d => d.FuncName.Equals(parsed.Value.Item1, StringComparison.OrdinalIgnoreCase));
            return def != null ? BoostLabels.GetLabel(def, isRu) : parsed.Value.Item1;
        }

        var sorted = parts
            .Select((raw, idx) => (raw, idx, rank: RankOf(raw), label: LabelOf(raw)))
            .OrderBy(x => x.rank)
            .ThenBy(x => x.label, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.raw)
            .ToList();

        SyncText(string.Join(";", sorted));
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
        // Fallback: StatsFunctors can also use Boost functions
        def ??= (IsFunctorMode ? BoostMapping.Boosts : BoostMapping.Functors)
            .FirstOrDefault(d => d.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));

        if (def == null)
            return CreateRawBlock(rawBoost);

        // Colour-by-damage-type: if this boost has a DamageType enum param and the current
        // argument is a concrete damage type, tint the chip by palette instead of by category
        // so chip and BbCodeTextBlock preview colouring line up.
        Color color = Color.Parse(def.Color);
        for (int pi = 0; pi < def.Params.Length && pi < args.Length; pi++)
        {
            var p = def.Params[pi];
            if (p.Type != "enum" || p.EnumValues == null) continue;
            if (p.EnumValues != BoostMapping.DamageTypes
                && p.EnumValues != BoostMapping.DamageTypesExtended
                && p.EnumValues != BoostMapping.AllOrDamageType) continue;

            var dmgValue = args[pi].Trim();
            var paletteColor = Themes.DamageTypePalette.TryGet(dmgValue);
            if (paletteColor.HasValue) { color = paletteColor.Value; break; }
        }
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

        // Target context tumbler — always shown for functors that support it, or if data already contains one
        var showTargetCtx = targetCtx != null || (IsFunctorMode && TargetContextFunctors.Contains(def.FuncName));
        if (showTargetCtx)
        {
            var ctxItems = new[] { "—" }.Concat(TargetContextValues).ToArray();
            var ctxDisplayItems = new[] { "—" }.Concat(Localization.Loc.Instance.GetEnumDisplayLabels(TargetContextValues)).ToArray();
            var ctxChip = new TumblerChipEditor
            {
                Text = targetCtx ?? "—",
                Items = ctxItems,
                DisplayItems = ctxDisplayItems,
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

        // Parameters — render only params that have values (don't show empty optional trailing params)
        var renderCount = Math.Max(args.Length, 0);
        for (int i = 0; i < Math.Min(def.Params.Length, Math.Max(renderCount, def.Params.Length)); i++)
        {
            var param = def.Params[i];
            var isOptional = i >= args.Length;
            var value = !isOptional ? args[i] : "";

            // Skip trailing params with no value (but keep optnum/optbool — they show "—")
            if (isOptional && string.IsNullOrEmpty(value) && param.Type is not "optnum" and not "optbool") continue;
            var paramIdx = i;

            if (param.Type == "hidden")
            {
                // Invisible constant — don't render, keep value as-is
                continue;
            }

            // Centralized conditional-visibility rules — see ParaTool.Core.Schema.VisibilityRules
            if (VisibilityRules.IsHidden(def, i, args))
            {
                // Clear stale value so hidden arg doesn't leak to compiled output
                if (!isOptional && !string.IsNullOrEmpty(value))
                    UpdateParam(rawBoost, paramIdx, "");
                continue;
            }

            if (param.Type == "int")
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
                        if ((pi == 0 && capturedDef.FuncName is "RollBonus" or "Ability" or "AbilityOverrideMinimum" or "Advantage" or "Disadvantage")
                            || (pi == 1 && capturedDef.FuncName == "DamageReduction"))
                            Avalonia.Threading.Dispatcher.UIThread.Post(Rebuild);
                    }
                };
                stack.Children.Add(chip);
            }
            else if (param.Type == "optbool")
            {
                // Optional bool: "—" / true / false
                var boolItems = new[] { "—", "true", "false" };
                var boolDisplayItems = new[] { "—", "true", "false" };
                var chip = new TumblerChipEditor
                {
                    Text = string.IsNullOrEmpty(value) ? "—" : value,
                    Items = boolItems,
                    DisplayItems = boolDisplayItems,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text == "—" ? "" : tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else if (param.Type == "optnum")
            {
                // Optional number: scrollable number with "—" (none) option
                var chip = new TumblerChipEditor
                {
                    Text = string.IsNullOrEmpty(value) ? "—" : value,
                    Step = 1, MinValue = 0, MaxValue = 999,
                    AllowNone = true,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                chip.Tag = (rawBoost, paramIdx);
                chip.PropertyChanged += (s, e2) =>
                {
                    if (e2.Property.Name == "Text" && s is TumblerChipEditor tc && tc.Tag is (string rb, int pi))
                        UpdateParam(rb, pi, tc.Text == "—" ? "" : tc.Text ?? "");
                };
                stack.Children.Add(chip);
            }
            else if (param.Type is "number" or "float")
            {
                var chip = new TumblerChipEditor
                {
                    Text = value,
                    Step = param.Type == "float" ? 0.1 : 1,
                    MinValue = -999, MaxValue = 999,
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

                if (isStatus || isSpell || isPassive || pickerItems is { Length: > 0 })
                {
                    var picker = new SearchPickerChip
                    {
                        Text = value,
                        Items = pickerItems,
                        Watermark = isStatus ? "Search status..." : "Search...",
                        VerticalAlignment = VerticalAlignment.Center,
                        Resolver = GlobalResolver,
                        LocaService = GlobalLocaService,
                    };
                    picker.Tag = (rawBoost, paramIdx);
                    picker.PropertyChanged += (s, e2) =>
                    {
                        if (e2.Property.Name == "Text" && s is SearchPickerChip sp && sp.Tag is (string rb, int pi) && !_updating)
                            UpdateParam(rb, pi, sp.Text ?? "");
                    };
                    // Context menu: Rename for spell/status params
                    var capturedValue = value;
                    var renameItem = new MenuItem
                    {
                        Header = Localization.Loc.Instance.CtxRename
                    };
                    renameItem.Click += (_, _) => { RenameRequested?.Invoke(capturedValue); GlobalRenameRequested?.Invoke(capturedValue); };
                    picker.ContextMenu = new ContextMenu { Items = { renameItem } };
                    stack.Children.Add(picker);
                }
                else
                {
                    var tb = new TextBox
                    {
                        Text = value, FontSize = FontScale.Of(11),
                        Padding = new Thickness(4, 2), MinWidth = 60,
                        Background = InputBg, Foreground = Themes.ThemeBrushes.TextPrimary,
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(4),
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
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
            IsFunctorMode = IsFunctorMode,
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

    /// <summary>Remove unnecessary trailing args from a boost call.</summary>
    private void CleanTrailingArgs(string rawBoost, string funcName, string[] args, int fromIndex)
    {
        var cleanArgs = args[..fromIndex];
        // Trim trailing empty
        while (cleanArgs.Length > 0 && string.IsNullOrEmpty(cleanArgs[^1]))
            cleanArgs = cleanArgs[..^1];
        var newBoost = cleanArgs.Length > 0 ? $"{funcName}({string.Join(",", cleanArgs)})" : funcName;
        var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var idx = parts.IndexOf(rawBoost);
        if (idx >= 0) { parts[idx] = newBoost; SyncText(string.Join(";", parts)); }
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

        // Apply conditional-visibility rules — if this edit changed a "governing" arg,
        // clear dependents that are no longer valid.
        var def = BoostMapping.Boosts.FirstOrDefault(d => d.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase))
               ?? BoostMapping.Functors.FirstOrDefault(d => d.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));
        if (def != null)
            args = VisibilityRules.ClearHiddenArgs(def, args);

        // Trim trailing empty args
        var trimmedArgs = args.AsEnumerable().ToList();
        while (trimmedArgs.Count > 0 && string.IsNullOrEmpty(trimmedArgs[^1]))
            trimmedArgs.RemoveAt(trimmedArgs.Count - 1);

        var newBoost = trimmedArgs.Count > 0 ? $"{funcName}({string.Join(",", trimmedArgs)})" : $"{funcName}()";

        var parts = (Text ?? "").Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        var idx = parts.IndexOf(rawBoost);
        if (idx >= 0)
            parts[idx] = newBoost;
        SyncText(string.Join(";", parts));
    }

    private void OnAddClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var defs = IsFunctorMode ? BoostMapping.Functors : BoostMapping.Boosts;
        var categoryOrder = IsFunctorMode
            ? BoostCategories.FunctorCategoryOrder
            : BoostCategories.BoostCategoryOrder;
        var menu = new ContextMenu();
        var isRu = Loc.Instance.Lang == "ru";

        // IF block available in both functor and boost modes
        {
            var ifLabel = IsFunctorMode ? Loc.Instance.BoostPickerIfFunctor : Loc.Instance.BoostPickerIfBoost;
            var ifTemplate = IsFunctorMode
                ? "IF(Enemy()):ApplyStatus(YOURSTATUS,100,1)"
                : "IF(Enemy()):AC(1)";
            var ifItem = new MenuItem { Header = $"⚡ {ifLabel}", FontWeight = FontWeight.Bold };
            ifItem.Click += (_, _) =>
            {
                var current = Text ?? "";
                SyncText(string.IsNullOrEmpty(current) ? ifTemplate : $"{current};{ifTemplate}");
            };
            menu.Items.Add(ifItem);
            menu.Items.Add(new Separator());
        }

        // Search box
        var searchBox = new TextBox
        {
            Watermark = Loc.Instance.WmSearchBoost,
            FontSize = FontScale.Of(11),
            MinWidth = 200,
            Margin = new Thickness(4),
        };
        var searchItem = new MenuItem { Header = searchBox, StaysOpenOnClick = true };
        menu.Items.Add(searchItem);
        menu.Items.Add(new Separator());

        var allMenuItems = new List<(MenuItem item, string searchText)>();

        // Weapon-specific pinned section
        if (EditorContext == BoostEditorContext.WeaponDefaultBoosts)
        {
            var header = new MenuItem
            {
                Header = $"⚔ {Loc.Instance.BoostPickerWeaponSection}",
                IsEnabled = false,
                FontWeight = FontWeight.SemiBold,
            };
            menu.Items.Add(header);

            foreach (var funcName in BoostCategories.WeaponDefaultBoostWhitelist)
            {
                var def = defs.FirstOrDefault(d =>
                    d.FuncName.Equals(funcName, StringComparison.OrdinalIgnoreCase));
                if (def == null) continue;

                var displayName = BoostLabels.GetLabel(def, isRu);
                var item = new MenuItem { Header = displayName, Tag = def };
                item.Click += (_, _) => { InsertDef(def); menu.Close(); };
                menu.Items.Add(item);
            }
            menu.Items.Add(new Separator());
        }

        // Favorites (rebuildable)
        var favItems = new Dictionary<string, MenuItem>(StringComparer.OrdinalIgnoreCase);
        var favSeparatorIdx = menu.Items.Count;

        void RebuildFavSection()
        {
            foreach (var fi in favItems.Values)
                menu.Items.Remove(fi);
            favItems.Clear();

            var currentFavs = Core.Services.BoostFavoritesStore.Load();
            int insertIdx = favSeparatorIdx;
            var sorted = currentFavs
                .Select(n => (name: n, def: defs.FirstOrDefault(b =>
                    b.FuncName.Equals(n, StringComparison.OrdinalIgnoreCase))))
                .Where(x => x.def != null)
                .OrderBy(x => BoostLabels.GetLabel(x.def!, isRu));

            foreach (var (favName, def) in sorted)
            {
                var dName = BoostLabels.GetLabel(def!, isRu);
                var fItem = new MenuItem { Header = $"★ {dName}", Tag = def };
                fItem.Click += (_, _) => { InsertDef(def!); menu.Close(); };
                menu.Items.Insert(insertIdx++, fItem);
                favItems[favName] = fItem;
            }
        }
        RebuildFavSection();

        menu.Items.Add(new Separator());

        // Categories
        var userFavs = Core.Services.BoostFavoritesStore.Load();
        var byCat = defs
            .GroupBy(d => BoostCategories.GetCategory(d.FuncName))
            .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.OrdinalIgnoreCase);

        foreach (var catKey in categoryOrder)
        {
            if (!byCat.TryGetValue(catKey, out var catDefs) || catDefs.Count == 0) continue;
            var sub = new MenuItem { Header = BoostLabels.GetCategoryLabel(catKey, isRu) };

            foreach (var def in catDefs.OrderBy(d => BoostLabels.GetLabel(d, isRu)))
            {
                var displayName = BoostLabels.GetLabel(def, isRu);
                var isFav = userFavs.Contains(def.FuncName);
                var star = isFav ? "★" : "☆";

                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var starBtn = new Button
                {
                    Content = star,
                    Padding = new Thickness(0),
                    Background = Brushes.Transparent,
                    BorderThickness = new Thickness(0),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    FontSize = FontScale.Of(12),
                    Foreground = ThemeBrushes.Accent,
                    MinWidth = 0, MinHeight = 0,
                };
                var funcName = def.FuncName;
                starBtn.Click += (s, e2) =>
                {
                    e2.Handled = true;
                    var nowFav = Core.Services.BoostFavoritesStore.Toggle(funcName);
                    if (s is Button b) b.Content = nowFav ? "★" : "☆";
                    RebuildFavSection();
                };
                itemPanel.Children.Add(starBtn);
                itemPanel.Children.Add(new TextBlock
                {
                    Text = displayName,
                    VerticalAlignment = VerticalAlignment.Center
                });

                var item = new MenuItem { Header = itemPanel, Tag = def, StaysOpenOnClick = true };
                item.Click += (_, _) => { InsertDef(def); menu.Close(); };
                sub.Items.Add(item);
                allMenuItems.Add((item, $"{displayName} {def.FuncName}".ToLower()));
            }
            menu.Items.Add(sub);
        }

        searchBox.TextChanged += (_, _) =>
        {
            var q = (searchBox.Text ?? "").Trim().ToLower();
            foreach (var (item, searchText) in allMenuItems)
                item.IsVisible = string.IsNullOrEmpty(q) || searchText.Contains(q);
        };

        menu.Open(this);
        searchBox.Focus();
    }

    private void InsertDef(BoostMapping.BlockDef d)
    {
        var defaultArgs = d.Params.Select(p =>
        {
            // Ability/AbilityOverrideMinimum: pre-fill Cap=24 so the user isn't silently
            // capped at BG3's default 20 when they leave it blank.
            if (p.Type == "optnum" && p.Name == "Cap"
                && d.FuncName is "Ability" or "AbilityOverrideMinimum")
                return "24";

            return p.Type switch
            {
                "hidden" => "100",
                "int" => "1",
                "number" => "1",
                "float" => "1",
                "dice" => "1d6",
                "formula" => "1",
                "bool" => "true",
                // Optional args start empty so UpdateParam trims them off cleanly.
                "optnum" => "",
                "optbool" => "",
                // Prefer the first *non-empty* enum/flags value (MagicalFlags ships with
                // an empty sentinel that would otherwise become the default).
                "enum" => p.EnumValues?.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "",
                "flags" => p.EnumValues?.FirstOrDefault(v => !string.IsNullOrEmpty(v)) ?? "",
                // Empty string → watermarked input in UI; no more YOURSTATUS/YourSpell
                // landing in compiled stats when the user forgets to fill the chip.
                "string" => p.Name.Contains("Resource") ? "ActionPoint" : "",
                _ => ""
            };
        }).ToArray();

        // Apply conditional-visibility rules so the new chip doesn't start with a
        // stale dependent arg (e.g. Ability chip on Strength shouldn't ship Savant=true).
        defaultArgs = VisibilityRules.ClearHiddenArgs(d, defaultArgs);

        // Trim trailing empty defaults (mirrors UpdateParam semantics)
        while (defaultArgs.Length > 0 && string.IsNullOrEmpty(defaultArgs[^1]))
            defaultArgs = defaultArgs[..^1];

        var newBoost = defaultArgs.Length > 0
            ? $"{d.FuncName}({string.Join(",", defaultArgs)})"
            : $"{d.FuncName}()";
        var current = Text ?? "";
        SyncText(string.IsNullOrEmpty(current) ? newBoost : $"{current};{newBoost}");
    }

    private static readonly string[] TargetContextValues = ["SELF", "SWAP", "OBSERVER_TARGET", "OBSERVER_SOURCE"];

    /// <summary>Functors that commonly use a target context prefix (SELF/SWAP/etc.).</summary>
    private static readonly HashSet<string> TargetContextFunctors = ["ApplyStatus", "DealDamage", "RegainHitPoints", "RemoveStatus", "RemoveUniqueStatus", "GainTemporaryHitPoints"];

    private void UpdateTargetContext(string rawBoost, string? newCtx)
    {
        if (_updating) return;
        var parsed = BoostMapping.ParseBoostCall(rawBoost);
        if (parsed == null)
        {
            Core.Services.AppLogger.Warn($"UpdateTargetContext: failed to parse boost '{rawBoost}' — target context change ignored.");
            return;
        }

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
