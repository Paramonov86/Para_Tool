using ParaTool.Core.Artifacts;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

/// <summary>
/// Spell cards: a copy compiles under a per-artifact name that `using`s the original, an edited
/// original compiles as a self-`using` override, and neither changes a field it didn't touch.
/// </summary>
public class SpellCardCompileTests
{
    private readonly ITestOutputHelper _output;
    public SpellCardCompileTests(ITestOutputHelper output) => _output = output;

    private static readonly Lazy<StatsResolver> Vanilla = new(() =>
    {
        var db = new VanillaDatabase();
        db.Load();
        return db.Resolver;
    });

    private static ArtifactDefinition NewRing() => new()
    {
        StatId = "TEST_Ring",
        StatType = "Armor",
        UsingBase = "ARM_Ring_A",
        LootPool = "Rings",
    };

    private static StatsEntry EntryOf(string statsText, string name) =>
        StatsParser.Parse(statsText).Last(e => e.Name == name);

    [Fact]
    public void Copy_IsRenamed_UsesOriginal_AndItemUnlocksTheCopy()
    {
        var art = NewRing();
        var spell = SpellCloner.CloneFrom("Target_VampiricTouch", Vanilla.Value);
        spell.Cooldown = "OncePerShortRest";
        art.Spells.Add(spell);
        art.SpellsOnEquip = "Target_VampiricTouch;Shout_Bless";

        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        var copy = EntryOf(text, "TEST_Ring_Spell_1");
        Assert.Equal("Target_VampiricTouch", copy.Using);
        Assert.Equal("OncePerShortRest", copy.Data["Cooldown"]);
        Assert.Contains("UnlockSpell(TEST_Ring_Spell_1);UnlockSpell(Shout_Bless)", text);
        Assert.DoesNotContain("new entry \"Target_VampiricTouch\"", text);
        Assert.DoesNotContain("UnlockSpell(Target_VampiricTouch)", text);
    }

    [Fact]
    public void Copy_NeverWritesItsTextUnderTheOriginalHandle()
    {
        var art = NewRing();
        var spell = SpellCloner.CloneFrom("Target_VampiricTouch", Vanilla.Value);
        spell.DisplayNameHandle = spell.SourceDisplayNameHandle!;
        spell.DisplayName["en"] = "Leech";
        art.Spells.Add(spell);

        var result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);

        Assert.DoesNotContain(result.LocalizationEntries["en"], e => e.handle == spell.SourceDisplayNameHandle);
        Assert.Contains(result.LocalizationEntries["en"], e => e.xmlText == "Leech");
    }

    [Fact]
    public void EditOriginal_IsSelfUsingOverride_KeepingTheOriginalText()
    {
        var art = NewRing();
        var spell = SpellCloner.CloneFrom("Target_VampiricTouch", Vanilla.Value);
        spell.EditOriginal = true;
        spell.DisplayName["en"] = "copied text";
        spell.SpellSuccess = "DealDamage(4d6,Necrotic,Magical)";
        art.Spells.Add(spell);
        art.SpellsOnEquip = "Target_VampiricTouch";

        var result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);
        var entry = EntryOf(result.StatsText, "Target_VampiricTouch");

        Assert.Equal("Target_VampiricTouch", entry.Using);
        Assert.Equal("DealDamage(4d6,Necrotic,Magical)", entry.Data["SpellSuccess"]);
        Assert.Contains(spell.SourceDisplayNameHandle!, entry.Data["DisplayName"]);
        Assert.DoesNotContain(result.LocalizationEntries["en"], e => e.xmlText == "copied text");
        Assert.Contains("UnlockSpell(Target_VampiricTouch)", result.StatsText);
    }

    [Fact]
    public void EditOriginal_WithEditedName_WritesNewHandle()
    {
        var art = NewRing();
        var spell = SpellCloner.CloneFrom("Target_VampiricTouch", Vanilla.Value);
        spell.EditOriginal = true;
        spell.DisplayName["en"] = "Leech";
        spell.DisplayNameEdited = true;
        art.Spells.Add(spell);

        var result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);
        var entry = EntryOf(result.StatsText, "Target_VampiricTouch");

        Assert.DoesNotContain(spell.SourceDisplayNameHandle!, entry.Data["DisplayName"]);
        Assert.Contains(result.LocalizationEntries["en"], e => e.xmlText == "Leech");
    }

    [Fact]
    public void EditOriginal_AfterBeingRenamed_GetsItsNameBack()
    {
        var art = NewRing();
        art.Spells.Add(SpellCloner.CloneFrom("Target_VampiricTouch", Vanilla.Value));
        art.SpellsOnEquip = "Target_VampiricTouch";
        ArtifactCompiler.Compile(art, resolver: Vanilla.Value);
        Assert.Equal("TEST_Ring_Spell_1", art.Spells[0].Name);

        art.Spells[0].EditOriginal = true;
        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        Assert.Equal("Target_VampiricTouch", EntryOf(text, "Target_VampiricTouch").Using);
        Assert.Contains("UnlockSpell(Target_VampiricTouch)", text);
        Assert.DoesNotContain("TEST_Ring_Spell_1", text);
    }

    [Fact]
    public void EditOriginal_SuppressesRenameOverrideOfTheSameSpell()
    {
        var art = NewRing();
        var spell = SpellCloner.CloneFrom("Target_VampiricTouch", Vanilla.Value);
        spell.EditOriginal = true;
        art.Spells.Add(spell);
        art.SpellRenames["Target_VampiricTouch"] = new() { ["en"] = "Renamed" };

        var result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);

        Assert.Single(StatsParser.Parse(result.StatsText), e => e.Name == "Target_VampiricTouch");
        Assert.Contains(result.Warnings, w => w.Contains("Target_VampiricTouch"));
    }

    [Fact]
    public void LegacySpell_NullCardFields_AreNotWritten()
    {
        var art = NewRing();
        art.Spells.Add(new SpellDefinition { Name = "TEST_Ring_Custom", UsingBase = "Shout_Bless", SpellType = "Shout" });

        var entry = EntryOf(ArtifactCompiler.Compile(art).StatsText, "TEST_Ring_Custom");

        foreach (var key in new[] { "Icon", "Level", "SpellSchool", "TargetRadius", "AreaRadius", "SpellRoll", "SpellSuccess", "SpellFail" })
            Assert.False(entry.Data.ContainsKey(key), key);
    }

    /// <summary>
    /// Every vanilla spell cloned and compiled untouched must restate its card fields exactly —
    /// the sanitizers the compiler runs over chip fields must not rewrite game syntax.
    /// </summary>
    [Fact]
    public void EveryVanillaSpell_ClonedUntouched_CompilesToSameCardFields()
    {
        var resolver = Vanilla.Value;
        var spells = resolver.AllEntries.Values.Where(e => e.Type == "SpellData").Select(e => e.Name).ToList();
        var mismatches = new List<string>();

        foreach (var name in spells)
        {
            var art = NewRing();
            art.Spells.Add(SpellCloner.CloneFrom(name, resolver));
            var text = ArtifactCompiler.Compile(art, resolver: resolver).StatsText;
            var compiled = StatsParser.Parse(text).LastOrDefault(e => e.Type == "SpellData");
            if (compiled == null) { mismatches.Add($"{name}: not emitted"); continue; }

            var original = resolver.ResolveAll(name);
            foreach (var key in SpellCloner.CardFields)
            {
                var want = original.GetValueOrDefault(key) ?? "";
                var got = compiled.Data.GetValueOrDefault(key) ?? "";
                if (want != got) mismatches.Add($"{name}.{key}: '{want}' -> '{got}'");
            }
        }

        foreach (var m in mismatches.Take(40)) _output.WriteLine(m);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} fields changed across {spells.Count} spells");
    }
}
