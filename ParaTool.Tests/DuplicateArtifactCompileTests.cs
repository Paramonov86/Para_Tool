using ParaTool.Core.Artifacts;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// A duplicated artifact compiles next to its original: the two never declare the same stats
/// entry, never share a creature template or a loca handle, and each item unlocks, equips and
/// applies its own cards — whether the original was duplicated as saved or after a compile had
/// already named its copies.
/// </summary>
public class DuplicateArtifactCompileTests
{
    private static readonly Lazy<StatsResolver> Vanilla = new(() =>
    {
        var db = new VanillaDatabase();
        db.Load();
        return db.Resolver;
    });

    private const string MudMephit = "02b5e1ea-389d-4008-a247-66538709388b";

    /// <summary>An item with every kind of card, as the Constructor saves it.</summary>
    private static ArtifactDefinition RingWithCards()
    {
        var r = Vanilla.Value;
        var art = new ArtifactDefinition
        {
            StatId = "TEST_Ring",
            StatType = "Armor",
            UsingBase = "ARM_Ring_A",
            LootPool = "Rings",
        };
        art.Passives.Add(new PassiveDefinition { Name = "Alert", UsingBase = "Alert" });
        art.Passives.Add(new PassiveDefinition { Name = "Passive_Own_Idea", Boosts = "AC(1)" });

        var touch = SpellCloner.CloneFrom("Target_VampiricTouch", r);
        touch.Cooldown = "OncePerShortRest";
        art.Spells.Add(touch);
        art.Spells.Add(SpellCloner.CloneWithVariants("Shout_DestructiveWave", r));
        art.SpellsOnEquip = "Target_VampiricTouch;Shout_DestructiveWave";

        var bless = StatusCloner.CloneFrom("BLESS", r);
        bless.Boosts = "RollBonus(Attack,1d6)";
        art.Statuses.Add(bless);
        art.Statuses.Add(StatusCloner.CreateBlank(art));
        art.StatusOnEquip = $"BLESS;{art.Statuses[1].Name}";

        var summon = SummonCloner.CloneFrom(MudMephit, r)!;
        summon.Stats["Vitality"] = "50";
        art.Summons.Add(summon);
        art.Spells.Add(new SpellDefinition
        {
            Name = "TEST_Ring_MephitCall",
            SpellType = "Target",
            SpellProperties = $"GROUND:Summon({MudMephit}, 10,,,'CombatSummonStack',)",
        });
        return art;
    }

    /// <summary>What the patcher does: a fresh object per compile, as loaded from disk.</summary>
    private static ArtifactDefinition Reload(ArtifactDefinition art) =>
        System.Text.Json.JsonSerializer.Deserialize<ArtifactDefinition>(System.Text.Json.JsonSerializer.Serialize(art))!;

    private static List<StatsEntry> Compile(ArtifactDefinition art, StatsResolver? resolver = null) =>
        StatsParser.Parse(ArtifactCompiler.Compile(art, resolver: resolver ?? Vanilla.Value).StatsText);

    private static void AssertIndependent(List<StatsEntry> original, List<StatsEntry> copy, string copyStatId)
    {
        var shared = original.Select(e => e.Name).Intersect(copy.Select(e => e.Name), StringComparer.OrdinalIgnoreCase).ToList();
        Assert.True(shared.Count == 0, "Both artifacts declare: " + string.Join(", ", shared));

        // Every card the copy declares is named after the copy.
        foreach (var e in copy)
            Assert.StartsWith(copyStatId, e.Name, StringComparison.OrdinalIgnoreCase);

        var item = copy.Single(e => e.Name == copyStatId);
        foreach (var passive in item.Data["PassivesOnEquip"].Split(';'))
            Assert.StartsWith(copyStatId + "_", passive);
        foreach (var status in item.Data["StatusOnEquip"].Split(';'))
            Assert.StartsWith(copyStatId + "_", status);
        var unlocked = System.Text.RegularExpressions.Regex.Matches(item.Data["Boosts"], @"UnlockSpell\(([A-Za-z0-9_]+)")
            .Select(m => m.Groups[1].Value).ToList();
        Assert.Equal(2, unlocked.Count);
        Assert.All(unlocked, s => Assert.StartsWith(copyStatId + "_", s));
    }

    private static void AssertCopiesStillUseTheOriginals(List<StatsEntry> copy, string copyStatId)
    {
        Assert.Equal("Alert", copy.Single(e => e.Name == copyStatId + "_Passive_1").Using);
        var touch = copy.Single(e => e.Name == copyStatId + "_Spell_1");
        Assert.Equal("Target_VampiricTouch", touch.Using);
        Assert.Equal("OncePerShortRest", touch.Data["Cooldown"]);
        Assert.Equal("Shout_DestructiveWave", copy.Single(e => e.Name == copyStatId + "_Spell_2").Using);
        Assert.Equal("Shout_DestructiveWave_Necrotic", copy.Single(e => e.Name == copyStatId + "_Spell_2_1").Using);
        Assert.Equal("BLESS", copy.Single(e => e.Name == copyStatId + "_Status_1").Using);
    }

    [Fact]
    public void DuplicateOfTheSavedArtifact_CompilesIndependently()
    {
        var saved = RingWithCards();
        var dup = ArtifactDuplicator.Duplicate(saved, _ => false)!;
        Assert.Equal("TEST_Ring_Copy", dup.StatId);

        var original = Compile(Reload(saved));
        var copy = Compile(Reload(dup));

        AssertIndependent(original, copy, "TEST_Ring_Copy");
        AssertCopiesStillUseTheOriginals(copy, "TEST_Ring_Copy");
    }

    [Fact]
    public void DuplicateOfACompiledArtifact_RenamesTheCopiesAgain()
    {
        // The compiler names copies on the object itself; an artifact duplicated after that
        // carries names like TEST_Ring_Spell_1 that `using` a different entry.
        var art = RingWithCards();
        var original = Compile(art);
        Assert.Contains(art.Spells, s => s.Name == "TEST_Ring_Spell_1" && s.UsingBase == "Target_VampiricTouch");
        Assert.Contains(art.Passives, p => p.Name == "TEST_Ring_Passive_1" && p.UsingBase == "Alert");

        var dup = ArtifactDuplicator.Duplicate(art, _ => false)!;
        var copy = Compile(dup);

        AssertIndependent(original, copy, "TEST_Ring_Copy");
        AssertCopiesStillUseTheOriginals(copy, "TEST_Ring_Copy");

        // The duplicate's summon spell spawns the duplicate's creature, not the original's.
        var call = copy.Single(e => e.Name == "TEST_Ring_Copy_Spell_3").Data["SpellProperties"];
        Assert.Contains(dup.Summons[0].TemplateUuid, call);
        Assert.DoesNotContain(art.Summons[0].TemplateUuid, call);
        Assert.Contains(art.Summons[0].TemplateUuid,
            original.Single(e => e.Name == "TEST_Ring_MephitCall").Data["SpellProperties"]);
    }

    [Fact]
    public void DuplicateOfACompiledArtifact_CompilesTheSameAsADuplicateOfTheSavedOne()
    {
        var saved = RingWithCards();
        var fromSaved = ArtifactDuplicator.Duplicate(Reload(saved), _ => false)!;
        var compiled = Reload(saved);
        Compile(compiled);
        var fromCompiled = ArtifactDuplicator.Duplicate(compiled, _ => false)!;
        // Handles are minted per copy; compare the entries, not the text ids.
        static string Shape(List<StatsEntry> entries) => string.Join("\n", entries.Select(e =>
            $"{e.Name}|{e.Type}|{e.Using}|" + string.Join(",", e.Data
                .Where(d => d.Key is not ("DisplayName" or "Description" or "RootTemplate"))
                .OrderBy(d => d.Key).Select(d => $"{d.Key}={d.Value}"))));

        var a = Compile(fromSaved);
        var b = Compile(fromCompiled);
        // Summon templates are new on every duplicate.
        string Normalize(string s, ArtifactDefinition d) => d.Summons.Aggregate(s, (acc, su) => acc.Replace(su.TemplateUuid, "<summon>"));
        Assert.Equal(Normalize(Shape(a), fromSaved), Normalize(Shape(b), fromCompiled));
    }

    [Fact]
    public void CompilingADuplicateTwice_ChangesNothing()
    {
        var art = RingWithCards();
        Compile(art);
        var dup = ArtifactDuplicator.Duplicate(art, _ => false)!;
        var first = ArtifactCompiler.Compile(dup, resolver: Vanilla.Value).StatsText;
        var second = ArtifactCompiler.Compile(dup, resolver: Vanilla.Value).StatsText;
        Assert.Equal(first, second);
    }

    [Fact]
    public void Clone_SharesNoTemplateAndNoHandle()
    {
        var art = RingWithCards();
        art.DisplayNameHandle = "h_item_name";
        art.DescriptionHandle = "h_item_desc";
        foreach (var p in art.Passives) { p.DisplayNameHandle = "h_p"; p.DescriptionHandle = "h_pd"; }

        var clone = ArtifactDuplicator.Clone(art, "TEST_Other")!;

        Assert.Equal("TEST_Other", clone.StatId);
        Assert.NotEqual(art.ArtifactId, clone.ArtifactId);
        Assert.NotEqual(art.TemplateUuid, clone.TemplateUuid);
        Assert.Equal("", clone.DisplayNameHandle);
        Assert.Equal("", clone.DescriptionHandle);
        Assert.All(clone.Passives, p => Assert.Equal("", p.DisplayNameHandle + p.DescriptionHandle));
        Assert.All(clone.Statuses, s => Assert.Equal("", s.DisplayNameHandle + s.DescriptionHandle));
        Assert.All(clone.Spells.SelectMany(s => s.WithVariants()), s => Assert.Equal("", s.DisplayNameHandle + s.DescriptionHandle));
        Assert.Single(clone.Summons);
        Assert.NotEqual(art.Summons[0].TemplateUuid, clone.Summons[0].TemplateUuid);
        Assert.Equal(art.Summons[0].ParentTemplateUuid, clone.Summons[0].ParentTemplateUuid);
        // The source is untouched.
        Assert.Equal("h_item_name", art.DisplayNameHandle);
        Assert.All(art.Passives, p => Assert.Equal("h_p", p.DisplayNameHandle));
    }

    [Fact]
    public void Duplicate_SkipsTakenStatIds_AndDropsTombstones()
    {
        var art = RingWithCards();
        art.RemovedPassives.Add("SomePassive");
        art.RemovedBoosts.Add("AC(1)");
        var taken = new HashSet<string>(["TEST_Ring_Copy", "TEST_Ring_Copy_2"], StringComparer.OrdinalIgnoreCase);

        var dup = ArtifactDuplicator.Duplicate(art, taken.Contains)!;

        Assert.Equal("TEST_Ring_Copy_3", dup.StatId);
        Assert.Empty(dup.RemovedPassives);
        Assert.Empty(dup.RemovedBoosts);
        Assert.Single(art.RemovedPassives);
    }

    [Fact]
    public void ModPassiveNamedLikeACopy_StillUsesTheModEntry()
    {
        // Seen in players' artifacts: an AMP item's own passive whose name ends in _Passive_3,
        // saved without a UsingBase. It is the mod's entry, not another artifact's copy.
        const string ampPassive = "MAG_Sarevok_OfChaos_Greatsword_Leeching_Passive_3";
        var resolver = new StatsResolver();
        resolver.AddEntries(Vanilla.Value.AllEntries.Values);
        resolver.AddEntries(StatsParser.Parse(
            $"new entry \"{ampPassive}\"\ntype \"PassiveData\"\ndata \"Boosts\" \"AC(1)\"\n"));
        var art = new ArtifactDefinition
        {
            StatId = "MAG_BG_Sarevok_OfChaos_Greatsword_3",
            StatType = "Weapon",
            UsingBase = "MAG_BG_Sarevok_OfChaos_Greatsword_3",
        };
        art.Passives.Add(new PassiveDefinition { Name = ampPassive });

        var entries = Compile(art, resolver);

        var passive = entries.Single(e => e.Name == "MAG_BG_Sarevok_OfChaos_Greatsword_3_Passive_1");
        Assert.Equal(ampPassive, passive.Using);
    }

    [Fact]
    public void OwnCopyNames_AreNotReusedForRenamedCards()
    {
        // A card already named TEST_Ring_Passive_2 keeps it; the copy renamed next to it doesn't take it.
        var art = new ArtifactDefinition { StatId = "TEST_Ring", StatType = "Armor", UsingBase = "ARM_Ring_A" };
        art.Passives.Add(new PassiveDefinition { Name = "TEST_Ring_Passive_2", UsingBase = "Alert" });
        art.Passives.Add(new PassiveDefinition { Name = "OTHER_Ring_Passive_1", UsingBase = "Alert" });

        var entries = Compile(art);

        var passives = entries.Where(e => e.Type == "PassiveData").Select(e => e.Name).ToList();
        Assert.Equal(passives.Count, passives.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Contains("TEST_Ring_Passive_2", passives);
        Assert.DoesNotContain("OTHER_Ring_Passive_1", passives);
    }
}
