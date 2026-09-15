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
}
