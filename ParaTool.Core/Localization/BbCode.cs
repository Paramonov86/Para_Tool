using System.Text;
using System.Text.RegularExpressions;

namespace ParaTool.Core.Localization;

/// <summary>
/// Converts between BB-code (human-readable, used in editor) and
/// BG3 XML-escaped localization format (stored in .loca.xml files).
///
/// BB-code syntax:
///   [b]bold text[/b]                          → &lt;b&gt;bold text&lt;/b&gt;
///   [br]                                       → &lt;br&gt;
///   [tip=ArmourClass]AC[/tip]                  → &lt;LSTag Tooltip="ArmourClass"&gt;AC&lt;/LSTag&gt;
///   [status=STUNNED]Stunned[/status]            → &lt;LSTag Type="Status" Tooltip="STUNNED"&gt;Stunned&lt;/LSTag&gt;
///   [spell=Shout_Fireball]Fireball[/spell]      → &lt;LSTag Type="Spell" Tooltip="Shout_Fireball"&gt;Fireball&lt;/LSTag&gt;
///   [passive=MY_PASSIVE]name[/passive]          → &lt;LSTag Type="Passive" Tooltip="MY_PASSIVE"&gt;name&lt;/LSTag&gt;
///   [resource=KiPoint]Ki Point[/resource]       → &lt;LSTag Type="ActionResource" Tooltip="KiPoint"&gt;Ki Point&lt;/LSTag&gt;
///   [p1], [p2], [p3]                           → [1], [2], [3] (DescriptionParams)
///   [dp1], [dp2]                               → &lt;b&gt;[1]&lt;/b&gt;, &lt;b&gt;[2]&lt;/b&gt; (bold param for damage)
///   [img=SoftWarning]                          → &lt;LSTag Type="Image" Info="SoftWarning"/&gt;
/// Tags nest: [b][status=WET]wet[/status][/b] → &lt;b&gt;&lt;LSTag …&gt;wet&lt;/LSTag&gt;&lt;/b&gt;.
/// </summary>
public static partial class BbCode
{
    /// <summary>
    /// Convert BB-code to BG3 XML-escaped localization string.
    /// </summary>
    public static string ToBg3Xml(string bbcode)
    {
        var sb = new StringBuilder(bbcode.Length * 2);
        int pos = 0;

        while (pos < bbcode.Length)
        {
            if (bbcode[pos] == '[')
            {
                var tag = TryParseTag(bbcode, pos);
                if (tag != null)
                {
                    sb.Append(tag.Value.xml);
                    pos = tag.Value.end;
                    continue;
                }
            }

            // Escape regular XML characters. Literal line breaks (the user pressing
            // Enter in the description box) must become &lt;br&gt; — BG3 collapses raw
            // newlines to a space, so otherwise multi-line text renders on one line.
            sb.Append(bbcode[pos] switch
            {
                '<' => "&lt;",
                '>' => "&gt;",
                '&' => "&amp;",
                '\n' => "&lt;br&gt;",
                '\r' => "",
                _ => bbcode[pos].ToString()
            });
            pos++;
        }

        return sb.ToString();
    }

    /// <summary>
    /// Convert BG3 XML-escaped localization string back to BB-code for editing.
    /// </summary>
    public static string FromBg3Xml(string bg3xml)
    {
        // First unescape XML entities, in one pass so "&amp;lt;" stays a literal "&lt;". All of
        // them: an entity left escaped (&apos; in AMP's texts) is escaped again on the way back
        // and the game shows "&apos;".
        var text = EntityRegex().Replace(bg3xml, m => m.Groups[1].Value switch
        {
            "lt" => "<",
            "gt" => ">",
            "amp" => "&",
            "apos" => "'",
            "quot" => "\"",
            var num when num[1] is 'x' or 'X' => DecodeCodePoint(num[2..], 16, m.Value),
            var num => DecodeCodePoint(num[1..], 10, m.Value),
        });

        // Bold params must run BEFORE the generic bold regex — otherwise
        // <b>[1]</b> is rewritten to [b][1][/b] first and the param tag is lost.
        text = BoldParamRegex().Replace(text, "[dp$1]");

        // Convert HTML/LSTag back to BB-code
        text = BoldRegex().Replace(text, "[b]$1[/b]");
        text = ItalicRegex().Replace(text, "[i]$1[/i]");
        text = text.Replace("<br>", "[br]");

        // LSTag self-closing with Image type: <LSTag Type="Image" Info="X"/>
        text = LsTagImageRegex().Replace(text, m =>
        {
            var info = m.Groups[1].Value;
            return $"[img={info}]";
        });

        // LSTag with Type
        text = LsTagTypedRegex().Replace(text, m =>
        {
            var type = m.Groups[1].Value;
            var tooltip = m.Groups[2].Value;
            var content = m.Groups[3].Value;
            return type switch
            {
                "Status" => $"[status={tooltip}]{content}[/status]",
                "Spell" => $"[spell={tooltip}]{content}[/spell]",
                "Passive" => $"[passive={tooltip}]{content}[/passive]",
                "ActionResource" => $"[resource={tooltip}]{content}[/resource]",
                // Other types (Skills, Ability, etc.) → encode Type in arg for round-trip
                _ => $"[tip={type}:{tooltip}]{content}[/tip]"
            };
        });

        // LSTag without Type (tooltip only)
        text = LsTagSimpleRegex().Replace(text, "[tip=$1]$2[/tip]");

        return text;
    }

    /// <summary>
    /// Convert BB-code to "visual" HTML for preview rendering (Avalonia WebView or TextBlock).
    /// Returns simplified HTML with inline styles.
    /// </summary>
    /// <summary>Strip all BB-code tags, leaving plain text content.</summary>
    public static string StripBbTags(string bbcode)
    {
        if (string.IsNullOrEmpty(bbcode)) return bbcode;
        var text = bbcode;
        // Remove closing tags
        text = Regex.Replace(text, @"\[/[a-z:]+\]", "", RegexOptions.IgnoreCase);
        // Remove self-closing image tags: [img=X]
        text = Regex.Replace(text, @"\[img=[^\]]*\]", "", RegexOptions.IgnoreCase);
        // Remove opening tags with args: [tag=arg]
        text = Regex.Replace(text, @"\[[a-z:]+=[^\]]*\]", "", RegexOptions.IgnoreCase);
        // Remove simple tags: [b], [i], [br]
        text = Regex.Replace(text, @"\[[a-z]+\]", "", RegexOptions.IgnoreCase);
        // Remove param tags: [p1], [dp1]
        text = Regex.Replace(text, @"\[d?p(\d+)\]", "[$1]");
        return text.Trim();
    }

    public static string ToPreviewHtml(string bbcode)
    {
        var sb = new StringBuilder();

        // Process BB tags
        var text = bbcode;
        text = Regex.Replace(text, @"\[b\](.*?)\[/b\]", "<b>$1</b>", RegexOptions.Singleline);
        text = text.Replace("[br]", "<br>");

        // LSTag → colored tooltip-styled spans
        text = Regex.Replace(text, @"\[tip=([^\]]+)\](.*?)\[/tip\]",
            "<span style=\"color:#87CEEB;text-decoration:underline\">$2</span>");
        text = Regex.Replace(text, @"\[status=([^\]]+)\](.*?)\[/status\]",
            "<span style=\"color:#E74C3C;text-decoration:underline\">$2</span>");
        text = Regex.Replace(text, @"\[spell=([^\]]+)\](.*?)\[/spell\]",
            "<span style=\"color:#9B59B6;text-decoration:underline\">$2</span>");
        text = Regex.Replace(text, @"\[passive=([^\]]+)\](.*?)\[/passive\]",
            "<span style=\"color:#2ECC71;text-decoration:underline\">$2</span>");
        text = Regex.Replace(text, @"\[resource=([^\]]+)\](.*?)\[/resource\]",
            "<span style=\"color:#F1C40F;text-decoration:underline\">$2</span>");

        // Params
        text = Regex.Replace(text, @"\[dp(\d+)\]",
            "<b style=\"color:#E67E22\">[${1}]</b>");
        text = Regex.Replace(text, @"\[p(\d+)\]", "[${1}]");

        sb.Append(text);
        return sb.ToString();
    }

    private static (string xml, int end)? TryParseTag(string text, int start)
    {
        // Quick check
        if (start >= text.Length || text[start] != '[') return null;

        // Self-closing tags
        if (TryMatchSimple(text, start, "br", out var brEnd))
            return ("&lt;br&gt;", brEnd);

        // Param shorthand: [p1] → [1], [dp1] → <b>[1]</b>
        var dpMatch = DpTagRegex().Match(text, start);
        if (dpMatch.Success && dpMatch.Index == start)
            return ($"&lt;b&gt;[{dpMatch.Groups[1].Value}]&lt;/b&gt;", start + dpMatch.Length);

        var pMatch = PTagRegex().Match(text, start);
        if (pMatch.Success && pMatch.Index == start)
            return ($"[{pMatch.Groups[1].Value}]", start + pMatch.Length);

        // Inline icon: [img=SoftWarning] → <LSTag Type="Image" Info="SoftWarning"/>
        var imgMatch = ImgTagRegex().Match(text, start);
        if (imgMatch.Success && imgMatch.Index == start)
            return ($"&lt;LSTag Type=\"Image\" Info=\"{imgMatch.Groups[1].Value}\"/&gt;", start + imgMatch.Length);

        // Paired tags
        if (TryMatchPaired(text, start, "i", null, null, out var iXml, out var iEnd))
            return (iXml, iEnd);

        if (TryMatchPaired(text, start, "b", null, null, out var bXml, out var bEnd))
            return (bXml, bEnd);

        if (TryMatchLsTag(text, start, "tip", null, out var tipXml, out var tipEnd))
            return (tipXml, tipEnd);
        if (TryMatchLsTag(text, start, "status", "Status", out var stXml, out var stEnd))
            return (stXml, stEnd);
        if (TryMatchLsTag(text, start, "spell", "Spell", out var spXml, out var spEnd))
            return (spXml, spEnd);
        if (TryMatchLsTag(text, start, "passive", "Passive", out var paXml, out var paEnd))
            return (paXml, paEnd);
        if (TryMatchLsTag(text, start, "resource", "ActionResource", out var resXml, out var resEnd))
            return (resXml, resEnd);

        return null;
    }

    private static bool TryMatchSimple(string text, int start, string tag, out int end)
    {
        var expected = $"[{tag}]";
        end = start;
        if (text.AsSpan(start).StartsWith(expected))
        {
            end = start + expected.Length;
            return true;
        }
        return false;
    }

    private static bool TryMatchPaired(string text, int start, string tag,
        string? xmlOpen, string? xmlClose, out string xml, out int end)
    {
        xml = "";
        end = start;

        var open = $"[{tag}]";
        var close = $"[/{tag}]";
        if (!text.AsSpan(start).StartsWith(open)) return false;

        int contentStart = start + open.Length;
        int closeIdx = FindClose(text, contentStart, open, close);
        if (closeIdx < 0) return false;

        // The content is BB-code too: the game nests markup (<b><LSTag …>…</LSTag></b>,
        // <i>(… <b>undead</b>)</i>), and escaping it verbatim put the inner tags into the
        // game's text as "[status=…]" or "[b]".
        var content = text[contentStart..closeIdx];
        xml = $"&lt;{xmlOpen ?? tag}&gt;{ToBg3Xml(content)}&lt;/{xmlClose ?? tag}&gt;";
        end = closeIdx + close.Length;
        return true;
    }

    private static bool TryMatchLsTag(string text, int start, string bbTag, string? lsType,
        out string xml, out int end)
    {
        xml = "";
        end = start;

        // Match [tag=VALUE]content[/tag]
        var pattern = $"[{bbTag}=";
        if (!text.AsSpan(start).StartsWith(pattern)) return false;

        int valStart = start + pattern.Length;
        int valEnd = text.IndexOf(']', valStart);
        if (valEnd < 0) return false;

        var tooltip = text[valStart..valEnd];
        int contentStart = valEnd + 1;

        var close = $"[/{bbTag}]";
        int closeIdx = FindClose(text, contentStart, pattern, close);
        if (closeIdx < 0) return false;

        var content = ToBg3Xml(text[contentStart..closeIdx]);

        if (lsType != null)
            xml = $"&lt;LSTag Type=\"{lsType}\" Tooltip=\"{tooltip}\"&gt;{content}&lt;/LSTag&gt;";
        else if (tooltip.Contains(':'))
        {
            // Round-trip encoded type: [tip=Skills:Perception] → <LSTag Type="Skills" Tooltip="Perception">
            var colonIdx = tooltip.IndexOf(':');
            var encodedType = tooltip[..colonIdx];
            var encodedTooltip = tooltip[(colonIdx + 1)..];
            xml = $"&lt;LSTag Type=\"{encodedType}\" Tooltip=\"{encodedTooltip}\"&gt;{content}&lt;/LSTag&gt;";
        }
        else
            xml = $"&lt;LSTag Tooltip=\"{tooltip}\"&gt;{content}&lt;/LSTag&gt;";

        end = closeIdx + close.Length;
        return true;
    }

    private static string DecodeCodePoint(string digits, int radix, string original)
    {
        try
        {
            var cp = Convert.ToInt32(digits, radix);
            return cp is > 0 and <= 0x10FFFF and not (>= 0xD800 and <= 0xDFFF) ? char.ConvertFromUtf32(cp) : original;
        }
        catch (Exception e) when (e is FormatException or OverflowException)
        {
            return original;
        }
    }

    /// <summary>
    /// Index of the close tag that ends a tag whose content starts at <paramref name="from"/>,
    /// past any pair of the same tag nested inside; -1 when it is never closed.
    /// </summary>
    private static int FindClose(string text, int from, string openPrefix, string close)
    {
        int depth = 0;
        for (int i = from; i < text.Length; i++)
        {
            if (text[i] != '[') continue;
            if (string.CompareOrdinal(text, i, close, 0, close.Length) == 0)
            {
                if (depth == 0) return i;
                depth--;
            }
            else if (string.CompareOrdinal(text, i, openPrefix, 0, openPrefix.Length) == 0)
                depth++;
        }
        return -1;
    }

    [GeneratedRegex(@"\[dp(\d+)\]")]
    private static partial Regex DpTagRegex();

    [GeneratedRegex(@"\[p(\d+)\]")]
    private static partial Regex PTagRegex();

    [GeneratedRegex(@"&(lt|gt|amp|apos|quot|#[0-9]{1,7}|#[xX][0-9a-fA-F]{1,6});")]
    private static partial Regex EntityRegex();

    [GeneratedRegex(@"\[img=([^\]\[]*)\]")]
    private static partial Regex ImgTagRegex();

    [GeneratedRegex(@"<b>(.*?)</b>", RegexOptions.Singleline)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"<i>(.*?)</i>", RegexOptions.Singleline)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"<LSTag\s+Type=""Image""\s+Info=""([^""]*)""\s*/>", RegexOptions.Singleline)]
    private static partial Regex LsTagImageRegex();

    [GeneratedRegex(@"<LSTag Type=""(\w+)"" Tooltip=""([^""]+)"">(.*?)</LSTag>", RegexOptions.Singleline)]
    private static partial Regex LsTagTypedRegex();

    [GeneratedRegex(@"<LSTag Tooltip=""([^""]+)"">(.*?)</LSTag>", RegexOptions.Singleline)]
    private static partial Regex LsTagSimpleRegex();

    [GeneratedRegex(@"<b>\[(\d+)\]</b>")]
    private static partial Regex BoldParamRegex();
}
