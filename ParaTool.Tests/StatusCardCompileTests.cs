using ParaTool.Core.Artifacts;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

/// <summary>
/// Status cards: a copy compiles under a per-artifact name that `using`s the original and the
/// item's own mentions of the original follow it; an edited original is a self-`using` override;
/// a blank card stands alone; nothing the card didn't touch changes.
/// </summary>
public class StatusCardCompileTests
{
    private readonly ITestOutputHelper _output;
    public StatusCardCompileTests(ITestOutputHelper output) => _output = output;

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
    public void Copy_IsRenamed_AndTheItemsMentionsFollowIt()
    {
        var art = NewRing();
        var status = StatusCloner.CloneFrom("BLESS", Vanilla.Value);
        status.Boosts = "RollBonus(Attack,1d6)";
        art.Statuses.Add(status);
        art.StatusOnEquip = "BLESS;HASTE";
        art.Passives.Add(new PassiveDefinition
        {
            Name = "TEST_Ring_Passive_1",
            StatsFunctors = "ApplyStatus(SELF,BLESS,100,2)",
            Conditions = "not HasStatus('BLESS')",
        });
        var spell = SpellCloner.CloneFrom("Target_Bless", Vanilla.Value);
        spell.SpellProperties = "ApplyStatus(BLESS,100,10)";
        art.Spells.Add(spell);

        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        var copy = EntryOf(text, "TEST_Ring_Status_1");
        Assert.Equal("BLESS", copy.Using);
        Assert.Equal("RollBonus(Attack,1d6)", copy.Data["Boosts"]);
        Assert.DoesNotContain("new entry \"BLESS\"", text);
        Assert.Equal("TEST_Ring_Status_1;HASTE", EntryOf(text, "TEST_Ring").Data["StatusOnEquip"]);
        var passive = EntryOf(text, "TEST_Ring_Passive_1");
        Assert.Equal("ApplyStatus(SELF,TEST_Ring_Status_1,100,2)", passive.Data["StatsFunctors"]);
        Assert.Equal("not HasStatus('TEST_Ring_Status_1')", passive.Data["Conditions"]);
        Assert.Equal("ApplyStatus(TEST_Ring_Status_1,100,10)", EntryOf(text, "TEST_Ring_Spell_1").Data["SpellProperties"]);
    }

    [Fact]
    public void Copy_KeepsTheOriginalText_UntilItIsEdited()
    {
        var art = NewRing();
        var status = StatusCloner.CloneFrom("BLESS", Vanilla.Value);
        status.DisplayName["en"] = "Bless";
        art.Statuses.Add(status);

        var result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);
        var copy = EntryOf(result.StatsText, "TEST_Ring_Status_1");
        Assert.Contains(status.SourceDisplayNameHandle!, copy.Data["DisplayName"]);
        Assert.Empty(result.LocalizationEntries["en"].Where(e => e.xmlText == "Bless"));

        status.DisplayNameEdited = true;
        status.DisplayName["en"] = "Greater Bless";
        result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);
        copy = EntryOf(result.StatsText, "TEST_Ring_Status_1");
        Assert.DoesNotContain(status.SourceDisplayNameHandle!, copy.Data["DisplayName"]);
        Assert.Contains(result.LocalizationEntries["en"], e => e.xmlText == "Greater Bless" && e.handle != status.SourceDisplayNameHandle);
    }

    [Fact]
    public void EditOriginal_IsSelfUsingOverride()
    {
        var art = NewRing();
        var status = StatusCloner.CloneFrom("BLESS", Vanilla.Value);
        status.EditOriginal = true;
        status.TickType = "StartTurn";
        art.Statuses.Add(status);
        art.StatusOnEquip = "BLESS";

        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        var entry = EntryOf(text, "BLESS");
        Assert.Equal("BLESS", entry.Using);
        Assert.Equal("StartTurn", entry.Data["TickType"]);
        Assert.Equal("BLESS", EntryOf(text, "TEST_Ring").Data["StatusOnEquip"]);
        Assert.DoesNotContain("TEST_Ring_Status_1", text);
    }

    [Fact]
    public void EditOriginal_SwitchedBackAfterACompile_GetsTheOriginalName()
    {
        var art = NewRing();
        var status = StatusCloner.CloneFrom("BLESS", Vanilla.Value);
        art.Statuses.Add(status);
        art.StatusOnEquip = "BLESS";
        ArtifactCompiler.Compile(art, resolver: Vanilla.Value);
        Assert.Equal("TEST_Ring_Status_1", status.Name);

        status.EditOriginal = true;
        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;
        Assert.Equal("BLESS", status.Name);
        Assert.Equal("BLESS", EntryOf(text, "TEST_Ring").Data["StatusOnEquip"]);
    }

    [Fact]
    public void Blank_StandsAlone_WithItsOwnStack()
    {
        var art = NewRing();
        var status = StatusCloner.CreateBlank(art);
        status.DisplayName["en"] = "Warded";
        status.Boosts = "AC(2)";
        art.Statuses.Add(status);
        art.StatusOnEquip = status.Name;

        var result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);

        var entry = EntryOf(result.StatsText, "TEST_Ring_Status_1");
        Assert.Null(entry.Using);
        Assert.Equal("BOOST", entry.Data["StatusType"]);
        Assert.Equal("TEST_Ring_Status_1", entry.Data["StackId"]);
        Assert.Equal("AC(2)", entry.Data["Boosts"]);
        Assert.Contains(result.LocalizationEntries["en"], e => e.xmlText == "Warded");
        Assert.DoesNotContain(result.Warnings, w => w.Contains("TEST_Ring_Status_1"));
    }

    [Fact]
    public void DuplicatedArtifact_RenamesTheCopyAgain()
    {
        var art = NewRing();
        art.Statuses.Add(new StatusDefinition { Name = "OTHER_Ring_Status_1", UsingBase = "BLESS" });
        art.StatusOnEquip = "OTHER_Ring_Status_1";

        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        Assert.Equal("BLESS", EntryOf(text, "TEST_Ring_Status_1").Using);
        Assert.Equal("TEST_Ring_Status_1", EntryOf(text, "TEST_Ring").Data["StatusOnEquip"]);
        Assert.DoesNotContain("OTHER_Ring_Status_1", text);
    }

    [Fact]
    public void TextWrittenOnlyInRussian_AlsoGoesOutAsEnglish()
    {
        // The game shows English for a language a handle has no text in; with no English either,
        // German, Polish, Chinese… players would see nothing.
        var art = NewRing();
        var blank = StatusCloner.CreateBlank(art);
        blank.DisplayName = new() { ["en"] = "", ["ru"] = "Оберег" };
        blank.Description = new() { ["en"] = "", ["ru"] = "", ["de"] = "Schützt." };
        art.Statuses.Add(blank);
        art.DisplayName = new() { ["en"] = "", ["ru"] = "Кольцо" };
        art.DisplayNameHandle = ParaTool.Core.Localization.HandleGenerator.New();

        var loca = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).LocalizationEntries;

        Assert.Contains(loca["en"], e => e.handle == blank.DisplayNameHandle && e.xmlText == "Оберег");
        Assert.Contains(loca["ru"], e => e.handle == blank.DisplayNameHandle && e.xmlText == "Оберег");
        Assert.Contains(loca["en"], e => e.handle == blank.DescriptionHandle && e.xmlText == "Schützt.");
        // The item's own name may be under the base item's handle, whose English the game has.
        Assert.Contains(loca["ru"], e => e.handle == art.DisplayNameHandle);
        Assert.DoesNotContain(loca["en"], e => e.handle == art.DisplayNameHandle);
    }

    [Fact]
    public void CompilingTwice_ChangesNothing()
    {
        var art = NewRing();
        art.Statuses.Add(StatusCloner.CloneFrom("BLESS", Vanilla.Value));
        art.StatusOnEquip = "BLESS";
        var first = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;
        var second = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;
        Assert.Equal(first, second);
    }

    [Fact]
    public void EveryVanillaStatus_ClonedUntouched_CompilesToSameCardFields()
    {
        var resolver = Vanilla.Value;
        var statuses = resolver.AllEntries.Values.Where(e => e.Type == "StatusData").Select(e => e.Name).ToList();
        var mismatches = new List<string>();

        foreach (var name in statuses)
        {
            var art = NewRing();
            art.Statuses.Add(StatusCloner.CloneFrom(name, resolver));
            var text = ArtifactCompiler.Compile(art, resolver: resolver).StatsText;
            var compiled = StatsParser.Parse(text).LastOrDefault(e => e.Name == art.Statuses[0].Name);
            if (compiled == null) { mismatches.Add($"{name}: not emitted"); continue; }

            // A copy's mentions of itself follow it (a status that removes itself removes the copy).
            var original = resolver.ResolveAll(name);
            var expected = new StatusDefinition { UsingBase = name };
            var probe = new ArtifactDefinition { StatId = "TEST_Ring", Statuses = [expected] };
            foreach (var key in StatusCloner.CardFields.Append("DisplayName").Append("Description"))
            {
                var want = original.GetValueOrDefault(key) ?? "";
                if (key is "Boosts" or "TickFunctors" or "OnApplyFunctors" or "OnRemoveFunctors" or "RemoveConditions" or "AuraStatuses")
                {
                    expected.TickFunctors = want;
                    ArtifactCompiler.RewriteStatusReferences(probe, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [name] = "TEST_Ring_Status_1" });
                    want = expected.TickFunctors ?? "";
                }
                var got = compiled.Data.GetValueOrDefault(key) ?? "";
                if (key is "DisplayName" or "Description")
                {
                    want = want.Split(';')[0];
                    got = got.Split(';')[0];
                }
                if (want != got) mismatches.Add($"{name}.{key}: '{want}' -> '{got}'");
            }
        }

        foreach (var m in mismatches.Take(40)) _output.WriteLine(m);
        Assert.True(mismatches.Count == 0, $"{mismatches.Count} fields changed across {statuses.Count} statuses");
    }
}
