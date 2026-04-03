using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using ParaTool.Core.Localization;

namespace ParaTool.App.Converters;

/// <summary>
/// Converts BG3 XML-escaped localization text into Avalonia InlineCollection
/// for rich rendering in TextBlock.
///
/// Flow: BG3 loca XML → BbCode.FromBg3Xml() → BB-code → parse → Inlines
/// </summary>
public static partial class BbCodeRenderer
{
    private static readonly SolidColorBrush TooltipColor = new(Color.Parse("#87CEEB"));   // light blue
    private static readonly SolidColorBrush StatusColor = new(Color.Parse("#E74C3C"));    // red
    private static readonly SolidColorBrush SpellColor = new(Color.Parse("#9B59B6"));     // purple
    private static readonly SolidColorBrush PassiveColor = new(Color.Parse("#2ECC71"));   // green
    private static readonly SolidColorBrush ResourceColor = new(Color.Parse("#F1C40F"));  // yellow
    private static readonly SolidColorBrush DamageParamColor = new(Color.Parse("#E67E22"));// orange
    private static readonly SolidColorBrush ImageTagColor = new(Color.Parse("#F39C12"));  // warm yellow
    private static readonly SolidColorBrush DefaultText = new(Color.Parse("#C8B8DB"));    // secondary

    // Known Image Info → icon asset file name mapping
    private static readonly Dictionary<string, string> ImageToAsset = new(StringComparer.OrdinalIgnoreCase)
    {
        // Warnings
        ["SoftWarning"] = "ico_warningSoft",
        ["Warning"] = "ico_warning",
        // Tooltip misc
        ["Concentration"] = "ico_concentration",
        ["Coin"] = "ico_coin",
        ["AC"] = "ico_AC",
        ["Duration"] = "ico_duration",
        ["Hourglass"] = "ico_hourglass",
        ["Recharge"] = "ico_recharge",
        ["Range"] = "ico_range",
        ["Reach"] = "ico_reach",
        ["Radius"] = "ico_radius",
        ["Target"] = "ico_target",
        ["Type"] = "ico_type",
        ["Proficiency"] = "ico_proficiency",
        ["Finesse"] = "ico_finesse",
        ["Throwable"] = "ico_throwable",
        ["Dippable"] = "ico_dippable",
        ["MagicalProperties"] = "ico_magicalProperties",
        ["CampSupplies"] = "ico_campSupplies",
        ["BonusActionPoint"] = "ico_bonusActionPointTT",
        ["ReactionPoint"] = "ico_reactionPointTT",
        ["SpellSlot"] = "ico_spellSlotTT",
        ["RollAttack"] = "ico_roll_attack",
        ["RollSave"] = "ico_roll_save",
        // Shared icons
        ["Gold"] = "ico_gold",
        ["Advantage"] = "ico_advantage",
        ["Disadvantage"] = "ico_disadvantage",
        ["Healing"] = "ico_healing",
        ["Health"] = "ico_health",
        ["Speed"] = "ico_speed",
        ["Inspiration"] = "ico_inspiration",
        ["Spellbook"] = "ico_spellbook",
        ["Upcasting"] = "ico_upcasting",
        // Damage types
        ["DamageAcid"] = "ico_dmg_acid",
        ["DamageBludgeoning"] = "ico_dmg_blunt",
        ["DamageCold"] = "ico_dmg_cold",
        ["DamageFire"] = "ico_dmg_fire",
        ["DamageForce"] = "ico_dmg_force",
        ["DamageLightning"] = "ico_dmg_lightning",
        ["DamageNecrotic"] = "ico_dmg_necrotic",
        ["DamagePiercing"] = "ico_dmg_piercing",
        ["DamagePoison"] = "ico_dmg_poison",
        ["DamagePsychic"] = "ico_dmg_psychic",
        ["DamageRadiant"] = "ico_dmg_radiant",
        ["DamageSlashing"] = "ico_dmg_slashing",
        ["DamageThunder"] = "ico_dmg_thunder",
        // Resources
        ["ActionPoint"] = "ActionPoint",
        ["BonusAction"] = "BonusActionPoint",
        ["Reaction"] = "ReactionActionPoint",
        ["Movement"] = "Movement",
        ["KiPoint"] = "KiPoint",
        ["SorceryPoint"] = "SorceryPoint",
        ["Rage"] = "Rage",
        ["BardicInspiration"] = "BardicInspiration",
        ["WildShape"] = "WildShape",
        ["ChannelDivinity"] = "ChannelDivinity",
        ["SuperiorityDie"] = "SuperiorityDie",
    };

    // Fallback Unicode symbols for unmapped Image tags
    private static readonly Dictionary<string, string> ImageSymbols = new(StringComparer.OrdinalIgnoreCase)
    {
        ["SoftWarning"] = "\u26A0",
        ["Warning"] = "\u26A0",
        ["Concentration"] = "\u25C9",
        ["Advantage"] = "\u2191",
        ["Disadvantage"] = "\u2193",
    };

    private static readonly Dictionary<string, Bitmap?> _iconCache = new(StringComparer.OrdinalIgnoreCase);

    private static Bitmap? LoadTooltipIcon(string assetName)
    {
        if (_iconCache.TryGetValue(assetName, out var cached))
            return cached;

        try
        {
            // Avalonia 11: load AvaloniaResource via assembly manifest
            var asm = typeof(BbCodeRenderer).Assembly;
            // AvaloniaResource items are embedded with dotted path: ParaTool.App.Assets.TooltipIcons.name.png
            var resName = $"ParaTool.App.Assets.TooltipIcons.{assetName}.png";
            using var stream = asm.GetManifestResourceStream(resName);
            if (stream == null) { _iconCache[assetName] = null; return null; }
            var bmp = new Bitmap(stream);
            _iconCache[assetName] = bmp;
            return bmp;
        }
        catch
        {
            _iconCache[assetName] = null;
            return null;
        }
    }

    /// <summary>
    /// Render BG3 loca XML-escaped text to Avalonia Inlines.
    /// </summary>
    public static InlineCollection RenderBg3Xml(string bg3xml)
    {
        var bbcode = BbCode.FromBg3Xml(bg3xml);
        return RenderBbCode(bbcode);
    }

    /// <summary>
    /// Render BB-code string to Avalonia Inlines.
    /// </summary>
    public static InlineCollection RenderBbCode(string bbcode)
    {
        var inlines = new InlineCollection();
        ParseBbCodeInto(inlines, bbcode);
        return inlines;
    }

    private static void ParseBbCodeInto(InlineCollection inlines, string text)
    {
        int pos = 0;
        while (pos < text.Length)
        {
            if (text[pos] == '[')
            {
                // Try [b]...[/b]
                var boldMatch = BoldRegex().Match(text, pos);
                if (boldMatch.Success && boldMatch.Index == pos)
                {
                    inlines.Add(new Run(boldMatch.Groups[1].Value) { FontWeight = FontWeight.Bold, Foreground = DefaultText });
                    pos = boldMatch.Index + boldMatch.Length;
                    continue;
                }

                // Try [i]...[/i]
                var italicMatch = ItalicRegex().Match(text, pos);
                if (italicMatch.Success && italicMatch.Index == pos)
                {
                    inlines.Add(new Run(italicMatch.Groups[1].Value) { FontStyle = FontStyle.Italic, Foreground = DefaultText });
                    pos = italicMatch.Index + italicMatch.Length;
                    continue;
                }

                // Try [br]
                if (text.AsSpan(pos).StartsWith("[br]"))
                {
                    inlines.Add(new LineBreak());
                    pos += 4;
                    continue;
                }

                // Try [tip=X]text[/tip]
                var tipMatch = TipRegex().Match(text, pos);
                if (tipMatch.Success && tipMatch.Index == pos)
                {
                    AddColoredRun(inlines, tipMatch.Groups[2].Value, TooltipColor, true);
                    pos += tipMatch.Length;
                    continue;
                }

                // Try [status=X]text[/status]
                var statusMatch = StatusRegex().Match(text, pos);
                if (statusMatch.Success && statusMatch.Index == pos)
                {
                    AddColoredRun(inlines, statusMatch.Groups[2].Value, StatusColor, true);
                    pos += statusMatch.Length;
                    continue;
                }

                // Try [spell=X]text[/spell]
                var spellMatch = SpellRegex().Match(text, pos);
                if (spellMatch.Success && spellMatch.Index == pos)
                {
                    AddColoredRun(inlines, spellMatch.Groups[2].Value, SpellColor, true);
                    pos += spellMatch.Length;
                    continue;
                }

                // Try [passive=X]text[/passive]
                var passiveMatch = PassiveRegex().Match(text, pos);
                if (passiveMatch.Success && passiveMatch.Index == pos)
                {
                    AddColoredRun(inlines, passiveMatch.Groups[2].Value, PassiveColor, true);
                    pos += passiveMatch.Length;
                    continue;
                }

                // Try [resource=X]text[/resource]
                var resMatch = ResourceRegex().Match(text, pos);
                if (resMatch.Success && resMatch.Index == pos)
                {
                    AddColoredRun(inlines, resMatch.Groups[2].Value, ResourceColor, true);
                    pos += resMatch.Length;
                    continue;
                }

                // Try [img=X] — image tag (rendered as inline icon or fallback symbol)
                var imgMatch = ImgRegex().Match(text, pos);
                if (imgMatch.Success && imgMatch.Index == pos)
                {
                    var info = imgMatch.Groups[1].Value;
                    bool added = false;

                    // Try to load real icon
                    if (ImageToAsset.TryGetValue(info, out var assetName))
                    {
                        var bmp = LoadTooltipIcon(assetName);
                        if (bmp != null)
                        {
                            var iconSize = Services.FontScale.Of(14);
                            var img = new Image { Source = bmp, Width = iconSize, Height = iconSize };
                            img.Margin = new Thickness(1, 0);
                            inlines.Add(new InlineUIContainer(img));
                            added = true;
                        }
                    }

                    // Fallback to Unicode symbol
                    if (!added)
                    {
                        var symbol = ImageSymbols.GetValueOrDefault(info, "\u25CF");
                        inlines.Add(new Run(symbol) { Foreground = ImageTagColor, FontWeight = FontWeight.Bold });
                    }

                    pos += imgMatch.Length;
                    continue;
                }

                // Try [dpN] — bold damage param
                var dpMatch = DpRegex().Match(text, pos);
                if (dpMatch.Success && dpMatch.Index == pos)
                {
                    var run = new Run($"[{dpMatch.Groups[1].Value}]")
                    {
                        FontWeight = FontWeight.Bold,
                        Foreground = DamageParamColor
                    };
                    inlines.Add(run);
                    pos += dpMatch.Length;
                    continue;
                }

                // Try [pN] — param placeholder
                var pMatch = PRegex().Match(text, pos);
                if (pMatch.Success && pMatch.Index == pos)
                {
                    inlines.Add(new Run($"[{pMatch.Groups[1].Value}]") { Foreground = DamageParamColor });
                    pos += pMatch.Length;
                    continue;
                }
            }

            // Regular text — collect until next [ or end
            int nextBracket = text.IndexOf('[', pos + 1);
            if (nextBracket < 0) nextBracket = text.Length;
            var segment = text[pos..nextBracket];
            if (segment.Length > 0)
                inlines.Add(new Run(segment) { Foreground = DefaultText });
            pos = nextBracket;
        }
    }

    private static void AddColoredRun(InlineCollection inlines, string text, IBrush color, bool underline)
    {
        var run = new Run(text) { Foreground = color };
        if (underline)
            run.TextDecorations = TextDecorations.Underline;
        inlines.Add(run);
    }

    [GeneratedRegex(@"\[b\](.*?)\[/b\]", RegexOptions.Singleline)]
    private static partial Regex BoldRegex();

    [GeneratedRegex(@"\[i\](.*?)\[/i\]", RegexOptions.Singleline)]
    private static partial Regex ItalicRegex();

    [GeneratedRegex(@"\[tip=([^\]]+)\](.*?)\[/tip\]", RegexOptions.Singleline)]
    private static partial Regex TipRegex();

    [GeneratedRegex(@"\[status=([^\]]+)\](.*?)\[/status\]", RegexOptions.Singleline)]
    private static partial Regex StatusRegex();

    [GeneratedRegex(@"\[spell=([^\]]+)\](.*?)\[/spell\]", RegexOptions.Singleline)]
    private static partial Regex SpellRegex();

    [GeneratedRegex(@"\[passive=([^\]]+)\](.*?)\[/passive\]", RegexOptions.Singleline)]
    private static partial Regex PassiveRegex();

    [GeneratedRegex(@"\[resource=([^\]]+)\](.*?)\[/resource\]", RegexOptions.Singleline)]
    private static partial Regex ResourceRegex();

    [GeneratedRegex(@"\[img=([^\]]*)\]")]
    private static partial Regex ImgRegex();

    [GeneratedRegex(@"\[dp(\d+)\]")]
    private static partial Regex DpRegex();

    [GeneratedRegex(@"\[p(\d+)\]")]
    private static partial Regex PRegex();
}
