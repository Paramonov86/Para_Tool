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

    public string? BbText
    {
        get => GetValue(BbTextProperty);
        set => SetValue(BbTextProperty, value);
    }

    private static readonly SolidColorBrush StatusColor = new(Color.Parse("#E74C3C"));
    private static readonly SolidColorBrush SpellColor = new(Color.Parse("#9B59B6"));
    private static readonly SolidColorBrush PassiveColor = new(Color.Parse("#C8A96E"));
    private static readonly SolidColorBrush ResourceColor = new(Color.Parse("#F1C40F"));
    private static readonly SolidColorBrush TipColor = new(Color.Parse("#C8A96E"));
    private static readonly SolidColorBrush ParamColor = new(Color.Parse("#E67E22"));

    public BbCodeTextBlock()
    {
        PropertyChanged += (_, e) =>
        {
            if (e.Property == BbTextProperty)
                RenderBbCode();
        };
    }

    private void RenderBbCode()
    {
        Inlines?.Clear();
        var raw = BbText;
        if (string.IsNullOrEmpty(raw)) return;

        Inlines ??= new InlineCollection();

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
}
