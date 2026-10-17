using System.Text.RegularExpressions;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Localization;
using ParaTool.Core.Parsing;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

/// <summary>
/// Tester report (0.7.11): changing anything in a passive of an AMP item, or adding a passive to it,
/// made the item's passive descriptions show raw markup in game. Two AMP items from the report:
/// Vanelona's rapier (AMP_Vanelona_Rapier_3, its "Life Drain" passive's target switched SWAP → SELF)
/// and the Robe of the Aquamancer (AMP_FrostMage_Robe, a passive added — Flood Caller's link to
/// the Aquamancer Powers broke). Both texts nest markup: &lt;i&gt;(… &lt;b&gt;undead&lt;/b&gt;)&lt;/i&gt; and
/// &lt;b&gt;&lt;LSTag …&gt;…&lt;/LSTag&gt;&lt;/b&gt;. The strings below are AMP's own, verbatim.
/// </summary>
public class PassiveLocaTests
{
    private readonly ITestOutputHelper _output;
    public PassiveLocaTests(ITestOutputHelper output) => _output = output;

    private const string LifeDrainName = "h9258a983g98a0g4f4bg8e46g9a2a54def355";
    private const string LifeDrainDesc = "hdc2634fdgd4e4g4870g940fgb19ddb8ab51b";
    private const string FloodCallerName = "h9f18c6a5g0f6eg4158gb18ag9530ec52527d";
    private const string FloodCallerDesc = "h09ce5f83gf6c2g4eebgaf61gc20992ea01df";

    /// <summary>AMP's loca for the two passives: handle → language → the text as the .loca.xml has it.</summary>
    private static readonly Dictionary<string, Dictionary<string, string>> AmpLoca = new()
    {
        [LifeDrainName] = new() { ["en"] = "Life Drain", ["ru"] = "Похищение жизни", ["de"] = "Lebensentzug" },
        [LifeDrainDesc] = new()
        {
            ["en"] = "Hitting a foe with this rapier, regain &lt;b&gt;[1]&lt;/b&gt; &lt;i&gt;(even if you are &lt;b&gt;undead&lt;/b&gt;)&lt;/i&gt;.",
            ["ru"] = "Попадая этой рапирой по врагу, восстановите &lt;b&gt;[1]&lt;/b&gt; &lt;i&gt;(даже если вы - &lt;b&gt;нежить&lt;/b&gt;)&lt;/i&gt;.",
            ["de"] = "Wenn du einen Gegner mit diesem Rapier triffst, erhältst du &lt;b&gt;[1]&lt;/b&gt; &lt;i&gt;(auch wenn du &lt;b&gt;untot&lt;/b&gt; bist)&lt;/i&gt; zurück.",
        },
        [FloodCallerName] = new() { ["en"] = "Flood Caller", ["ru"] = "Зов потопа", ["de"] = "Überschwemmungsmelder" },
        [FloodCallerDesc] = new()
        {
            ["en"] = "When you deal damage with &lt;b&gt;&lt;LSTag Type=\"Status\" Tooltip=\"AMP_AQUAMANCER_POWER_INFO\"&gt;Aquamancer Powers&lt;/LSTag&gt;&lt;/b&gt;, you create a &lt;b&gt;water&lt;/b&gt; surface beneath the target in a &lt;b&gt;[1]&lt;/b&gt; radius.",
            ["ru"] = "Нанося урон &lt;b&gt;&lt;LSTag Type=\"Status\" Tooltip=\"AMP_AQUAMANCER_POWER_INFO\"&gt;силами водоманта&lt;/LSTag&gt;&lt;/b&gt;, вы создаете под целью &lt;b&gt;водную&lt;/b&gt; поверхность радиусом &lt;b&gt;[1]&lt;/b&gt;.",
            ["de"] = "Wenn du mit &lt;b&gt;&lt;LSTag Type=\"Status\" Tooltip=\"AMP_AQUAMANCER_POWER_INFO\"&gt;Wassermagier-Kräften&lt;/LSTag&gt;&lt;/b&gt; Schaden verursachst, erzeugst du eine &lt;b&gt;Wasserfläche&lt;/b&gt; unter dem Ziel in einem Radius von &lt;b&gt;[1]&lt;/b&gt;.",
        },
    };

    /// <summary>The entries from AMP's Extra_Items.txt the two items need.</summary>
    private const string AmpStats = """
        new entry "AMP_Vanelona_LifeDrain_3"
        type "PassiveData"
        data "DisplayName" "h9258a983g98a0g4f4bg8e46g9a2a54def355;1"
        data "Description" "hdc2634fdgd4e4g4870g940fgb19ddb8ab51b;1"
        data "DescriptionParams" "RegainHitPoints(1d6)"
        data "Properties" "Highlighted"
        data "StatsFunctorContext" "OnAttack"
        data "Conditions" "AttackedWithPassiveSourceWeapon() and HasDamageEffectFlag(DamageFlags.Hit) and Enemy()"
        data "StatsFunctors" "ApplyStatus(SWAP,AMP_VANELONA_HEAL,100,0)"

        new entry "AMP_VANELONA_HEAL"
        type "StatusData"
        data "StatusType" "BOOST"
        data "StackId" "MAG_REGAIN_HP"

        new entry "AMP_FrostMage_FloodCaller_Passive"
        type "PassiveData"
        data "DisplayName" "h9f18c6a5g0f6eg4158gb18ag9530ec52527d;1"
        data "Description" "h09ce5f83gf6c2g4eebgaf61gc20992ea01df;1"
        data "DescriptionParams" "Distance(2)"
        data "Properties" "Highlighted"
        data "StatsFunctorContext" "OnDamage"
        data "Conditions" "IsAquamancerPower() and not HasDamageDoneForType(DamageType.Fire) and not Item()"
        data "StatsFunctors" "GROUND:CreateSurface(2,-1,Water)"
        """;

    private static StatsResolver Resolver()
    {
        var r = new StatsResolver();
        r.AddEntries(StatsParser.Parse(AmpStats));
        return r;
    }

    /// <summary>
    /// A passive card the way the constructor makes one from an item's PassivesOnEquip: fields from
    /// the stats, text from the loca as BB-code, the source's handles remembered, and the fresh
    /// handles a save gives every card.
    /// </summary>
    private static PassiveDefinition LoadCard(string name, StatsResolver resolver, params string[] langs)
    {
        var f = resolver.ResolveAll(name);
        var card = new PassiveDefinition
        {
            Name = name,
            DescriptionParams = f.GetValueOrDefault("DescriptionParams", ""),
            Properties = f.GetValueOrDefault("Properties", ""),
            StatsFunctorContext = f.GetValueOrDefault("StatsFunctorContext", ""),
            Conditions = f.GetValueOrDefault("Conditions", ""),
            StatsFunctors = f.GetValueOrDefault("StatsFunctors", ""),
            SourceDisplayNameHandle = HandleGenerator.Parse(f["DisplayName"]).handle,
            SourceDescriptionHandle = HandleGenerator.Parse(f["Description"]).handle,
            DisplayNameHandle = HandleGenerator.New(),
            DescriptionHandle = HandleGenerator.New(),
        };
        foreach (var lang in langs)
        {
            card.DisplayName[lang] = BbCode.FromBg3Xml(AmpLoca[card.SourceDisplayNameHandle][lang]);
            card.Description[lang] = BbCode.FromBg3Xml(AmpLoca[card.SourceDescriptionHandle][lang]);
        }
        return card;
    }

    private static ArtifactDefinition Rapier() => new()
    {
        StatId = "AMP_Vanelona_Rapier_3", StatType = "Weapon", LootPool = "Rapiers",
        PassivesOnEquip = "AMP_Vanelona_LifeDrain_3",
    };

    private static ArtifactDefinition Robe() => new()
    {
        StatId = "AMP_FrostMage_Robe", StatType = "Armor", LootPool = "Robes",
        PassivesOnEquip = "AMP_FrostMage_FloodCaller_Passive",
    };

    private static string? LocaText(ArtifactCompiler.CompileResult r, string lang, string handle) =>
        r.LocalizationEntries.GetValueOrDefault(lang)?.FirstOrDefault(e => e.handle == handle).xmlText;

    // ── The converter ─────────────────────────────────────────

    public static IEnumerable<object[]> AmpTexts() =>
        AmpLoca.Values.SelectMany(byLang => byLang.Values).Select(t => new object[] { t });

    [Theory]
    [MemberData(nameof(AmpTexts))]
    public void AmpText_SurvivesTheEditorRoundTrip(string gameXml)
    {
        // Was: <i>(… [b]undead[/b])</i> and <b>[status=AMP_AQUAMANCER_POWER_INFO]…[/status]</b> —
        // the content of a [b]/[i]/LSTag pair was escaped verbatim instead of converted.
        Assert.Equal(gameXml, BbCode.ToBg3Xml(BbCode.FromBg3Xml(gameXml)));
    }

    [Theory]
    [InlineData("[b][status=WET]wet[/status][/b]", "&lt;b&gt;&lt;LSTag Type=\"Status\" Tooltip=\"WET\"&gt;wet&lt;/LSTag&gt;&lt;/b&gt;")]
    [InlineData("[i](even [b]undead[/b])[/i]", "&lt;i&gt;(even &lt;b&gt;undead&lt;/b&gt;)&lt;/i&gt;")]
    [InlineData("[tip=Vulnerable][b]weak[/b][/tip]", "&lt;LSTag Tooltip=\"Vulnerable\"&gt;&lt;b&gt;weak&lt;/b&gt;&lt;/LSTag&gt;")]
    [InlineData("[b]a [b]b[/b] c[/b]", "&lt;b&gt;a &lt;b&gt;b&lt;/b&gt; c&lt;/b&gt;")]
    [InlineData("[img=SoftWarning] One at a time.", "&lt;LSTag Type=\"Image\" Info=\"SoftWarning\"/&gt; One at a time.")]
    [InlineData("[b]unclosed", "[b]unclosed")]
    public void NestedTags_AreConverted(string bb, string xml)
    {
        Assert.Equal(xml, BbCode.ToBg3Xml(bb));
    }

    [Theory]
    // AMP's DifficultyModule: "Windborne&apos;s back" came out as "Windborne&amp;apos;s" — shown as typed.
    [InlineData("the Windborne&apos;s back", "the Windborne's back")]
    [InlineData("fury &#8212; and flame", "fury — and flame")]
    [InlineData("&quot;quoted&quot; &#x2014;", "\"quoted\" —")]
    [InlineData("a literal &amp;lt;b&amp;gt;", "a literal &lt;b&gt;")]
    public void Entities_AreDecodedOnce(string gameXml, string bb)
    {
        Assert.Equal(bb, BbCode.FromBg3Xml(gameXml));
        Assert.DoesNotContain("&amp;apos;", BbCode.ToBg3Xml(BbCode.FromBg3Xml(gameXml)));
    }

    // ── The compile ───────────────────────────────────────────

    [Fact]
    public void FunctorEdit_KeepsTheSourceText_InEveryLanguage()
    {
        // Vanelona's rapier: the target of Life Drain switched SWAP → SELF, the text left alone.
        var resolver = Resolver();
        var art = Rapier();
        var card = LoadCard("AMP_Vanelona_LifeDrain_3", resolver, "en", "ru");
        card.StatsFunctors = "ApplyStatus(SELF,AMP_VANELONA_HEAL,100,0)";
        art.Passives.Add(card);

        foreach (var pass in new[] { 1, 2 }) // the patcher compiles twice on the same object
        {
            var result = ArtifactCompiler.Compile(art, resolver: resolver);
            var entry = StatsParser.Parse(result.StatsText).Single(e => e.Name == "AMP_Vanelona_Rapier_3_Passive_1");
            Assert.Equal("AMP_Vanelona_LifeDrain_3", entry.Using);
            Assert.Equal(LifeDrainName + ";1", entry.Data["DisplayName"]);
            Assert.Equal(LifeDrainDesc + ";1", entry.Data["Description"]);
            Assert.Equal("ApplyStatus(SELF,AMP_VANELONA_HEAL,100,0)", entry.Data["StatsFunctors"]);
            // Nothing re-written: AMP's own strings serve every language, German included.
            Assert.All(result.LocalizationEntries.Values.SelectMany(v => v),
                e => Assert.NotEqual(e.handle, card.DescriptionHandle));
            Assert.All(result.LocalizationEntries.Values.SelectMany(v => v),
                e => Assert.NotEqual(e.handle, card.DisplayNameHandle));
            _output.WriteLine($"pass {pass}: Description={entry.Data["Description"]}");
        }
    }

    [Fact]
    public void EditedText_GoesOutUnderItsOwnHandle_WithTheMarkupIntact()
    {
        var resolver = Resolver();
        var art = Rapier();
        var card = LoadCard("AMP_Vanelona_LifeDrain_3", resolver, "en", "ru");
        card.Description["ru"] += " Срабатывает раз в ход.";
        card.DescriptionEdited = true;
        art.Passives.Add(card);

        var result = ArtifactCompiler.Compile(art, resolver: resolver);
        var entry = StatsParser.Parse(result.StatsText).Single(e => e.Name == "AMP_Vanelona_Rapier_3_Passive_1");

        Assert.Equal(LifeDrainName + ";1", entry.Data["DisplayName"]); // the name was not edited
        var descHandle = HandleGenerator.Parse(entry.Data["Description"]).handle;
        Assert.NotEqual(LifeDrainDesc, descHandle);
        Assert.Equal(AmpLoca[LifeDrainDesc]["ru"] + " Срабатывает раз в ход.", LocaText(result, "ru", descHandle));
        Assert.Equal(AmpLoca[LifeDrainDesc]["en"], LocaText(result, "en", descHandle));
        Assert.DoesNotContain("[b]", LocaText(result, "ru", descHandle));
    }

    [Fact]
    public void EditedText_NeverReusesTheSourceHandle()
    {
        // An older card may carry the source's handle as its own; edited text must not overwrite AMP's.
        var resolver = Resolver();
        var art = Rapier();
        var card = LoadCard("AMP_Vanelona_LifeDrain_3", resolver, "en", "ru");
        card.DescriptionHandle = LifeDrainDesc;
        card.Description["en"] = "Heal on hit.";
        card.DescriptionEdited = true;
        art.Passives.Add(card);

        var result = ArtifactCompiler.Compile(art, resolver: resolver);
        var entry = StatsParser.Parse(result.StatsText).Single(e => e.Name == "AMP_Vanelona_Rapier_3_Passive_1");

        Assert.NotEqual(LifeDrainDesc, HandleGenerator.Parse(entry.Data["Description"]).handle);
        Assert.Null(LocaText(result, "en", LifeDrainDesc));
    }

    [Fact]
    public void AddingAPassive_LeavesTheOtherPassivesText_Alone()
    {
        // Robe of the Aquamancer: a passive added (wet on equip), Flood Caller untouched.
        var resolver = Resolver();
        var art = Robe();
        art.Passives.Add(LoadCard("AMP_FrostMage_FloodCaller_Passive", resolver, "en", "ru"));
        var added = new PassiveDefinition
        {
            Name = "Passive_Wet_On_Equip",
            DisplayNameHandle = HandleGenerator.New(),
            DescriptionHandle = HandleGenerator.New(),
            StatsFunctorContext = "OnEquip",
            StatsFunctors = "ApplyStatus(SELF,WET,100,1)",
        };
        added.DisplayName["ru"] = "Сразу мокрый";
        added.Description["ru"] = "Надевая робу, вы становитесь [status=WET]мокрым[/status].";
        art.Passives.Add(added);

        var result = ArtifactCompiler.Compile(art, resolver: resolver);
        var flood = StatsParser.Parse(result.StatsText).Single(e => e.Name == "AMP_FrostMage_Robe_Passive_1");

        Assert.Equal(FloodCallerDesc + ";1", flood.Data["Description"]);
        var addedDesc = LocaText(result, "ru", added.DescriptionHandle);
        Assert.Equal("Надевая робу, вы становитесь &lt;LSTag Type=\"Status\" Tooltip=\"WET\"&gt;мокрым&lt;/LSTag&gt;.", addedDesc);
    }

    [Fact]
    public void CardFromAnOlderBuild_WritesTheSourceMarkupBack()
    {
        // Saved before cards remembered their source: the text goes out under the card's handles,
        // and must come out as AMP wrote it.
        var resolver = Resolver();
        var art = Robe();
        var card = LoadCard("AMP_FrostMage_FloodCaller_Passive", resolver, "en", "ru");
        card.SourceDisplayNameHandle = null;
        card.SourceDescriptionHandle = null;
        art.Passives.Add(card);

        var result = ArtifactCompiler.Compile(art, resolver: resolver);

        Assert.Equal(AmpLoca[FloodCallerDesc]["ru"], LocaText(result, "ru", card.DescriptionHandle));
        Assert.Equal(AmpLoca[FloodCallerDesc]["en"], LocaText(result, "en", card.DescriptionHandle));
    }

    // ── All of AMP's text, when it is on disk ─────────────────

    private const string AmpLocaDir =
        @"E:\SteamLibrary\steamapps\common\Baldurs Gate 3\Data\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025\Localization";

    [Fact]
    public void EveryAmpString_SurvivesTheEditorRoundTrip()
    {
        if (!Directory.Exists(AmpLocaDir)) { _output.WriteLine("AMP loca not on disk — skipped"); return; }
        var content = new Regex("<content contentuid=\"([^\"]*)\"[^>]*>(.*?)</content>", RegexOptions.Singleline);
        int total = 0;
        var broken = new List<string>();
        foreach (var file in Directory.EnumerateFiles(AmpLocaDir, "*.xml", SearchOption.AllDirectories))
            foreach (Match m in content.Matches(File.ReadAllText(file)))
            {
                var text = m.Groups[2].Value;
                // The editor turns a raw line break into <br> on purpose (the game shows none); a
                // text whose tags overlap (<b><i>…</b></i>) has no nesting to keep; raw "<b>" or a
                // broken entity ("&gt>") is not valid loca text.
                if (text.Contains('\n') || text.Contains('<') || text.Contains('>') || !WellNested(text)) continue;
                total++;
                var back = BbCode.ToBg3Xml(BbCode.FromBg3Xml(text));
                // The same text to the game: entities other than &lt; &gt; &amp; come back as the
                // characters they stand for.
                var expected = Regex.Replace(text.Replace("\" /&gt;", "\"/&gt;").Replace("&apos;", "'").Replace("&quot;", "\""),
                    "&#([0-9]+);", d => char.ConvertFromUtf32(int.Parse(d.Groups[1].Value)));
                if (back != expected)
                    broken.Add($"{Path.GetFileName(file)} {m.Groups[1].Value}: {text}\n  → {back}");
            }
        _output.WriteLine($"{total} strings, {broken.Count} broken");
        foreach (var b in broken.Take(10)) _output.WriteLine(b);
        Assert.True(total > 1000);
        Assert.Empty(broken);
    }

    private static bool WellNested(string xml)
    {
        var stack = new Stack<string>();
        foreach (Match t in Regex.Matches(xml, "&lt;(/?)(b|i)&gt;"))
        {
            if (t.Groups[1].Value == "") stack.Push(t.Groups[2].Value);
            else if (stack.Count == 0 || stack.Pop() != t.Groups[2].Value) return false;
        }
        return stack.Count == 0;
    }
}
