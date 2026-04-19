using Xunit;
using ParaTool.Core.Localization;

namespace ParaTool.Tests;

/// <summary>
/// Tests for BbCode.ToBg3Xml/FromBg3Xml — the BB-code ↔ BG3 XML converter
/// used for item descriptions. Breakage here causes corrupted loca text.
/// </summary>
public class BbCodeTests
{
    // ── ToBg3Xml: BB-code → XML-escaped ────────────────────

    [Fact]
    public void ToXml_PlainText_Unchanged()
    {
        Assert.Equal("Hello world", BbCode.ToBg3Xml("Hello world"));
    }

    [Fact]
    public void ToXml_Bold()
    {
        Assert.Equal("&lt;b&gt;bold&lt;/b&gt;", BbCode.ToBg3Xml("[b]bold[/b]"));
    }

    [Fact]
    public void ToXml_Italic()
    {
        Assert.Equal("&lt;i&gt;italic&lt;/i&gt;", BbCode.ToBg3Xml("[i]italic[/i]"));
    }

    [Fact]
    public void ToXml_LineBreak()
    {
        Assert.Equal("&lt;br&gt;", BbCode.ToBg3Xml("[br]"));
    }

    [Fact]
    public void ToXml_Status_LsTag()
    {
        var result = BbCode.ToBg3Xml("[status=STUNNED]Stunned[/status]");
        Assert.Contains("LSTag", result);
        Assert.Contains("Type=\"Status\"", result);
        Assert.Contains("Tooltip=\"STUNNED\"", result);
        Assert.Contains("Stunned", result);
    }

    [Fact]
    public void ToXml_Spell_LsTag()
    {
        var result = BbCode.ToBg3Xml("[spell=Shout_Fireball]Fireball[/spell]");
        Assert.Contains("Type=\"Spell\"", result);
        Assert.Contains("Tooltip=\"Shout_Fireball\"", result);
    }

    [Fact]
    public void ToXml_Passive_LsTag()
    {
        var result = BbCode.ToBg3Xml("[passive=MY_PASSIVE]My Passive[/passive]");
        Assert.Contains("Type=\"Passive\"", result);
        Assert.Contains("Tooltip=\"MY_PASSIVE\"", result);
    }

    [Fact]
    public void ToXml_Resource_LsTag()
    {
        var result = BbCode.ToBg3Xml("[resource=KiPoint]Ki Point[/resource]");
        Assert.Contains("Type=\"ActionResource\"", result);
        Assert.Contains("Tooltip=\"KiPoint\"", result);
    }

    [Fact]
    public void ToXml_Tip_NoType()
    {
        var result = BbCode.ToBg3Xml("[tip=ArmourClass]AC[/tip]");
        Assert.Contains("LSTag", result);
        Assert.Contains("Tooltip=\"ArmourClass\"", result);
        Assert.DoesNotContain("Type=", result);
    }

    [Fact]
    public void ToXml_ParamShorthand()
    {
        Assert.Equal("[1]", BbCode.ToBg3Xml("[p1]"));
        Assert.Equal("[3]", BbCode.ToBg3Xml("[p3]"));
    }

    [Fact]
    public void ToXml_BoldParam()
    {
        Assert.Equal("&lt;b&gt;[1]&lt;/b&gt;", BbCode.ToBg3Xml("[dp1]"));
    }

    [Fact]
    public void ToXml_EscapesRawAngleBrackets()
    {
        // Raw < > & must be escaped to avoid breaking XML
        Assert.Equal("a &lt; b", BbCode.ToBg3Xml("a < b"));
        Assert.Equal("a &gt; b", BbCode.ToBg3Xml("a > b"));
        Assert.Equal("x &amp; y", BbCode.ToBg3Xml("x & y"));
    }

    [Fact]
    public void ToXml_UnknownTag_PassesThrough()
    {
        // Unknown [tag] should not be eaten — shows as literal text
        var result = BbCode.ToBg3Xml("[unknown]x[/unknown]");
        Assert.Contains("[unknown]", result);
    }

    [Fact]
    public void ToXml_MixedContent()
    {
        var input = "Deals [dp1] damage to [status=BURN]burning[/status] targets.";
        var result = BbCode.ToBg3Xml(input);
        Assert.Contains("&lt;b&gt;[1]&lt;/b&gt;", result);
        Assert.Contains("Type=\"Status\"", result);
        Assert.Contains("Tooltip=\"BURN\"", result);
    }

    // ── FromBg3Xml: XML-escaped → BB-code ──────────────────

    [Fact]
    public void FromXml_Bold()
    {
        Assert.Equal("[b]text[/b]", BbCode.FromBg3Xml("&lt;b&gt;text&lt;/b&gt;"));
    }

    [Fact]
    public void FromXml_LineBreak()
    {
        Assert.Equal("[br]", BbCode.FromBg3Xml("&lt;br&gt;"));
    }

    [Fact]
    public void FromXml_Status_ToBbCode()
    {
        var input = "&lt;LSTag Type=\"Status\" Tooltip=\"STUNNED\"&gt;Stunned&lt;/LSTag&gt;";
        Assert.Equal("[status=STUNNED]Stunned[/status]", BbCode.FromBg3Xml(input));
    }

    [Fact]
    public void FromXml_Spell_ToBbCode()
    {
        var input = "&lt;LSTag Type=\"Spell\" Tooltip=\"Shout_Fireball\"&gt;Fireball&lt;/LSTag&gt;";
        Assert.Equal("[spell=Shout_Fireball]Fireball[/spell]", BbCode.FromBg3Xml(input));
    }

    [Fact]
    public void FromXml_BoldParam_ToBbDp()
    {
        // <b>[1]</b> should become [dp1]
        var input = "&lt;b&gt;[1]&lt;/b&gt;";
        Assert.Equal("[dp1]", BbCode.FromBg3Xml(input));
    }

    // ── Roundtrip ───────────────────────────────────────────

    [Fact]
    public void Roundtrip_BoldText()
    {
        var orig = "[b]important[/b]";
        Assert.Equal(orig, BbCode.FromBg3Xml(BbCode.ToBg3Xml(orig)));
    }

    [Fact]
    public void Roundtrip_Status()
    {
        var orig = "[status=STUNNED]Stunned[/status]";
        Assert.Equal(orig, BbCode.FromBg3Xml(BbCode.ToBg3Xml(orig)));
    }

    [Fact]
    public void Roundtrip_Spell()
    {
        var orig = "[spell=Shout_Heal]Heal[/spell]";
        Assert.Equal(orig, BbCode.FromBg3Xml(BbCode.ToBg3Xml(orig)));
    }

    [Fact]
    public void Roundtrip_BoldParam()
    {
        var orig = "[dp1]";
        Assert.Equal(orig, BbCode.FromBg3Xml(BbCode.ToBg3Xml(orig)));
    }

    [Fact]
    public void Roundtrip_PlainText()
    {
        var orig = "Plain description with no tags.";
        Assert.Equal(orig, BbCode.FromBg3Xml(BbCode.ToBg3Xml(orig)));
    }

    // ── StripBbTags ────────────────────────────────────────

    [Fact]
    public void Strip_RemovesBoldTags()
    {
        Assert.Equal("bold", BbCode.StripBbTags("[b]bold[/b]"));
    }

    [Fact]
    public void Strip_RemovesLsTags_KeepsContent()
    {
        Assert.Equal("Fireball", BbCode.StripBbTags("[spell=Shout_Fireball]Fireball[/spell]"));
    }

    [Fact]
    public void Strip_ConvertsParamShorthand()
    {
        // [p1] → [1], [dp1] → [1]
        Assert.Equal("[1]", BbCode.StripBbTags("[p1]"));
        Assert.Equal("[1]", BbCode.StripBbTags("[dp1]"));
    }

    [Fact]
    public void Strip_EmptyOrNull()
    {
        Assert.Equal("", BbCode.StripBbTags(""));
    }
}
