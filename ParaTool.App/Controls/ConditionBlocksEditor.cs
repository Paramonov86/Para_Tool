
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Localization;
using ParaTool.Core.Schema;
using ParaTool.App.Services;

namespace ParaTool.App.Controls;

/// <summary>
/// Visual condition chip editor. Each condition function = a colored chip with
/// typed parameter controls (dropdowns, tumblers). AND/OR connectors between chips.
/// Parses/serializes "InSurface('SurfaceWater') and Enemy() and not Dead()".
/// </summary>
public class ConditionBlocksEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ConditionBlocksEditor, string?>(nameof(Text),
            defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? Text { get => GetValue(TextProperty); set => SetValue(TextProperty, value); }

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal, ClipToBounds = false };
    private bool _updating;
    private List<CondToken> _tokens = [];
    private readonly Stack<string> _undoStack = new(); // for Ctrl+Z

    private static SolidColorBrush FgFunc => Themes.ThemeBrushes.Get("SuccessBrush");
    private static SolidColorBrush BgFunc => new(Themes.ThemeBrushes.Get("SuccessBrush").Color, 0.1);
    private static readonly Color OpColor = Color.Parse("#F1C40F");
    private static SolidColorBrush FgOp => new(OpColor);
    private static SolidColorBrush BgOp => new(OpColor, 0.1);
    private static SolidColorBrush FgNot => Themes.ThemeBrushes.Get("ErrorBrush");
    private static SolidColorBrush BgNot => new(Themes.ThemeBrushes.Get("ErrorBrush").Color, 0.1);
    private static SolidColorBrush FgMuted => Themes.ThemeBrushes.TextMuted;
    private static SolidColorBrush InputBg => Themes.ThemeBrushes.InputBg;

    private readonly PropertyChangedEventHandler _locHandler;
    private readonly Action _scaleHandler;

    public ConditionBlocksEditor()
    {
        Content = _panel;
        ClipToBounds = false;
        Focusable = true;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty && !_updating) Rebuild();
        };
        AttachedToVisualTree += (_, _) => { if (!_updating && !_rebuilding && !string.IsNullOrEmpty(Text)) Rebuild(); };
        // Rebuild chips when UI language changes (labels need to update)
        _locHandler = (_, _) => { if (!_updating && IsLoaded) Avalonia.Threading.Dispatcher.UIThread.Post(() => { if (IsLoaded) Rebuild(); }); };
        _scaleHandler = () => { if (!_updating && IsLoaded) Rebuild(); };
        Loc.Instance.PropertyChanged += _locHandler;
        FontScale.ScaleChanged += _scaleHandler;
        AddHandler(KeyDownEvent, (_, e) =>
        {
            if (e.Key == Key.Z && e.KeyModifiers.HasFlag(KeyModifiers.Control) && _undoStack.Count > 0)
            {
                _updating = true;
                Text = _undoStack.Pop();
                _updating = false;
                Rebuild();
                e.Handled = true;
            }
        }, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    // ── Parsed token model ─────────────────────────────────────

    private sealed class CondToken
    {
        public enum Kind { Func, And, Or, Group }
        public Kind Type;
        public bool Negated;
        public string FuncName = "";
        public string[] Args = [];
        public string Raw = "";
        public List<CondToken>? Children; // for Group type
    }

    // ── Rebuild UI from text ───────────────────────────────────

    private bool _rebuilding;

    private void Rebuild()
    {
        if (_rebuilding) return;
        _rebuilding = true;
        try { RebuildCore(); } finally { _rebuilding = false; }
    }

    private void RebuildCore()
    {
        _panel.Children.Clear();
        var raw = Text ?? "";
        if (string.IsNullOrWhiteSpace(raw)) { _tokens = []; AddPlusButton(); return; }

        _tokens = Tokenize(raw);
        for (int i = 0; i < _tokens.Count; i++)
        {
            var token = _tokens[i];
            var idx = i;

            Control element;
            if (token.Type is CondToken.Kind.And or CondToken.Kind.Or)
            {
                var connLabel = token.Type == CondToken.Kind.And
                    ? Localization.Loc.Instance["LblAnd"]
                    : Localization.Loc.Instance["LblOr"];
                var connBtn = new Button
                {
                    Content = connLabel,
                    FontSize = FontScale.Of(10), FontWeight = FontWeight.Bold,
                    Foreground = FgOp, Background = BgOp,
                    Padding = new Thickness(6, 2), Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(8),
                    BorderThickness = new Thickness(0),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = idx,
                };
                connBtn.Click += (_, _) =>
                {
                    token.Type = token.Type == CondToken.Kind.And ? CondToken.Kind.Or : CondToken.Kind.And;
                    SyncFromTokens(_tokens);
                };
                element = connBtn;
            }
            else if (token.Type == CondToken.Kind.Group)
            {
                element = BuildGroupChip(token, _tokens, idx);
            }
            else
            {
                element = BuildConditionChip(token, _tokens, idx);
            }

            // Drag-and-drop: hold and drag to reorder
            SetupReorder(element, idx);
            _panel.Children.Add(element);
        }

        AddPlusButton();
    }

    // ── Drag-and-drop reorder ─────────────────────────────────

    private int _dragFromIdx = -1;

    private void SetupReorder(Control element, int tokenIdx)
    {
        DragDrop.SetAllowDrop(element, true);

        element.PointerPressed += (s, e) =>
        {
            if (!e.GetCurrentPoint(element).Properties.IsLeftButtonPressed) return;
            if (s is not Control ctrl) return;

            var startPos = e.GetPosition(_panel);
            bool dragging = false;

            void onMove(object? _, PointerEventArgs me)
            {
                if (dragging) return;
                var pos = me.GetPosition(_panel);
                if (Math.Abs(pos.X - startPos.X) > 12 || Math.Abs(pos.Y - startPos.Y) > 12)
                {
                    dragging = true;
                    ctrl.PointerMoved -= onMove;
                    _dragFromIdx = tokenIdx;
                    ctrl.Opacity = 0.4;
                    try
                    {
                        var data = new DataObject();
                        data.Set("CondIdx", tokenIdx.ToString());
                        DragDrop.DoDragDrop(e, data, DragDropEffects.Move);
                    }
                    catch { }
                    ctrl.Opacity = 1;
                    _dragFromIdx = -1;
                }
            }
            ctrl.PointerMoved += onMove;
            ctrl.PointerReleased += (_, _) => ctrl.PointerMoved -= onMove;
        };

        element.AddHandler(DragDrop.DragOverEvent, (_, e) => e.DragEffects = DragDropEffects.Move);
        element.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.Data.Get("CondIdx") is not string fromStr || !int.TryParse(fromStr, out var fromIdx)) return;
            if (fromIdx == tokenIdx || fromIdx < 0 || fromIdx >= _tokens.Count) return;

            // Move token (not copy!)
            var moved = _tokens[fromIdx];
            _tokens.RemoveAt(fromIdx);
            var insertAt = tokenIdx > fromIdx ? tokenIdx - 1 : tokenIdx;
            insertAt = Math.Clamp(insertAt, 0, _tokens.Count);
            _tokens.Insert(insertAt, moved);

            // Fix: ensure valid AND/OR between all conditions
            NormalizeTokens(_tokens);
            SyncFromTokens(_tokens);
        });

        var moveLeft = new MenuItem { Header = "← Move Left" };
        moveLeft.Click += (_, _) =>
        {
            if (tokenIdx > 0 && tokenIdx < _tokens.Count)
            {
                (_tokens[tokenIdx], _tokens[tokenIdx - 1]) = (_tokens[tokenIdx - 1], _tokens[tokenIdx]);
                SyncFromTokens(_tokens);
            }
        };
        var moveRight = new MenuItem { Header = "→ Move Right" };
        moveRight.Click += (_, _) =>
        {
            if (tokenIdx >= 0 && tokenIdx < _tokens.Count - 1)
            {
                (_tokens[tokenIdx], _tokens[tokenIdx + 1]) = (_tokens[tokenIdx + 1], _tokens[tokenIdx]);
                SyncFromTokens(_tokens);
            }
        };

        if (element.ContextMenu != null)
        {
            element.ContextMenu.Items.Add(new Separator());
            element.ContextMenu.Items.Add(moveLeft);
            element.ContextMenu.Items.Add(moveRight);
        }
        else
        {
            element.ContextMenu = new ContextMenu { Items = { moveLeft, moveRight } };
        }
    }

    // ── Build a single condition chip ──────────────────────────

    private Control BuildConditionChip(CondToken token, List<CondToken> tokens, int tokenIdx)
    {
        var schema = ConditionSchema.Instance;
        schema.ByName.TryGetValue(token.FuncName, out var def);

        var stack = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 3 };

        // NOT toggle button
        if (token.Negated)
        {
            var notBtn = new Button
            {
                Content = Localization.Loc.Instance["LblNot"], FontSize = FontScale.Of(9), FontWeight = FontWeight.Bold,
                Foreground = FgNot, Background = BgNot,
                Padding = new Thickness(4, 1), CornerRadius = new CornerRadius(4),
                BorderThickness = new Thickness(0),
                Cursor = new Cursor(StandardCursorType.Hand),
            };
            notBtn.Click += (_, _) => { token.Negated = false; SyncFromTokens(tokens); };
            stack.Children.Add(notBtn);
        }

        // Function label
        var isRu = Localization.Loc.Instance.Lang == "ru";
        var label = ConditionLabels.GetLabel(token.FuncName, isRu);
        stack.Children.Add(new TextBlock
        {
            Text = label, FontSize = FontScale.Of(11), FontWeight = FontWeight.SemiBold,
            Foreground = FgFunc, VerticalAlignment = VerticalAlignment.Center,
        });

        // Parameter controls
        // Only render params that actually exist in the expression
        // Optional params (entity target/source) only shown if explicitly provided
        var paramCount = def?.Params.Length ?? 0;
        var argCount = token.Args.Length;
        var count = argCount;

        // Detect if leading optional entity param was omitted:
        // If first param is optional entity AND argCount < paramCount AND first arg doesn't look like entity
        int paramOffset = 0;
        if (def != null && paramCount > 0 && argCount < paramCount
            && def.Params[0].IsOptional
            && (def.Params[0].EnumValues == ConditionSchema.EntityTargetsEn || def.Params[0].EnumValues == ConditionSchema.EntityTargetsRu))
        {
            var firstArg = argCount > 0 ? token.Args[0].Trim('\'', '"', ' ') : "";
            if (!firstArg.StartsWith("context.", StringComparison.OrdinalIgnoreCase))
                paramOffset = 1; // skip entity param in definition
        }

        for (int pi = 0; pi < count; pi++)
        {
            var param = (pi + paramOffset) < paramCount ? def!.Params[pi + paramOffset] : null;
            var argVal = pi < argCount ? token.Args[pi].Trim('\'', '"', ' ') : "";
            var paramIdx = pi;

            if (param?.Type == "flags" && param.EnumValues != null)
            {
                // Strip enum prefix from each flag value (e.g. "SpellFlags.Concentration" → "Concentration")
                var flagPrefix = "";
                var cleanFlags = argVal;
                if (argVal.Contains('.'))
                {
                    var flagParts = argVal.Split(';').Select(f =>
                    {
                        var dot = f.IndexOf('.');
                        if (dot > 0) { flagPrefix = f[..(dot + 1)]; return f[(dot + 1)..]; }
                        return f;
                    });
                    cleanFlags = string.Join(";", flagParts);
                }
                var capturedFlagPrefix = flagPrefix;
                var lang = Localization.Loc.Instance.Lang;
                var flagsPicker = new ChecklistPickerChip
                {
                    Text = cleanFlags,
                    Options = param.EnumValues,
                    Labels = Localization.Loc.Instance.GetEnumDisplayLabels(param.EnumValues),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                flagsPicker.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Text" && s is ChecklistPickerChip cp)
                    {
                        var val = cp.Text ?? "";
                        if (!string.IsNullOrEmpty(capturedFlagPrefix))
                            val = string.Join(";", val.Split(';').Select(f => capturedFlagPrefix + f));
                        token.Args[paramIdx] = val;
                        SyncFromTokens(tokens);
                    }
                };
                stack.Children.Add(flagsPicker);
            }
            else if (param?.Type == "enum" && param.EnumValues != null)
            {
                var isRuParam = Localization.Loc.Instance.Lang == "ru";
                var isComplexExpr = argVal.Contains('(') && !argVal.StartsWith("context.");
                var isEntity = !isComplexExpr && (param.EnumValues == ConditionSchema.EntityTargetsEn
                    || param.EnumValues == ConditionSchema.EntityTargetsRu
                    || argVal.StartsWith("context.", StringComparison.OrdinalIgnoreCase));

                // Skip entity param only if it's empty (not explicitly set)
                if (isEntity && argVal == "")
                    continue;

                // Complex expressions like GetAttackWeapon(Source) → read-only text
                if (isComplexExpr)
                {
                    stack.Children.Add(new TextBlock
                    {
                        Text = argVal, FontSize = FontScale.Of(10),
                        Foreground = FgMuted, VerticalAlignment = VerticalAlignment.Center,
                        Background = InputBg, Padding = new Thickness(4, 1),
                    });
                    continue;
                }

                // Strip enum prefix (e.g. "DamageType.Fire" → "Fire")
                var enumPrefix = "";
                var cleanVal = argVal;
                var dotIdx = argVal.IndexOf('.');
                if (dotIdx > 0 && !argVal.StartsWith("context."))
                {
                    enumPrefix = argVal[..(dotIdx + 1)]; // "DamageType."
                    cleanVal = argVal[(dotIdx + 1)..];   // "Fire"
                }

                var lang = Localization.Loc.Instance.Lang;
                var displayVal = isEntity ? ConditionSchema.EntityFromRaw(argVal, isRuParam) : cleanVal;
                var tumblerItems = isEntity ? ConditionSchema.GetEntityTargets(isRuParam) : param.EnumValues;
                var tumblerDisplayItems = isEntity ? null
                    : param.DisplayValues ?? (param.EnumValues != null ? Localization.Loc.Instance.GetEnumDisplayLabels(param.EnumValues) : null);
                var capturedPrefix = enumPrefix;
                var enumTumbler = new TumblerChipEditor
                {
                    Text = displayVal, Items = tumblerItems,
                    DisplayItems = tumblerDisplayItems,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                enumTumbler.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Text" && s is TumblerChipEditor tc)
                    {
                        var raw = isEntity ? ConditionSchema.EntityToRaw(tc.Text ?? "") : tc.Text ?? "";
                        token.Args[paramIdx] = capturedPrefix + raw;
                        SyncFromTokens(tokens);
                    }
                };
                stack.Children.Add(enumTumbler);
            }
            else if (param?.Type is "int")
            {
                var tumbler = new TumblerChipEditor
                {
                    Text = argVal, Step = 1, MinValue = 0, MaxValue = 999,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                tumbler.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Text" && s is TumblerChipEditor tc)
                    {
                        token.Args[paramIdx] = tc.Text ?? "0";
                        SyncFromTokens(tokens);
                    }
                };
                stack.Children.Add(tumbler);
            }
            else if (param?.Type == "bool" || (param?.Type != "enum" && argVal is "true" or "false"))
            {
                var boolTumbler = new TumblerChipEditor
                {
                    Text = argVal, Items = ["true", "false"],
                    VerticalAlignment = VerticalAlignment.Center,
                };
                boolTumbler.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Text" && s is TumblerChipEditor tc)
                    {
                        token.Args[paramIdx] = tc.Text ?? "false";
                        SyncFromTokens(tokens);
                    }
                };
                stack.Children.Add(boolTumbler);
            }
            else if (param?.Type == "float")
            {
                var tumbler = new TumblerChipEditor
                {
                    Text = argVal, Step = 0.5, MinValue = 0, MaxValue = 9999,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                tumbler.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Text" && s is TumblerChipEditor tc)
                    {
                        token.Args[paramIdx] = tc.Text ?? "0";
                        SyncFromTokens(tokens);
                    }
                };
                stack.Children.Add(tumbler);
            }
            else if (argVal.StartsWith("context.", StringComparison.OrdinalIgnoreCase))
            {
                // Entity value in a non-entity param — always show if explicitly set
                var isRuEntity = Localization.Loc.Instance.Lang == "ru";
                var entityTumbler = new TumblerChipEditor
                {
                    Text = ConditionSchema.EntityFromRaw(argVal, isRuEntity),
                    Items = ConditionSchema.GetEntityTargets(isRuEntity),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                entityTumbler.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Text" && s is TumblerChipEditor tc)
                    {
                        token.Args[paramIdx] = ConditionSchema.EntityToRaw(tc.Text ?? "");
                        SyncFromTokens(tokens);
                    }
                };
                stack.Children.Add(entityTumbler);
            }
            else
            {
                // Check if this string param has a searchable list
                var paramLower = param?.Name?.ToLowerInvariant() ?? "";
                string[]? searchItems = paramLower switch
                {
                    "statusid" or "status" => BoostBlocksEditor.GlobalStatusList,
                    "spellid" or "spell" => BoostBlocksEditor.GlobalSpellList,
                    "passivename" or "passive" => BoostBlocksEditor.GlobalPassiveList,
                    _ => null,
                };

                var isSearchable = paramLower is "statusid" or "status" or "spellid" or "spell" or "passivename" or "passive";
                if (isSearchable || searchItems is { Length: > 0 })
                {
                    // SearchPickerChip for status/spell/passive
                    var picker = new SearchPickerChip
                    {
                        Text = argVal,
                        Items = searchItems,
                        Watermark = Localization.Loc.Instance.WmSearch,
                        VerticalAlignment = VerticalAlignment.Center,
                        Resolver = BoostBlocksEditor.GlobalResolver,
                        LocaService = BoostBlocksEditor.GlobalLocaService,
                    };
                    picker.PropertyChanged += (s, e2) =>
                    {
                        if (e2.Property.Name == "Text" && s is SearchPickerChip sp)
                        {
                            token.Args[paramIdx] = $"'{sp.Text}'";
                            SyncFromTokens(tokens);
                        }
                    };
                    stack.Children.Add(picker);
                }
                else
                {
                    // String/unknown — small TextBox
                    var tb = new TextBox
                    {
                        Text = argVal, FontSize = FontScale.Of(10),
                        Padding = new Thickness(4, 2), MinWidth = 60,
                        Background = InputBg, Foreground = Themes.ThemeBrushes.TextPrimary,
                        BorderThickness = new Thickness(0),
                        CornerRadius = new CornerRadius(4),
                        VerticalAlignment = VerticalAlignment.Center,
                        VerticalContentAlignment = VerticalAlignment.Center,
                    };
                    tb.LostFocus += (s, _) =>
                    {
                        if (s is TextBox t)
                        {
                            token.Args[paramIdx] = $"'{t.Text}'";
                            SyncFromTokens(tokens);
                        }
                    };
                    stack.Children.Add(tb);
                }
            }
        }

        // "+" button to add optional params — skip entity defaults, only offer context.Source
        var hasEntityArg = token.Args.Any(a => a.TrimStart('\'', '"').StartsWith("context."));
        if (def != null && argCount < paramCount && !hasEntityArg)
        {
            var nextParam = def.Params[argCount];
            // Skip if next param is entity and default (most users don't need it)
            var isEntityNext = nextParam.EnumValues == ConditionSchema.EntityTargetsEn;
            // Only show +source button if explicitly useful
            if (isEntityNext) nextParam = new ConditionParam
            {
                Name = "source", Type = "enum",
                EnumValues = ConditionSchema.EntityTargetsEn
            };
            var addArgBtn = new Button
            {
                Content = $"+{nextParam.Name}", FontSize = FontScale.Of(9),
                Padding = new Thickness(4, 1),
                Background = Brushes.Transparent, Foreground = FgMuted,
                BorderThickness = new Thickness(1), BorderBrush = FgMuted,
                CornerRadius = new CornerRadius(4),
                Cursor = new Cursor(StandardCursorType.Hand),
                VerticalAlignment = VerticalAlignment.Center,
            };
            addArgBtn.Click += (_, _) =>
            {
                // Add default value for the next optional param
                var defaultVal = nextParam.Type switch
                {
                    "enum" => nextParam.EnumValues?.FirstOrDefault() ?? "context.Target",
                    "flags" => nextParam.EnumValues?.FirstOrDefault() ?? "",
                    "int" => "1",
                    "bool" => "true",
                    _ => "context.Target"
                };
                var args = token.Args.ToList();
                args.Add(nextParam.Type == "string" || nextParam.Type == "enum" ? $"'{defaultVal}'" : defaultVal);
                token.Args = args.ToArray();
                SyncFromTokens(tokens);
            };
            stack.Children.Add(addArgBtn);
        }

        if (count == 0 && token.Args.Length > 0 && def == null)
        {
            // Unknown function — show raw args as text
            var argsText = string.Join(", ", token.Args);
            stack.Children.Add(new TextBlock
            {
                Text = $"({argsText})", FontSize = FontScale.Of(10),
                Foreground = FgMuted, VerticalAlignment = VerticalAlignment.Center,
            });
        }

        // Context menu: NOT toggle + delete
        var notToggle = new MenuItem { Header = token.Negated ? "Remove NOT" : "Add NOT" };
        notToggle.Click += (_, _) => { token.Negated = !token.Negated; SyncFromTokens(tokens); };

        var delete = new MenuItem { Header = "Delete" };
        delete.Click += (_, _) => { RemoveToken(tokens, tokenIdx); SyncFromTokens(tokens); };

        // × remove button
        var removeBtn = new Button
        {
            Content = "×", FontSize = FontScale.Of(10),
            Padding = new Thickness(3, 0),
            Background = Brushes.Transparent, Foreground = FgMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
            VerticalAlignment = VerticalAlignment.Center,
        };
        removeBtn.Click += (_, _) => { RemoveToken(tokens, tokenIdx); SyncFromTokens(tokens); };
        stack.Children.Add(removeBtn);

        var chip = new Border
        {
            Child = stack,
            Background = BgFunc, CornerRadius = new CornerRadius(8),
            Padding = new Thickness(6, 3), Margin = new Thickness(2),
            ContextMenu = new ContextMenu { Items = { notToggle, delete } },
        };

        return chip;
    }

    // ── Group chip (parentheses) ───────────────────────────────

    private Control BuildGroupChip(CondToken token, List<CondToken> tokens, int tokenIdx)
    {
        // Serialize children to string for embedded editor
        var innerText = token.Children != null ? SerializeTokens(token.Children) : "";

        var header = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
        header.Children.Add(new TextBlock
        {
            Text = "( )", FontSize = FontScale.Of(11), FontWeight = FontWeight.Bold,
            Foreground = FgMuted, VerticalAlignment = VerticalAlignment.Center,
        });
        var removeBtn = new Button
        {
            Content = "×", FontSize = FontScale.Of(10),
            Padding = new Thickness(3, 0),
            Background = Brushes.Transparent, Foreground = FgMuted,
            BorderThickness = new Thickness(0),
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        removeBtn.Click += (_, _) => { RemoveToken(tokens, tokenIdx); SyncFromTokens(tokens); };
        header.Children.Add(removeBtn);

        // Embedded condition editor for group content
        var innerEditor = new ConditionBlocksEditor
        {
            Text = innerText,
            Margin = new Thickness(4, 2, 4, 0),
            ClipToBounds = false,
        };
        innerEditor.PropertyChanged += (s, e) =>
        {
            if (e.Property.Name == "Text" && s is ConditionBlocksEditor ce && !_updating)
            {
                // Re-parse inner text to update token children
                token.Children = Tokenize(ce.Text ?? "");
                SyncFromTokens(tokens);
            }
        };

        var stack = new StackPanel { Spacing = 2, Children = { header, innerEditor } };

        return new Border
        {
            Child = stack,
            Background = new SolidColorBrush(Themes.ThemeBrushes.BorderSubtle.Color, 0.35),
            BorderBrush = Themes.ThemeBrushes.TextMuted,
            BorderThickness = new Thickness(2),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(8, 6), Margin = new Thickness(2),
            ClipToBounds = false,
        };
    }

    // ── Add button with categorized menu ───────────────────────

    private void AddPlusButton()
    {
        var addBtn = new Button
        {
            Content = "+", FontSize = FontScale.Of(12), FontWeight = FontWeight.Bold,
            Padding = new Thickness(6, 2), Margin = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Background = InputBg, Foreground = FgFunc,
            BorderThickness = new Thickness(1), BorderBrush = FgFunc,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        addBtn.Click += OnAddClick;
        _panel.Children.Add(addBtn);
    }

    private void OnAddClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var schema = ConditionSchema.Instance;
        var menu = new ContextMenu();

        // Group (parentheses) option at top
        var groupItem = new MenuItem { Header = "( ) Group / Скобки", FontWeight = FontWeight.SemiBold };
        groupItem.Click += (_, _) =>
        {
            var current = Text ?? "";
            var addition = "(Enemy())";
            _updating = true;
            Text = string.IsNullOrEmpty(current) ? addition : $"{current} and {addition}";
            _updating = false;
            Rebuild();
        };
        menu.Items.Add(groupItem);
        menu.Items.Add(new Separator());

        // Group by category (snapshot to avoid collection-modified crash)
        var funcs = schema.Functions.ToList();
        var groups = funcs
            .Where(f => f.Category != "General" || f.Params.Length == 0) // skip complex helpers
            .GroupBy(f => f.Category)
            .OrderBy(g => g.Key);

        var userFavs = Core.Services.FavoritesStore.Load();
        var isRu = Localization.Loc.Instance.Lang == "ru";

        // Search box at top
        var searchBox = new TextBox
        {
            Watermark = isRu ? "Поиск условия..." : "Search condition...",
            FontSize = Services.FontScale.Of(11),
            MinWidth = 200,
            Margin = new Thickness(4),
        };
        var searchItem = new MenuItem { Header = searchBox, StaysOpenOnClick = true };
        menu.Items.Add(searchItem);
        menu.Items.Add(new Separator());

        // Build all menu items for filtering
        var allMenuItems = new List<(MenuItem item, MenuItem? parent, string searchText)>();

        // Track favorites section items for in-place updates
        var favItems = new Dictionary<string, MenuItem>(StringComparer.OrdinalIgnoreCase);
        var favSeparatorIdx = menu.Items.Count; // index where favorites start

        // User favorites at top
        void RebuildFavSection()
        {
            // Remove old favorites (between favSeparatorIdx and the separator after them)
            foreach (var fi in favItems.Values)
                menu.Items.Remove(fi);
            favItems.Clear();

            var currentFavs = Core.Services.FavoritesStore.Load();
            int insertIdx = favSeparatorIdx;
            foreach (var favName in currentFavs.OrderBy(n => ConditionLabels.GetLabel(n, isRu)))
            {
                if (!schema.ByName.TryGetValue(favName, out var fDef)) continue;
                var dName = ConditionLabels.GetLabel(favName, isRu);
                var fItem = new MenuItem { Header = $"★ {dName}", Tag = fDef };
                fItem.Click += (_, _) => AddCondition(fDef);
                menu.Items.Insert(insertIdx++, fItem);
                favItems[favName] = fItem;
            }
        }
        RebuildFavSection();

        menu.Items.Add(new Separator());

        // All by category, sorted by localized name, with star toggle
        foreach (var group in groups)
        {
            var sub = new MenuItem { Header = ConditionLabels.GetCategoryLabel(group.Key, isRu) };
            foreach (var def in group.OrderBy(d => ConditionLabels.GetLabel(d.Name, isRu)))
            {
                var displayName = ConditionLabels.GetLabel(def.Name, isRu);
                var paramHint = def.Params.Length > 0
                    ? $" ({string.Join(", ", def.Params.Select(p => p.Name))})"
                    : "";
                var isFav = userFavs.Contains(def.Name);
                var star = isFav ? "★" : "☆";

                var itemPanel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 6 };
                var starBtn = new Button
                {
                    Content = star, Padding = new Thickness(0), Background = Avalonia.Media.Brushes.Transparent,
                    BorderThickness = new Thickness(0), Cursor = new Avalonia.Input.Cursor(Avalonia.Input.StandardCursorType.Hand),
                    FontSize = Services.FontScale.Of(12), Foreground = Themes.ThemeBrushes.Accent,
                    MinWidth = 0, MinHeight = 0,
                };
                var condName = def.Name; // capture
                starBtn.Click += (s, e2) =>
                {
                    e2.Handled = true;
                    var nowFav = Core.Services.FavoritesStore.Toggle(condName);
                    if (s is Button b) b.Content = nowFav ? "★" : "☆";
                    RebuildFavSection();
                };
                itemPanel.Children.Add(starBtn);
                itemPanel.Children.Add(new TextBlock { Text = displayName + paramHint, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center });

                var item = new MenuItem { Header = itemPanel, Tag = def, StaysOpenOnClick = true };
                item.Click += (_, _) => { AddCondition(def); menu.Close(); };
                sub.Items.Add(item);
                allMenuItems.Add((item, sub, $"{displayName} {def.Name}".ToLower()));
            }
            menu.Items.Add(sub);
        }

        // Search filtering
        searchBox.TextChanged += (_, _) =>
        {
            var q = (searchBox.Text ?? "").Trim().ToLower();
            foreach (var (item, parent, searchText) in allMenuItems)
            {
                item.IsVisible = string.IsNullOrEmpty(q) || searchText.Contains(q);
            }
        };

        menu.Open(this);
        searchBox.Focus();
    }

    private void AddCondition(ConditionDef def)
    {
        // Build default args (skip optional params — user adds them explicitly)
        var args = def.Params
            .Where(p => !p.IsOptional)
            .Select(p => p.Type switch
            {
                "enum" => $"'{p.EnumValues?.FirstOrDefault() ?? "None"}'",
                "flags" => $"'{p.EnumValues?.FirstOrDefault() ?? "None"}'",
                "int" => "1",
                "float" => "1",
                "bool" => "true",
                _ => $"'{p.Name}'"
            }).ToArray();

        var call = args.Length > 0 ? $"{def.Name}({string.Join(",", args)})" : $"{def.Name}()";

        var current = Text ?? "";
        _updating = true;
        Text = string.IsNullOrEmpty(current) ? call : $"{current} and {call}";
        _updating = false;
        Rebuild();
    }

    // ── Token removal ──────────────────────────────────────────

    /// <summary>Ensure valid token sequence: no double operators, no leading/trailing operators,
    /// AND inserted between adjacent conditions.</summary>
    private static void NormalizeTokens(List<CondToken> tokens)
    {
        // Remove empty groups
        tokens.RemoveAll(t => t.Type == CondToken.Kind.Group && (t.Children == null || t.Children.Count == 0));

        // Normalize children of groups recursively
        foreach (var t in tokens)
            if (t.Type == CondToken.Kind.Group && t.Children != null)
                NormalizeTokens(t.Children);

        // Remove leading operators
        while (tokens.Count > 0 && tokens[0].Type is CondToken.Kind.And or CondToken.Kind.Or)
            tokens.RemoveAt(0);

        // Remove trailing operators
        while (tokens.Count > 0 && tokens[^1].Type is CondToken.Kind.And or CondToken.Kind.Or)
            tokens.RemoveAt(tokens.Count - 1);

        // Remove double operators + insert AND between adjacent conditions
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            var cur = tokens[i];
            if (i > 0)
            {
                var prev = tokens[i - 1];
                // Two operators in a row → remove one
                if (cur.Type is CondToken.Kind.And or CondToken.Kind.Or &&
                    prev.Type is CondToken.Kind.And or CondToken.Kind.Or)
                {
                    tokens.RemoveAt(i);
                    continue;
                }
                // Two conditions in a row → insert AND between
                bool curIsCond = cur.Type is CondToken.Kind.Func or CondToken.Kind.Group;
                bool prevIsCond = prev.Type is CondToken.Kind.Func or CondToken.Kind.Group;
                if (curIsCond && prevIsCond)
                {
                    tokens.Insert(i, new CondToken { Type = CondToken.Kind.And });
                }
            }
        }
    }

    private static void RemoveToken(List<CondToken> tokens, int idx)
    {
        if (idx < 0 || idx >= tokens.Count) return;

        tokens.RemoveAt(idx);

        // Clean up orphan connector at start or double connectors
        while (tokens.Count > 0 && tokens[0].Type is CondToken.Kind.And or CondToken.Kind.Or)
            tokens.RemoveAt(0);
        for (int i = tokens.Count - 1; i >= 0; i--)
        {
            if (tokens[i].Type is CondToken.Kind.And or CondToken.Kind.Or)
            {
                if (i == tokens.Count - 1 || (i + 1 < tokens.Count && tokens[i + 1].Type is CondToken.Kind.And or CondToken.Kind.Or))
                    tokens.RemoveAt(i);
            }
        }
    }

    // ── Sync tokens → Text ─────────────────────────────────────

    private void SyncFromTokens(List<CondToken> tokens)
    {
        NormalizeTokens(tokens);

        var oldText = Text ?? "";
        if (!string.IsNullOrEmpty(oldText))
            _undoStack.Push(oldText);

        _updating = true;
        Text = SerializeTokens(tokens);
        _updating = false;
        Rebuild();
    }

    private static string SerializeTokens(List<CondToken> tokens)
    {
        var parts = new List<string>();
        foreach (var t in tokens)
        {
            if (t.Type == CondToken.Kind.And) { parts.Add("and"); continue; }
            if (t.Type == CondToken.Kind.Or) { parts.Add("or"); continue; }
            if (t.Type == CondToken.Kind.Group)
            {
                var inner = t.Children != null ? SerializeTokens(t.Children) : "";
                parts.Add($"({inner})");
                continue;
            }

            var sb = "";
            if (t.Negated) sb = "not ";
            sb += t.Args.Length > 0 ? $"{t.FuncName}({string.Join(",", t.Args)})" : $"{t.FuncName}()";
            parts.Add(sb);
        }
        return string.Join(" ", parts);
    }

    // ── Tokenizer ──────────────────────────────────────────────

    private static List<CondToken> Tokenize(string raw)
    {
        var tokens = new List<CondToken>();
        var remaining = raw.Trim();

        while (remaining.Length > 0)
        {
            remaining = remaining.TrimStart();
            if (remaining.Length == 0) break;

            // Operator
            if (remaining.StartsWith("and ", StringComparison.OrdinalIgnoreCase) ||
                remaining.StartsWith("and(", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new CondToken { Type = CondToken.Kind.And, Raw = "and" });
                remaining = remaining[3..].TrimStart();
                continue;
            }
            if (remaining.StartsWith("or ", StringComparison.OrdinalIgnoreCase) ||
                remaining.StartsWith("or(", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add(new CondToken { Type = CondToken.Kind.Or, Raw = "or" });
                remaining = remaining[2..].TrimStart();
                continue;
            }

            // Grouping parentheses → Group token with children
            if (remaining.StartsWith('(') && !System.Text.RegularExpressions.Regex.IsMatch(remaining, @"^\w+\("))
            {
                int d = 1, p = 1;
                while (p < remaining.Length && d > 0)
                {
                    if (remaining[p] == '(') d++;
                    else if (remaining[p] == ')') d--;
                    p++;
                }
                var inner = remaining[1..(p - 1)];
                remaining = p < remaining.Length ? remaining[p..] : "";

                tokens.Add(new CondToken
                {
                    Type = CondToken.Kind.Group,
                    Children = Tokenize(inner), // recurse
                    Raw = $"({inner})"
                });
                continue;
            }

            // Not prefix
            bool negated = false;
            if (remaining.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
            {
                negated = true;
                remaining = remaining[4..].TrimStart();
            }

            // Function call: Name(args) or Name()
            var m = System.Text.RegularExpressions.Regex.Match(remaining, @"^(\w+)\(");
            if (m.Success)
            {
                var funcName = m.Groups[1].Value;
                var afterName = remaining[m.Length..];

                // Find matching close paren (handle nested parens)
                int depth = 1, pos = 0;
                while (pos < afterName.Length && depth > 0)
                {
                    if (afterName[pos] == '(') depth++;
                    else if (afterName[pos] == ')') depth--;
                    if (depth > 0) pos++;
                }

                var argsStr = afterName[..pos];
                remaining = pos + 1 < afterName.Length ? afterName[(pos + 1)..] : "";

                // Split args respecting nested parens
                var args = SplitArgs(argsStr);

                tokens.Add(new CondToken
                {
                    Type = CondToken.Kind.Func,
                    Negated = negated,
                    FuncName = funcName,
                    Args = args,
                    Raw = $"{(negated ? "not " : "")}{funcName}({argsStr})"
                });
            }
            else
            {
                // Bare identifier (e.g. "TurnBased" without parens, or context.Target)
                int nextSpace = remaining.IndexOf(' ');
                var word = nextSpace >= 0 ? remaining[..nextSpace] : remaining;
                remaining = nextSpace >= 0 ? remaining[nextSpace..] : "";

                tokens.Add(new CondToken
                {
                    Type = CondToken.Kind.Func,
                    Negated = negated,
                    FuncName = word.TrimEnd('(', ')'),
                    Args = [],
                    Raw = word
                });
            }
        }

        return tokens;
    }

    private static string[] SplitArgs(string argsStr)
    {
        if (string.IsNullOrWhiteSpace(argsStr)) return [];

        var args = new List<string>();
        int depth = 0, start = 0;
        for (int i = 0; i < argsStr.Length; i++)
        {
            if (argsStr[i] == '(' || argsStr[i] == '{') depth++;
            else if (argsStr[i] == ')' || argsStr[i] == '}') depth--;
            else if (argsStr[i] == ',' && depth == 0)
            {
                args.Add(argsStr[start..i].Trim());
                start = i + 1;
            }
        }
        args.Add(argsStr[start..].Trim());
        return args.Where(a => !string.IsNullOrEmpty(a)).ToArray();
    }
}
