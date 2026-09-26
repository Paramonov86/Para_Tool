using Xunit;
using ParaTool.Core.Parsing;

namespace ParaTool.Tests;

public class StatsResolverTests
{
    [Fact]
    public void Resolve_DirectProperty_ReturnsValue()
    {
        var resolver = new StatsResolver();
        resolver.AddEntries(new[]
        {
            new StatsEntry { Name = "TestItem", Type = "Armor", Data = new() { ["Slot"] = "Ring" } }
        });

        Assert.Equal("Ring", resolver.Resolve("TestItem", "Slot"));
    }

    [Fact]
    public void Resolve_InheritedProperty_ReturnsParentValue()
    {
        var resolver = new StatsResolver();
        resolver.AddEntries(new[]
        {
            new StatsEntry { Name = "_Base", Type = "Armor", Data = new() { ["Slot"] = "Breast", ["ArmorType"] = "None" } },
            new StatsEntry { Name = "Child", Type = "Armor", Using = "_Base", Data = new() { ["ArmorType"] = "Leather" } }
        });

        Assert.Equal("Breast", resolver.Resolve("Child", "Slot")); // inherited
        Assert.Equal("Leather", resolver.Resolve("Child", "ArmorType")); // overridden
    }

    [Fact]
    public void Resolve_ThreeLevelChain_Works()
    {
        var resolver = new StatsResolver();
        resolver.AddEntries(new[]
        {
            new StatsEntry { Name = "Root", Type = "Armor", Data = new() { ["Slot"] = "Breast" } },
            new StatsEntry { Name = "Mid", Type = "Armor", Using = "Root", Data = new() { ["Rarity"] = "Rare" } },
            new StatsEntry { Name = "Leaf", Type = "Armor", Using = "Mid", Data = new() { ["ArmorType"] = "Plate" } }
        });

        Assert.Equal("Breast", resolver.Resolve("Leaf", "Slot"));
        Assert.Equal("Rare", resolver.Resolve("Leaf", "Rarity"));
        Assert.Equal("Plate", resolver.Resolve("Leaf", "ArmorType"));
    }

    [Fact]
    public void Resolve_NonExistentProperty_ReturnsNull()
    {
        var resolver = new StatsResolver();
        resolver.AddEntries(new[]
        {
            new StatsEntry { Name = "TestItem", Type = "Armor", Data = new() { ["Slot"] = "Ring" } }
        });

        Assert.Null(resolver.Resolve("TestItem", "NonExistent"));
    }

    [Fact]
    public void ResolveAll_MergesAllLevels()
    {
        var resolver = new StatsResolver();
        resolver.AddEntries(new[]
        {
            new StatsEntry { Name = "Parent", Type = "Armor", Data = new() { ["Slot"] = "Breast", ["Weight"] = "5" } },
            new StatsEntry { Name = "Child", Type = "Armor", Using = "Parent", Data = new() { ["Rarity"] = "Rare", ["Weight"] = "3" } }
        });

        var all = resolver.ResolveAll("Child");

        Assert.Equal("Breast", all["Slot"]);
        Assert.Equal("3", all["Weight"]); // child overrides parent
        Assert.Equal("Rare", all["Rarity"]);
    }

    // A later `new entry "X"` that `using "X"` extends the X it replaces (later vanilla layers,
    // AMP's spell rebalances writing only a Cooldown) instead of wiping its fields.
    private static StatsResolver WithRebalance() => Build(
        new StatsEntry { Name = "Shout_Base", Type = "SpellData", Data = new() { ["Icon"] = "Base" } },
        new StatsEntry { Name = "Shout_X", Type = "SpellData", Using = "Shout_Base",
            Data = new() { ["SpellProperties"] = "DealDamage(1d6,Fire)", ["Cooldown"] = "OncePerTurn" } },
        new StatsEntry { Name = "Shout_X", Type = "SpellData", Using = "Shout_X",
            Data = new() { ["Cooldown"] = "OncePerCombat" } },
        new StatsEntry { Name = "Shout_X_Child", Type = "SpellData", Using = "Shout_X",
            Data = new() { ["UseCosts"] = "ActionPoint:1" } });

    private static StatsResolver Build(params StatsEntry[] entries)
    {
        var resolver = new StatsResolver();
        resolver.AddEntries(entries);
        return resolver;
    }

    [Fact]
    public void SelfUsingRedefinition_ExtendsTheDefinitionItReplaced()
    {
        var resolver = WithRebalance();

        Assert.Equal("OncePerCombat", resolver.Resolve("Shout_X", "Cooldown"));
        Assert.Equal("DealDamage(1d6,Fire)", resolver.Resolve("Shout_X", "SpellProperties"));
        Assert.Equal("Base", resolver.Resolve("Shout_X", "Icon"));
        var all = resolver.ResolveAll("Shout_X");
        Assert.Equal("OncePerCombat", all["Cooldown"]);
        Assert.Equal("DealDamage(1d6,Fire)", all["SpellProperties"]);
        Assert.Equal("Base", all["Icon"]);
        // The entry itself stays as parsed: the patcher tells overrides apart by `using <self>`.
        Assert.Equal("Shout_X", resolver.Get("Shout_X")!.Using);
    }

    [Fact]
    public void ChildOfSelfUsingRedefinition_SeesBothLayers()
    {
        var all = WithRebalance().ResolveAll("Shout_X_Child");

        Assert.Equal("ActionPoint:1", all["UseCosts"]);
        Assert.Equal("OncePerCombat", all["Cooldown"]);
        Assert.Equal("DealDamage(1d6,Fire)", all["SpellProperties"]);
        Assert.Equal("Base", all["Icon"]);
    }

    [Fact]
    public void RedefinitionWithoutSelfUsing_StillReplaces()
    {
        var resolver = Build(
            new StatsEntry { Name = "Shout_X", Type = "SpellData", Data = new() { ["Icon"] = "Old", ["Cooldown"] = "OncePerTurn" } },
            new StatsEntry { Name = "Shout_X", Type = "SpellData", Data = new() { ["Icon"] = "New" } });

        Assert.Equal("New", resolver.Resolve("Shout_X", "Icon"));
        Assert.Null(resolver.Resolve("Shout_X", "Cooldown"));
    }

    [Fact]
    public void SelfUsingWithNothingBefore_ResolvesItsOwnData()
    {
        var resolver = Build(
            new StatsEntry { Name = "Shout_X", Type = "SpellData", Using = "Shout_X", Data = new() { ["Icon"] = "Own" } });

        Assert.Equal("Own", resolver.Resolve("Shout_X", "Icon"));
        Assert.Null(resolver.Resolve("Shout_X", "Cooldown"));
        Assert.Single(resolver.ResolveAll("Shout_X"));
    }

    [Fact]
    public void CopyingDefinitions_KeepsTheLayersSelfUsingEntriesExtend()
    {
        var copy = new StatsResolver();
        copy.AddEntries(WithRebalance().Definitions);

        Assert.Equal("OncePerCombat", copy.Resolve("Shout_X", "Cooldown"));
        Assert.Equal("DealDamage(1d6,Fire)", copy.Resolve("Shout_X_Child", "SpellProperties"));
        Assert.Equal(3, copy.AllEntries.Count);
    }

    [Fact]
    public void Vanilla_LaterLayerRedefinition_KeepsEarlierFields()
    {
        // The embedded dump redefines Target_SoberingRealisation in a later layer with `using` itself
        // and two data lines; its SpellProperties, SpellRoll and Icon live in the earlier definition.
        var db = new ParaTool.Core.Services.VanillaDatabase();
        db.Load();

        Assert.False(string.IsNullOrEmpty(db.Resolver.Resolve("Target_SoberingRealisation", "SpellProperties")));
        Assert.False(string.IsNullOrEmpty(db.Resolver.Resolve("Target_SoberingRealisation", "Icon")));
    }

    // ── Names differing only in case are different entries, as in the game ──────────────
    // AMP has the weapon WPN_Longsword_l and the status WPN_LONGSWORD_L, the passive
    // MAG_Weapon57_StatusHeal and the status MAG_WEAPON57_STATUSHEAL; the game itself has the
    // spells Projectile_Jump and Projectile_JUMP. No stats name repeats exactly across types.

    private static StatsEntry E(string name, string type, string? usingBase = null, params (string k, string v)[] data) =>
        new() { Name = name, Type = type, Using = usingBase, Data = data.ToDictionary(d => d.k, d => d.v) };

    [Fact]
    public void CaseVariants_OfDifferentTypes_AreBothKept()
    {
        var r = new StatsResolver();
        r.AddEntries([
            E("WPN_Longsword_l", "Weapon", null, ("Damage", "1d8")),
            E("WPN_LONGSWORD_L", "StatusData", null, ("StatusType", "BOOST"), ("StackPriority", "4")),
        ]);

        Assert.Equal("Weapon", r.Get("WPN_Longsword_l")!.Type);
        Assert.Equal("StatusData", r.Get("WPN_LONGSWORD_L")!.Type);
        Assert.Equal("BOOST", r.ResolveAll("WPN_LONGSWORD_L")["StatusType"]);
        Assert.False(r.ResolveAll("WPN_Longsword_l").ContainsKey("StatusType"));
        Assert.Equal(2, r.AllEntries.Count);
        Assert.Equal(2, r.Definitions.Count());
    }

    [Fact]
    public void CaseVariants_OfTheSameType_AreBothKept()
    {
        var r = new StatsResolver();
        r.AddEntries([E("Projectile_Jump", "SpellData", null, ("Level", "0")), E("Projectile_JUMP", "SpellData", null, ("Level", "1"))]);
        Assert.Equal("0", r.Resolve("Projectile_Jump", "Level"));
        Assert.Equal("1", r.Resolve("Projectile_JUMP", "Level"));
    }

    [Fact]
    public void ALaterCaseVariant_IsNotALayerOfTheEarlierOne()
    {
        // A self-using entry extends the earlier definition of its own name — never one that
        // only matches ignoring case.
        var r = new StatsResolver();
        r.AddEntries([
            E("MAG_Weapon57_StatusHeal", "PassiveData", null, ("Boosts", "AC(1)")),
            E("MAG_WEAPON57_STATUSHEAL", "StatusData", "MAG_WEAPON57_STATUSHEAL", ("StatusType", "BOOST")),
        ]);
        Assert.False(r.ResolveAll("MAG_WEAPON57_STATUSHEAL").ContainsKey("Boosts"));
        Assert.Equal("AC(1)", r.ResolveAll("MAG_Weapon57_StatusHeal")["Boosts"]);
    }

    [Fact]
    public void Using_ResolvesTheExactName()
    {
        var r = new StatsResolver();
        r.AddEntries([
            E("WPN_Longsword_u", "Weapon", null, ("Damage", "1d8")),
            E("WPN_LONGSWORD_U", "StatusData", null, ("StatusType", "BOOST"), ("Boosts", "DamageBonus(1)")),
            E("WPN_LONGSWORD_E", "StatusData", "WPN_LONGSWORD_U", ("StackPriority", "3")),
            E("WPN_Longsword_e", "Weapon", "WPN_Longsword_u", ("Damage", "1d10")),
        ]);
        Assert.Equal("DamageBonus(1)", r.ResolveAll("WPN_LONGSWORD_E")["Boosts"]);
        Assert.False(r.ResolveAll("WPN_Longsword_e").ContainsKey("Boosts"));
    }

    [Fact]
    public void ANameInAnotherCase_StillFindsTheOnlyMatch()
    {
        // Names typed or read in another case (a user's StatId, an old save) find their entry
        // when there is just one; they never pick one of two case variants at random.
        var r = new StatsResolver();
        r.AddEntries([E("BLESS", "StatusData", null, ("StatusType", "BOOST")),
                      E("Foo_a", "Armor"), E("FOO_A", "StatusData")]);
        Assert.Equal("BLESS", r.Get("bless")!.Name);
        Assert.True(r.AllEntries.ContainsKey("Bless"));
        Assert.Equal("BOOST", r.Resolve("Bless", "StatusType"));
        Assert.Equal("Foo_a", r.Get("Foo_a")!.Name);
        Assert.Equal("FOO_A", r.Get("FOO_A")!.Name);
        Assert.Null(r.Get("foo_A"));
    }

    [Fact]
    public void AnExactDuplicate_OfAnItem_StillLeavesTheItem()
    {
        var r = new StatsResolver();
        r.AddEntries([E("ARM_Ring_A", "Armor", null, ("Slot", "Ring")), E("ARM_Ring_A", "PassiveData")]);
        Assert.Equal("Armor", r.Get("ARM_Ring_A")!.Type);
    }

    [Fact]
    public void GamesOwnCaseVariantSpells_StayTwoSpells()
    {
        // Projectile_JUMP is a ranged attack using Projectile_MainHandAttack; Projectile_Jump is the jump.
        var db = new ParaTool.Core.Services.VanillaDatabase();
        db.Load();
        var attack = db.Resolver.ResolveAll("Projectile_JUMP");
        var jump = db.Resolver.ResolveAll("Projectile_Jump");
        Assert.Equal("Projectile_MainHandAttack", db.Resolver.Get("Projectile_JUMP")!.Using);
        Assert.Null(db.Resolver.Get("Projectile_Jump")!.Using);
        Assert.Equal("0.8", jump.GetValueOrDefault("TargetCeiling"));
        Assert.NotEqual(attack.GetValueOrDefault("TargetCeiling"), jump.GetValueOrDefault("TargetCeiling"));
    }
}
