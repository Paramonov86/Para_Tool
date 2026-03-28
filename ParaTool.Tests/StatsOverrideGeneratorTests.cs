using Xunit;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;

namespace ParaTool.Tests;

public class StatsOverrideGeneratorTests
{
    [Fact]
    public void ComputeFields_ReturnsCorrectValues()
    {
        var item = new ItemEntry
        {
            StatId = "MAG_Ring99",
            StatType = "Armor",
            DetectedPool = "Rings",
            DetectedRarity = "Rare",
            Enabled = true
        };

        var fields = StatsOverrideGenerator.ComputeFields(item);

        Assert.NotNull(fields);
        Assert.Equal("Rare", fields!["Rarity"]);
        Assert.Equal("400", fields["ValueOverride"]); // Ring + Rare = 400
        Assert.Equal("", fields["Unique"]);
    }

    [Fact]
    public void ComputeFields_DisabledItem_ReturnsNull()
    {
        var item = new ItemEntry
        {
            StatId = "MAG_Skipped",
            StatType = "Armor",
            DetectedPool = "Rings",
            DetectedRarity = "Rare",
            Enabled = false
        };

        Assert.Null(StatsOverrideGenerator.ComputeFields(item));
    }

    [Fact]
    public void ComputeFields_CommonRarity_ReturnsNull()
    {
        var item = new ItemEntry
        {
            StatId = "MAG_Common",
            StatType = "Armor",
            DetectedPool = "Rings",
            DetectedRarity = "Common",
            Enabled = true
        };

        Assert.Null(StatsOverrideGenerator.ComputeFields(item));
    }

    [Fact]
    public void GenerateSkeletonEntries_ProducesCorrectFormat()
    {
        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "MAG_Ring99",
                StatType = "Armor",
                DetectedPool = "Rings",
                DetectedRarity = "Rare",
                Enabled = true
            }
        };

        var result = StatsOverrideGenerator.GenerateSkeletonEntries(items);

        Assert.Contains("new entry \"MAG_Ring99\"", result);
        Assert.Contains("type \"Armor\"", result);
        Assert.Contains("using \"MAG_Ring99\"", result);
        Assert.Contains("data \"Rarity\" \"Rare\"", result);
        Assert.Contains("data \"ValueOverride\" \"400\"", result);
        Assert.Contains("data \"Unique\" \"\"", result);
    }

    [Fact]
    public void GenerateSkeletonEntries_DisabledItem_IsSkipped()
    {
        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "MAG_Skipped",
                StatType = "Armor",
                DetectedPool = "Rings",
                DetectedRarity = "Rare",
                Enabled = false
            }
        };

        var result = StatsOverrideGenerator.GenerateSkeletonEntries(items);

        Assert.DoesNotContain("MAG_Skipped", result);
    }

    [Fact]
    public void GenerateSkeletonEntries_WeaponVeryRare_CorrectPrice()
    {
        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "WPN_TestBlade",
                StatType = "Weapon",
                DetectedPool = "Weapons",
                DetectedRarity = "VeryRare",
                Enabled = true
            }
        };

        var result = StatsOverrideGenerator.GenerateSkeletonEntries(items);

        Assert.Contains("data \"ValueOverride\" \"1100\"", result);
    }
}
