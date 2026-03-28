using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Localization;
using ParaTool.Core.Schema;

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
    private List<CondToken> _tokens = []; // current tokens for drag-drop

    private static readonly SolidColorBrush FgFunc = new(Color.Parse("#2ECC71"));
    private static readonly SolidColorBrush BgFunc = new(Color.Parse("#1A2ECC71"));
    private static readonly SolidColorBrush FgOp = new(Color.Parse("#F1C40F"));
    private static readonly SolidColorBrush BgOp = new(Color.Parse("#1AF1C40F"));
    private static readonly SolidColorBrush FgNot = new(Color.Parse("#E74C3C"));
    private static readonly SolidColorBrush BgNot = new(Color.Parse("#1AE74C3C"));
    private static SolidColorBrush FgMuted => Themes.ThemeBrushes.TextMuted;
    private static SolidColorBrush InputBg => Themes.ThemeBrushes.InputBg;

    public ConditionBlocksEditor()
    {
        Content = _panel;
        ClipToBounds = false;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty && !_updating) Rebuild();
        };
    }

    // ── Parsed token model ─────────────────────────────────────

    private sealed class CondToken
    {
        public enum Kind { Func, And, Or }
        public Kind Type;
        public bool Negated;       // "not" prefix
        public string FuncName = "";  // e.g. "InSurface"
        public string[] Args = [];    // e.g. ["'SurfaceWater'"]
        public string Raw = "";       // original text
    }

    // ── Rebuild UI from text ───────────────────────────────────

    private void Rebuild()
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
                var connLabel = token.Type == CondToken.Kind.And ? "AND" : "OR";
                var connBtn = new Button
                {
                    Content = connLabel,
                    FontSize = 10, FontWeight = FontWeight.Bold,
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
            else
            {
                element = BuildConditionChip(token, _tokens, idx);
            }

            // Drag-and-drop: hold and drag to reorder
            SetupDrag(element, idx);
            _panel.Children.Add(element);
        }

        AddPlusButton();
    }

    // ── Drag and drop ──────────────────────────────────────────

    private void SetupDrag(Control element, int tokenIdx)
    {
        element.Tag = tokenIdx;

        // Start drag on pointer press + move
        element.PointerPressed += async (s, e) =>
        {
            if (e.GetCurrentPoint(element).Properties.IsLeftButtonPressed && s is Control ctrl)
            {
                // Small delay to distinguish click from drag
                var startPos = e.GetPosition(_panel);

                void onMove(object? _, PointerEventArgs me)
                {
                    var pos = me.GetPosition(_panel);
                    if (Math.Abs(pos.X - startPos.X) > 8 || Math.Abs(pos.Y - startPos.Y) > 8)
                    {
                        ctrl.PointerMoved -= onMove;
                        var data = new DataObject();
                        data.Set("CondTokenIdx", tokenIdx);
                        ctrl.Opacity = 0.5;
                        DragDrop.DoDragDrop(me, data, DragDropEffects.Move);
                        ctrl.Opacity = 1;
                    }
                }
                ctrl.PointerMoved += onMove;
                ctrl.PointerReleased += (_, _) => ctrl.PointerMoved -= onMove;
            }
        };

        // Drop target
        DragDrop.SetAllowDrop(element, true);
        element.AddHandler(DragDrop.DropEvent, (_, e) =>
        {
            if (e.Data.Get("CondTokenIdx") is int fromIdx)
            {
                var toIdx = tokenIdx;
                if (fromIdx != toIdx && fromIdx >= 0 && fromIdx < _tokens.Count && toIdx >= 0 && toIdx < _tokens.Count)
                {
                    var moved = _tokens[fromIdx];
                    _tokens.RemoveAt(fromIdx);
                    var insertAt = toIdx > fromIdx ? toIdx - 1 : toIdx;
                    if (insertAt < 0) insertAt = 0;
                    if (insertAt > _tokens.Count) insertAt = _tokens.Count;
                    _tokens.Insert(insertAt, moved);
                    SyncFromTokens(_tokens);
                }
            }
        });
        element.AddHandler(DragDrop.DragOverEvent, (_, e) =>
        {
            e.DragEffects = DragDropEffects.Move;
        });
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
                Content = "NOT", FontSize = 9, FontWeight = FontWeight.Bold,
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
            Text = label, FontSize = 11, FontWeight = FontWeight.SemiBold,
            Foreground = FgFunc, VerticalAlignment = VerticalAlignment.Center,
        });

        // Parameter controls
        // Show params: use def if available, otherwise show raw args
        var paramCount = def?.Params.Length ?? 0;
        var argCount = token.Args.Length;
        var count = Math.Max(paramCount, argCount);

        for (int pi = 0; pi < count; pi++)
        {
            var param = pi < paramCount ? def!.Params[pi] : null;
            var argVal = pi < argCount ? token.Args[pi].Trim('\'', '"', ' ') : "";
            var paramIdx = pi;

            // Skip entity/target params — user doesn't set these
            if (param != null && param.Type == "entity") continue;

            if (param?.Type == "enum" && param.EnumValues != null)
            {
                var enumTumbler = new TumblerChipEditor
                {
                    Text = argVal, Items = param.EnumValues,
                    VerticalAlignment = VerticalAlignment.Center,
                };
                enumTumbler.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Text" && s is TumblerChipEditor tc)
                    {
                        token.Args[paramIdx] = $"'{tc.Text}'";
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
            else
            {
                // String/unknown — small TextBox
                var tb = new TextBox
                {
                    Text = argVal, FontSize = 10,
                    Padding = new Thickness(4, 1), MinWidth = 60,
                    Background = InputBg, CornerRadius = new CornerRadius(4),
                    VerticalAlignment = VerticalAlignment.Center,
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

        if (count == 0 && token.Args.Length > 0 && def == null)
        {
            // Unknown function — show raw args as text
            var argsText = string.Join(", ", token.Args);
            stack.Children.Add(new TextBlock
            {
                Text = $"({argsText})", FontSize = 10,
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
            Content = "×", FontSize = 10,
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

    // ── Add button with categorized menu ───────────────────────

    private void AddPlusButton()
    {
        var addBtn = new Button
        {
            Content = "+", FontSize = 12, FontWeight = FontWeight.Bold,
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

        // Group by category
        var groups = schema.Functions
            .Where(f => f.Category != "General" || f.Params.Length == 0) // skip complex helpers
            .GroupBy(f => f.Category)
            .OrderBy(g => g.Key);

        // Most used conditions at top level
        var favorites = new[] { "Enemy", "Ally", "Self", "Combat", "TurnBased",
            "HasStatus", "SpellId", "IsWeaponAttack", "IsSpellAttack", "IsMeleeAttack",
            "IsRangedWeaponAttack", "IsCritical", "IsMiss", "InSurface",
            "HasShieldEquipped", "Dead", "IsSpell", "IsCantrip", "HasPassive" };

        var isRu = Localization.Loc.Instance.Lang == "ru";

        foreach (var fav in favorites)
        {
            if (!schema.ByName.TryGetValue(fav, out var def)) continue;
            var item = new MenuItem { Header = ConditionLabels.GetLabel(fav, isRu), Tag = def };
            item.Click += (_, _) => AddCondition(def);
            menu.Items.Add(item);
        }

        menu.Items.Add(new Separator());

        // All by category
        foreach (var group in groups)
        {
            var sub = new MenuItem { Header = ConditionLabels.GetCategoryLabel(group.Key, isRu) };
            foreach (var def in group.OrderBy(d => d.Name))
            {
                var displayName = ConditionLabels.GetLabel(def.Name, isRu);
                var paramHint = def.Params.Length > 0
                    ? $" ({string.Join(", ", def.Params.Select(p => p.Name))})"
                    : "";
                var item = new MenuItem { Header = displayName + paramHint, Tag = def };
                item.Click += (_, _) => AddCondition(def);
                sub.Items.Add(item);
            }
            menu.Items.Add(sub);
        }

        menu.Open(this);
    }

    private void AddCondition(ConditionDef def)
    {
        // Build default args
        var args = def.Params.Select(p => p.Type switch
        {
            "enum" => $"'{p.EnumValues?.FirstOrDefault() ?? "None"}'",
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
        var parts = new List<string>();
        foreach (var t in tokens)
        {
            if (t.Type == CondToken.Kind.And) { parts.Add("and"); continue; }
            if (t.Type == CondToken.Kind.Or) { parts.Add("or"); continue; }

            var sb = "";
            if (t.Negated) sb = "not ";
            sb += t.Args.Length > 0 ? $"{t.FuncName}({string.Join(",", t.Args)})" : $"{t.FuncName}()";
            parts.Add(sb);
        }

        _updating = true;
        Text = string.Join(" ", parts);
        _updating = false;
        Rebuild();
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
