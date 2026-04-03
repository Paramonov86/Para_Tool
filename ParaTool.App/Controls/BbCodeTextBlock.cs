using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using ParaTool.App.Themes;

namespace ParaTool.App.Controls;

/// <summary>
/// TextBlock that renders BB-code as rich Inlines (bold, italic, colored spans).
/// Supports: [b], [i], [br], [status=X], [spell=X], [passive=X], [resource=X], [tip=X], [p1], [dp1]
/// </summary>
public class BbCodeTextBlock : TextBlock
{
    public static readonly StyledProperty<string?> BbTextProperty =
        AvaloniaProperty.Register<BbCodeTextBlock, string?>(nameof(BbText));

    public static readonly StyledProperty<string?> BbParamsProperty =
        AvaloniaProperty.Register<BbCodeTextBlock, string?>(nameof(BbParams));

    public string? BbText
    {
        get => GetValue(BbTextProperty);
        set => SetValue(BbTextProperty, value);
    }

    /// <summary>Semicolon-separated DescriptionParams, e.g. "DealDamage(1d4,Fire);3"</summary>
    public string? BbParams
    {
        get => GetValue(BbParamsProperty);
        set => SetValue(BbParamsProperty, value);
    }

    private static readonly SolidColorBrush StatusColor = new(Color.Parse("#C8A96E"));
    private static readonly SolidColorBrush SpellColor = new(Color.Parse("#C8A96E"));
    private static readonly SolidColorBrush PassiveColor = new(Color.Parse("#C8A96E"));
    private static readonly SolidColorBrush ResourceColor = new(Color.Parse("#C8A96E"));
    private static readonly SolidColorBrush TipColor = new(Color.Parse("#C8A96E"));
    private static readonly SolidColorBrush ParamColor = new(Color.Parse("#E67E22"));

    // BG3 damage type colors
    private static readonly Dictionary<string, SolidColorBrush> DamageColors = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Fire"] = new(Color.Parse("#E8602A")),
        ["Cold"] = new(Color.Parse("#48A8D0")),
        ["Lightning"] = new(Color.Parse("#60B0E8")),
        ["Thunder"] = new(Color.Parse("#8868C8")),
        ["Acid"] = new(Color.Parse("#50E828")),
        ["Poison"] = new(Color.Parse("#A8B840")),
        ["Necrotic"] = new(Color.Parse("#6AA85C")),
        ["Radiant"] = new(Color.Parse("#E8C838")),
        ["Psychic"] = new(Color.Parse("#C850C0")),
        ["Force"] = new(Color.Parse("#E03030")),
        ["Slashing"] = new(Color.Parse("#C0C0C0")),
        ["Piercing"] = new(Color.Parse("#C0C0C0")),
        ["Bludgeoning"] = new(Color.Parse("#C0C0C0")),
    };
    private static readonly SolidColorBrush HealColor = new(Color.Parse("#48D1CC"));

    public BbCodeTextBlock()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.Property == BbTextProperty || e.Property == BbParamsProperty)
                RenderBbCode();
        };
        AttachedToVisualTree += (_, _) => RenderBbCode();
    }

    private void RenderBbCode()
    {
        Inlines?.Clear();
        var raw = BbText;
        if (string.IsNullOrEmpty(raw)) return;

        Inlines ??= new InlineCollection();

        // Substitute DescriptionParams: [1] → resolved param, [dp1] → bold colored param
        var paramStr = BbParams;
        if (!string.IsNullOrEmpty(paramStr))
        {
            var paramParts = paramStr.Split(';', StringSplitOptions.TrimEntries);
            for (int pi = 0; pi < paramParts.Length; pi++)
            {
                var paramText = ResolveParam(paramParts[pi]);
                var idx = (pi + 1).ToString();
                // [dp1] → bold colored param, [p1] and [1] → param text
                raw = raw.Replace($"[dp{idx}]", $"[b]{paramText}[/b]");
                raw = raw.Replace($"[p{idx}]", paramText);
                raw = raw.Replace($"[{idx}]", paramText);
            }
        }

        // Tokenize BB-code into segments
        int pos = 0;
        var tagPattern = new Regex(@"\[(/?)([a-z]+)(?:=([^\]]*))?\]", RegexOptions.IgnoreCase);

        var boldStack = 0;
        var italicStack = 0;

        while (pos < raw.Length)
        {
            var match = tagPattern.Match(raw, pos);
            if (!match.Success)
            {
                // Rest is plain text
                AddRun(raw[pos..], boldStack > 0, italicStack > 0, null);
                break;
            }

            // Text before tag
            if (match.Index > pos)
                AddRun(raw[pos..match.Index], boldStack > 0, italicStack > 0, null);

            var isClosing = match.Groups[1].Value == "/";
            var tag = match.Groups[2].Value.ToLower();
            var arg = match.Groups[3].Value;

            pos = match.Index + match.Length;

            switch (tag)
            {
                case "b":
                    if (isClosing) boldStack = Math.Max(0, boldStack - 1);
                    else boldStack++;
                    break;
                case "i":
                    if (isClosing) italicStack = Math.Max(0, italicStack - 1);
                    else italicStack++;
                    break;
                case "br":
                    Inlines.Add(new LineBreak());
                    break;
                case "status" when !isClosing:
                    pos = AddTaggedSpan(raw, pos, "status", StatusColor, boldStack, italicStack);
                    break;
                case "spell" when !isClosing:
                    pos = AddTaggedSpan(raw, pos, "spell", SpellColor, boldStack, italicStack);
                    break;
                case "passive" when !isClosing:
                    pos = AddTaggedSpan(raw, pos, "passive", PassiveColor, boldStack, italicStack);
                    break;
                case "resource" when !isClosing:
                    pos = AddTaggedSpan(raw, pos, "resource", ResourceColor, boldStack, italicStack);
                    break;
                case "tip" when !isClosing:
                    pos = AddTaggedSpan(raw, pos, "tip", TipColor, boldStack, italicStack);
                    break;
                case "dmg" when !isClosing:
                    var dmgType = arg;
                    var dmgColor = DamageColors.GetValueOrDefault(dmgType, ParamColor);
                    pos = AddTaggedSpan(raw, pos, "dmg", dmgColor, boldStack, italicStack);
                    break;
                case "heal" when !isClosing:
                    pos = AddTaggedSpan(raw, pos, "heal", HealColor, boldStack, italicStack);
                    break;
                case "p" when !isClosing:
                    // no-op, just skip
                    break;
                case "dp" when !isClosing:
                    // no-op, just skip
                    break;
                default:
                    if (tag.StartsWith("p") && !isClosing && int.TryParse(tag[1..], out _))
                    {
                        AddRun($"[{tag[1..]}]", true, false, ParamColor);
                    }
                    else if (tag.StartsWith("dp") && !isClosing && int.TryParse(tag[2..], out _))
                    {
                        AddRun($"[{tag[2..]}]", true, false, ParamColor);
                    }
                    break;
            }
        }
    }

    /// <summary>Find content until [/tag] and add as colored span.</summary>
    private int AddTaggedSpan(string raw, int pos, string tag, SolidColorBrush color, int boldStack, int italicStack)
    {
        var closeTag = $"[/{tag}]";
        var closeIdx = raw.IndexOf(closeTag, pos, StringComparison.OrdinalIgnoreCase);
        if (closeIdx < 0)
        {
            // No closing tag — add rest as colored
            AddRun(raw[pos..], boldStack > 0, italicStack > 0, color);
            return raw.Length;
        }

        var content = raw[pos..closeIdx];
        AddRun(content, boldStack > 0, italicStack > 0, color);
        return closeIdx + closeTag.Length;
    }

    private void AddRun(string text, bool bold, bool italic, SolidColorBrush? color)
    {
        if (string.IsNullOrEmpty(text)) return;

        // Handle [p1] [dp1] etc in plain text
        text = Regex.Replace(text, @"\[dp?(\d+)\]", "[$1]");

        var run = new Run(text);
        if (bold) run.FontWeight = FontWeight.Bold;
        if (italic) run.FontStyle = FontStyle.Italic;
        if (color != null) run.Foreground = color;
        else run.Foreground = Foreground; // inherit

        Inlines!.Add(run);
    }

    // ── DescriptionParams resolution ───────────────────────────

    /// <summary>
    /// Resolve a DescriptionParams entry to display text.
    /// DealDamage(1d4,Fire) → "1~4 Fire Damage"
    /// DealDamage(,Fire) → " Fire Damage" (leading space)
    /// RegainHitPoints(2) → "2 hit points"
    /// Distance(3) → "3m / 10ft"
    /// Plain number → as-is
    /// </summary>
    private static string ResolveParam(string param)
    {
        param = param.Trim();

        // DealDamage(dice,Type) or DealDamage(,Type) → colored damage text
        if (param.StartsWith("DealDamage(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = param["DealDamage(".Length..].TrimEnd(')');
            var parts = inner.Split(',', 2, StringSplitOptions.TrimEntries);
            var dice = parts.Length > 0 ? parts[0] : "";
            var dmgType = parts.Length > 1 ? parts[1] : "";

            var diceDisplay = FormatDice(dice);
            // Use [dmg=Type] custom tag for colored rendering
            if (string.IsNullOrEmpty(diceDisplay))
                return $" [dmg={dmgType}]{dmgType} Damage[/dmg]";
            return $"[dmg={dmgType}]{diceDisplay} {dmgType} Damage[/dmg]";
        }

        // RegainHitPoints(amount) → teal heal color
        if (param.StartsWith("RegainHitPoints(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = param["RegainHitPoints(".Length..].TrimEnd(')');
            return $"[heal]{FormatDice(inner)} hit points[/heal]";
        }

        // GainTemporaryHitPoints(amount) → teal heal color
        if (param.StartsWith("GainTemporaryHitPoints(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = param["GainTemporaryHitPoints(".Length..].TrimEnd(')');
            return $"[heal]{FormatDice(inner)} temporary hit points[/heal]";
        }

        // Distance(n)
        if (param.StartsWith("Distance(", StringComparison.OrdinalIgnoreCase))
        {
            var inner = param["Distance(".Length..].TrimEnd(')');
            if (double.TryParse(inner, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var m))
                return $"{m}m / {Math.Round(m * 3.28)}ft";
            return inner;
        }

        return param; // plain number or unknown
    }

    /// <summary>Format dice: "1d4" → "1~4", "2d6" → "2~12", number → as-is.</summary>
    private static string FormatDice(string dice)
    {
        if (string.IsNullOrEmpty(dice)) return "";
        var m = System.Text.RegularExpressions.Regex.Match(dice, @"^(\d+)d(\d+)$");
        if (m.Success)
        {
            var n = int.Parse(m.Groups[1].Value);
            var d = int.Parse(m.Groups[2].Value);
            return $"{n}~{n * d}";
        }
        return dice;
    }
}
