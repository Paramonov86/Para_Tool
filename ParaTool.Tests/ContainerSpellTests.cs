using ParaTool.Core.Artifacts;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

/// <summary>
/// Container spells: the effects live on the variants a container lists. A copied container must
/// list copied variants that point back at it; an edited original overrides the family in place.
/// </summary>
public class ContainerSpellTests
{
    private readonly ITestOutputHelper _output;
    public ContainerSpellTests(ITestOutputHelper output) => _output = output;

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

    private static string CompileWave(ArtifactDefinition art, bool editOriginal = false)
    {
        var wave = SpellCloner.CloneWithVariants("Shout_DestructiveWave", Vanilla.Value);
        wave.EditOriginal = editOriginal;
        art.Spells.Add(wave);
        return ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;
    }

    [Fact]
    public void ListedVariants_FindsContainersOnly()
    {
        var r = Vanilla.Value;
        Assert.Equal(["Shout_DestructiveWave_Necrotic", "Shout_DestructiveWave_Radiant"],
            SpellCloner.ListedVariants("Shout_DestructiveWave", r));
        // A variant `using` its container inherits the list but is not a container.
        Assert.Empty(SpellCloner.ListedVariants("Shout_DestructiveWave_Necrotic", r));
        // An upcast container lists its own upcast variants.
        var hex2 = SpellCloner.ListedVariants("Target_Hex_2", r);
        Assert.Equal(6, hex2.Count);
        Assert.All(hex2, v => Assert.EndsWith("_2", v));
        Assert.Empty(SpellCloner.ListedVariants("Target_VampiricTouch", r));
    }

    [Fact]
    public void CloneWithVariants_CopiesEachVariantsEffects()
    {
        var wave = SpellCloner.CloneWithVariants("Shout_DestructiveWave", Vanilla.Value);

        Assert.Equal(2, wave.Variants.Count);
        Assert.Contains("Necrotic", wave.Variants[0].SpellSuccess);
        Assert.Equal("Shout_DestructiveWave_Radiant", wave.Variants[1].UsingBase);
    }

    [Fact]
    public void CopiedContainer_ListsCopiedVariants_ThatPointBackAtIt()
    {
        var text = CompileWave(NewRing());

        var container = EntryOf(text, "TEST_Ring_Spell_1");
        Assert.Equal("Shout_DestructiveWave", container.Using);
        Assert.Equal("TEST_Ring_Spell_1_1;TEST_Ring_Spell_1_2", container.Data["ContainerSpells"]);

        var necrotic = EntryOf(text, "TEST_Ring_Spell_1_1");
        Assert.Equal("Shout_DestructiveWave_Necrotic", necrotic.Using);
        Assert.Equal("TEST_Ring_Spell_1", necrotic.Data["SpellContainerID"]);
        Assert.Equal("", necrotic.Data["ContainerSpells"]); // it inherits the container's list
        Assert.Contains("Necrotic", necrotic.Data["SpellSuccess"]);
        Assert.Equal("TEST_Ring_Spell_1", EntryOf(text, "TEST_Ring_Spell_1_2").Data["SpellContainerID"]);
        Assert.DoesNotContain("new entry \"Shout_DestructiveWave", text);
    }

    [Fact]
    public void CompilingTwice_WritesTheSameLinks()
    {
        var art = NewRing();
        var first = CompileWave(art);
        var second = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        Assert.Equal(first, second);
    }

    [Fact]
    public void GrantedContainer_UnlocksTheCopiedContainer()
    {
        var art = NewRing();
        art.SpellsOnEquip = "Shout_DestructiveWave";
        var text = CompileWave(art);

        var boosts = EntryOf(text, "TEST_Ring").Data["Boosts"];
        Assert.Contains("UnlockSpell(TEST_Ring_Spell_1)", boosts);
        Assert.DoesNotContain("TEST_Ring_Spell_1_", boosts);
    }

    [Fact]
    public void EditedOriginalContainer_OverridesTheFamilyInPlace_WithoutTouchingLinks()
    {
        var text = CompileWave(NewRing(), editOriginal: true);

        foreach (var name in new[] { "Shout_DestructiveWave", "Shout_DestructiveWave_Necrotic", "Shout_DestructiveWave_Radiant" })
        {
            var entry = EntryOf(text, name);
            Assert.Equal(name, entry.Using);
            Assert.False(entry.Data.ContainsKey("ContainerSpells"), name);
            Assert.False(entry.Data.ContainsKey("SpellContainerID"), name);
        }
        Assert.DoesNotContain("TEST_Ring_Spell_", text);
    }

    [Fact]
    public void SwitchingToEditOriginal_AfterACopyCompile_RestoresOriginalNames()
    {
        var art = NewRing();
        CompileWave(art);
        art.Spells[0].EditOriginal = true;
        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        Assert.Equal("Shout_DestructiveWave_Radiant", art.Spells[0].Variants[1].Name);
        Assert.Equal("Shout_DestructiveWave_Radiant", EntryOf(text, "Shout_DestructiveWave_Radiant").Using);
        Assert.DoesNotContain("TEST_Ring_Spell_", text);
    }

    [Fact]
    public void VariantCopiedOnItsOwn_LeavesTheOriginalContainer()
    {
        var art = NewRing();
        art.Spells.Add(SpellCloner.CloneWithVariants("Shout_DestructiveWave_Necrotic", Vanilla.Value));
        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        var copy = EntryOf(text, "TEST_Ring_Spell_1");
        Assert.Equal("", copy.Data["SpellContainerID"]);
        Assert.Equal("", copy.Data["ContainerSpells"]);
    }

    [Fact]
    public void CopiedUpcastContainer_CutsItsUpcastLinks()
    {
        var art = NewRing();
        art.Spells.Add(SpellCloner.CloneWithVariants("Target_Hex_2", Vanilla.Value));
        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        Assert.Equal("", EntryOf(text, "TEST_Ring_Spell_1").Data["RootSpellID"]);
        var variant = EntryOf(text, "TEST_Ring_Spell_1_1");
        Assert.Equal("TEST_Ring_Spell_1", variant.Data["SpellContainerID"]);
        Assert.Equal("", variant.Data["RootSpellID"]);
    }

    /// <summary>
    /// Every vanilla container copied untouched: each variant restates its card fields exactly and
    /// the family links to itself.
    /// </summary>
    [Fact]
    public void EveryVanillaContainer_CopiedUntouched_KeepsVariantFields_AndLinksToItself()
    {
        var resolver = Vanilla.Value;
        var containers = resolver.AllEntries.Values
            .Where(e => e.Type == "SpellData" && SpellCloner.ListedVariants(e.Name, resolver).Count > 0)
            .Select(e => e.Name).ToList();
        var problems = new List<string>();

        foreach (var name in containers)
        {
            var art = NewRing();
            var card = SpellCloner.CloneWithVariants(name, resolver);
            art.Spells.Add(card);
            var originals = card.Variants.Select(v => v.UsingBase!).ToList();
            var entries = StatsParser.Parse(ArtifactCompiler.Compile(art, resolver: resolver).StatsText)
                .GroupBy(e => e.Name).ToDictionary(g => g.Key, g => g.Last());

            var container = entries[card.Name];
            var listed = card.Variants.Select(v => v.Name).ToList();
            if (container.Data.GetValueOrDefault("ContainerSpells") != string.Join(";", listed))
                problems.Add($"{name}: ContainerSpells '{container.Data.GetValueOrDefault("ContainerSpells")}'");

            for (int i = 0; i < listed.Count; i++)
            {
                if (!entries.TryGetValue(listed[i], out var variant)) { problems.Add($"{name}: {listed[i]} not emitted"); continue; }
                if (variant.Using != originals[i]) problems.Add($"{name}: {listed[i]} using '{variant.Using}'");
                if (variant.Data.GetValueOrDefault("SpellContainerID") != card.Name)
                    problems.Add($"{name}: {listed[i]} SpellContainerID '{variant.Data.GetValueOrDefault("SpellContainerID")}'");
                var want = resolver.ResolveAll(originals[i]);
                foreach (var key in SpellCloner.CardFields)
                    if ((want.GetValueOrDefault(key) ?? "") != (variant.Data.GetValueOrDefault(key) ?? ""))
                        problems.Add($"{name}: {originals[i]}.{key} changed");
            }
        }

        foreach (var p in problems.Take(40)) _output.WriteLine(p);
        Assert.True(containers.Count > 100, $"only {containers.Count} containers found");
        Assert.True(problems.Count == 0, $"{problems.Count} problems across {containers.Count} containers");
    }
}
