using ParaTool.Core.Artifacts;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// The patcher compiles a new artifact twice on the same object and writes the second pass, so
/// the second pass must equal the first — tombstones included, after the first pass renamed cards.
/// </summary>
public class SecondCompilePassTests
{
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

    private static (StatsEntry first, StatsEntry second) ItemTwice(ArtifactDefinition art)
    {
        var first = StatsParser.Parse(ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText).Single(e => e.Name == art.StatId);
        var second = StatsParser.Parse(ArtifactCompiler.Compile(art, resolver: Vanilla.Value).StatsText).Single(e => e.Name == art.StatId);
        return (first, second);
    }

    [Fact]
    public void StaleTombstoneUnderALiveCard_DoesNotHideIt()
    {
        // Seen in a player's artifact: "Passive_Novaya_Passivk" deleted, then made again under the
        // same name — the card is back, the tombstone stayed. The first pass hid it, the second didn't.
        var art = NewRing();
        art.Passives.Add(new PassiveDefinition { Name = "Passive_Novaya_Passivk", Boosts = "AC(1)" });
        art.RemovedPassives.Add("Passive_Novaya_Passivk");

        var (first, second) = ItemTwice(art);

        Assert.Equal("TEST_Ring_Passive_1", first.Data["PassivesOnEquip"]);
        Assert.Equal(first.Data["PassivesOnEquip"], second.Data["PassivesOnEquip"]);
    }

    [Fact]
    public void TombstonedInheritedPassive_StaysRemovedOnBothPasses()
    {
        var art = NewRing();
        art.PassivesOnEquip = "Alert;SomeOther";
        art.Passives.Add(new PassiveDefinition { Name = "Alert", UsingBase = "Alert" });
        art.RemovedPassives.Add("SomeOther");

        var (first, second) = ItemTwice(art);

        Assert.Equal("TEST_Ring_Passive_1", first.Data["PassivesOnEquip"]);
        Assert.Equal(first.Data["PassivesOnEquip"], second.Data["PassivesOnEquip"]);
    }

    [Fact]
    public void TombstonedSpellWithACard_IsNotGrantedOnEitherPass()
    {
        var art = NewRing();
        art.Spells.Add(SpellCloner.CloneFrom("Target_VampiricTouch", Vanilla.Value));
        art.SpellsOnEquip = "Target_VampiricTouch;Shout_Bless";
        art.RemovedSpells.Add("Target_VampiricTouch");

        var (first, second) = ItemTwice(art);

        Assert.Equal("UnlockSpell(Shout_Bless)", first.Data["Boosts"]);
        Assert.Equal(first.Data["Boosts"], second.Data["Boosts"]);
    }

    [Fact]
    public void LegacySpellNamedByTheArtifact_IsNotHiddenByItsBasesTombstone()
    {
        var art = NewRing();
        art.Spells.Add(new SpellDefinition { Name = "TEST_Ring_Custom", UsingBase = "Shout_Bless", SpellType = "Shout" });
        art.SpellsOnEquip = "Shout_Bless;TEST_Ring_Custom";
        art.RemovedSpells.Add("Shout_Bless");

        var (first, second) = ItemTwice(art);

        Assert.Equal("UnlockSpell(TEST_Ring_Custom)", first.Data["Boosts"]);
        Assert.Equal(first.Data["Boosts"], second.Data["Boosts"]);
    }
}
