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
}
