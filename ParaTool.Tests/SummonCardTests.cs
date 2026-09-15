using ParaTool.Core.Artifacts;
using ParaTool.Core.LSLib;
using ParaTool.Core.Parsing;
using ParaTool.Core.Patching;
using ParaTool.Core.Services;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// A creature card compiles to a Character entry using the creature's stats, a character template
/// inheriting the creature, and the item's spell cards spawn that template instead.
/// </summary>
public class SummonCardTests
{
    private const string MudMephit = "02b5e1ea-389d-4008-a247-66538709388b";

    private static readonly Lazy<StatsResolver> Vanilla = new(() =>
    {
        var db = new VanillaDatabase();
        db.Load();
        return db.Resolver;
    });

    private static ArtifactDefinition RingWithSummonSpell(out SpellDefinition spell)
    {
        var art = new ArtifactDefinition { StatId = "TEST_Ring", StatType = "Armor", UsingBase = "ARM_Ring_A", LootPool = "Rings" };
        spell = new SpellDefinition
        {
            Name = "TEST_Ring_MephitCall",
            SpellType = "Target",
            SpellProperties = $"GROUND:Summon({MudMephit}, 10,,,'CombatSummonStack',)",
        };
        art.Spells.Add(spell);
        return art;
    }

    [Fact]
    public void CloneFrom_CopiesCreatureStats()
    {
        var summon = SummonCloner.CloneFrom(MudMephit, Vanilla.Value);

        Assert.NotNull(summon);
        Assert.Equal("Mephit_Mud_Summon", summon.UsingBase);
        Assert.Equal(MudMephit, summon.ParentTemplateUuid);
        Assert.True(summon.Stats.ContainsKey("Vitality"));
        Assert.Equal("Young Mud Mephit", summon.DisplayName["en"]);
        Assert.NotEqual(MudMephit, summon.TemplateUuid);
    }

    [Fact]
    public void Summon_CompilesCharacterEntry_AndSpellSpawnsTheCopy()
    {
        var art = RingWithSummonSpell(out var spell);
        var summon = SummonCloner.CloneFrom(MudMephit, Vanilla.Value)!;
        summon.Stats["Vitality"] = "50";
        art.Summons.Add(summon);

        var result = ArtifactCompiler.Compile(art, resolver: Vanilla.Value);
        var entries = StatsParser.Parse(result.StatsText);

        var creature = entries.Single(e => e.Name == "TEST_Ring_Summon_1");
        Assert.Equal("Character", creature.Type);
        Assert.Equal("Mephit_Mud_Summon", creature.Using);
        Assert.Equal("50", creature.Data["Vitality"]);

        var props = entries.Single(e => e.Name == "TEST_Ring_MephitCall").Data["SpellProperties"];
        Assert.Equal($"GROUND:Summon({summon.TemplateUuid}, 10,,,'CombatSummonStack',)", props);
        Assert.DoesNotContain(result.LocalizationEntries["en"], e => e.xmlText == "Young Mud Mephit");
    }

    [Fact]
    public void SpellWithoutCreatureCard_KeepsOriginalSummon()
    {
        var art = RingWithSummonSpell(out _);

        var text = ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText;

        Assert.Contains($"Summon({MudMephit}, 10,,,'CombatSummonStack',)", text);
        Assert.DoesNotContain("type \"Character\"", text);
    }

    [Fact]
    public void CharacterTemplate_InheritsCreature_WithNewStats()
    {
        var summon = SummonCloner.CloneFrom(MudMephit, Vanilla.Value)!;
        summon.StatsName = "TEST_Ring_Summon_1";
        var path = Path.Combine(Path.GetTempPath(), $"pt_summon_{Guid.NewGuid():N}.lsf");
        try
        {
            AmpPatcher.CreateCharacterTemplateLsf(path, summon);

            using var fs = File.OpenRead(path);
            var node = new LSFReader(fs).Read().Regions["Templates"].Children["GameObjects"].Single();
            Assert.Equal("character", node.Attributes["Type"].Value?.ToString());
            Assert.Equal(summon.TemplateUuid, node.Attributes["MapKey"].Value?.ToString());
            Assert.Equal(MudMephit, node.Attributes["ParentTemplateId"].Value?.ToString());
            Assert.Equal("TEST_Ring_Summon_1", node.Attributes["Stats"].Value?.ToString());
            Assert.False(node.Attributes.ContainsKey("DisplayName"));
        }
        finally { File.Delete(path); }
    }
}
