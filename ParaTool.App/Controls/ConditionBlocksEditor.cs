using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.Core.Schema;

namespace ParaTool.App.Controls;

/// <summary>
/// Renders condition strings as colored chips.
/// "Enemy() and not Dead()" → [Is Enemy] [AND] [NOT] [Not dead]
/// </summary>
public class ConditionBlocksEditor : UserControl
{
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<ConditionBlocksEditor, string?>(nameof(Text), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    private readonly WrapPanel _panel = new() { Orientation = Orientation.Horizontal };
    private bool _updating;

    private static readonly SolidColorBrush BgFunc = new(Color.Parse("#1A2ECC71"));
    private static readonly SolidColorBrush FgFunc = new(Color.Parse("#2ECC71"));
    private static readonly SolidColorBrush BgOp = new(Color.Parse("#1AF1C40F"));
    private static readonly SolidColorBrush FgOp = new(Color.Parse("#F1C40F"));
    private static readonly SolidColorBrush BgNot = new(Color.Parse("#1AE74C3C"));
    private static readonly SolidColorBrush FgNot = new(Color.Parse("#E74C3C"));
    private static readonly SolidColorBrush BgDefault = new(Color.Parse("#252330"));
    private static readonly SolidColorBrush FgMuted = new(Color.Parse("#8A8494"));

    public ConditionBlocksEditor()
    {
        Content = _panel;
        PropertyChanged += (_, e) =>
        {
            if (e.Property == TextProperty && !_updating) Rebuild();
        };
    }

    private void Rebuild()
    {
        _panel.Children.Clear();
        var raw = Text ?? "";
        if (string.IsNullOrWhiteSpace(raw)) { AddPlusButton(); return; }

        // Tokenize: split by " and ", " or ", "not " while preserving functions
        var tokens = Tokenize(raw);

        foreach (var token in tokens)
        {
            var trimmed = token.Trim();
            if (string.IsNullOrEmpty(trimmed)) continue;

            var (bg, fg, label) = trimmed.ToLowerInvariant() switch
            {
                "and" => (BgOp, FgOp, "AND"),
                "or" => (BgOp, FgOp, "OR"),
                "not" => (BgNot, FgNot, "NOT"),
                _ => ResolveCondition(trimmed)
            };

            var chip = new Border
            {
                Child = new TextBlock
                {
                    Text = label, FontSize = 11, FontWeight = FontWeight.SemiBold,
                    Foreground = fg, VerticalAlignment = VerticalAlignment.Center,
                },
                Background = bg, CornerRadius = new CornerRadius(8),
                Padding = new Thickness(8, 3), Margin = new Thickness(2),
            };
            _panel.Children.Add(chip);
        }

        AddPlusButton();
    }

    private (SolidColorBrush bg, SolidColorBrush fg, string label) ResolveCondition(string raw)
    {
        // Try to find in mapping
        foreach (var (pattern, label) in BoostMapping.Conditions)
        {
            var funcName = pattern.Split('(')[0];
            if (raw.StartsWith(funcName, StringComparison.OrdinalIgnoreCase))
                return (BgFunc, FgFunc, label.Split('/')[0].Trim());
        }

        // Check if it's a HasStatus call
        if (raw.StartsWith("HasStatus(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = raw["HasStatus(".Length..].TrimEnd(')');
            return (BgFunc, FgFunc, $"Has Status: {inner.Trim('\'')}");
        }

        if (raw.StartsWith("SpellId(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = raw["SpellId(".Length..].TrimEnd(')');
            return (BgFunc, FgFunc, $"Spell: {inner.Trim('\'')}");
        }

        return (BgDefault, FgMuted, raw);
    }

    private static List<string> Tokenize(string raw)
    {
        var tokens = new List<string>();
        // Simple tokenizer: split by " and " and " or " and standalone "not "
        var remaining = raw;
        while (remaining.Length > 0)
        {
            remaining = remaining.TrimStart();
            if (remaining.StartsWith("and ", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("and");
                remaining = remaining[4..];
            }
            else if (remaining.StartsWith("or ", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("or");
                remaining = remaining[3..];
            }
            else if (remaining.StartsWith("not ", StringComparison.OrdinalIgnoreCase))
            {
                tokens.Add("not");
                remaining = remaining[4..];
            }
            else
            {
                // Find next operator
                int nextAnd = remaining.IndexOf(" and ", StringComparison.OrdinalIgnoreCase);
                int nextOr = remaining.IndexOf(" or ", StringComparison.OrdinalIgnoreCase);
                int nextBreak = (nextAnd >= 0 && nextOr >= 0) ? Math.Min(nextAnd, nextOr)
                    : nextAnd >= 0 ? nextAnd : nextOr >= 0 ? nextOr : -1;

                if (nextBreak >= 0)
                {
                    tokens.Add(remaining[..nextBreak].Trim());
                    remaining = remaining[nextBreak..];
                }
                else
                {
                    tokens.Add(remaining.Trim());
                    break;
                }
            }
        }
        return tokens;
    }

    private void AddPlusButton()
    {
        var addBtn = new Button
        {
            Content = "+", FontSize = 12, FontWeight = FontWeight.Bold,
            Padding = new Thickness(6, 2), Margin = new Thickness(2),
            CornerRadius = new CornerRadius(8),
            Background = BgDefault, Foreground = FgFunc,
            BorderThickness = new Thickness(1), BorderBrush = FgFunc,
            Cursor = new Cursor(StandardCursorType.Hand),
        };
        addBtn.Click += OnAddClick;
        _panel.Children.Add(addBtn);
    }

    private void OnAddClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var menu = new ContextMenu();
        foreach (var (pattern, label) in BoostMapping.Conditions)
        {
            var displayLabel = label.Split('/')[0].Trim();
            var item = new MenuItem { Header = displayLabel, Tag = pattern.Split('(')[0] + "()" };
            item.Click += (_, _) =>
            {
                var current = Text ?? "";
                var addition = item.Tag?.ToString() ?? "";
                _updating = true;
                Text = string.IsNullOrEmpty(current) ? addition : $"{current} and {addition}";
                _updating = false;
                Rebuild();
            };
            menu.Items.Add(item);
        }
        menu.Open(this);
    }
}
