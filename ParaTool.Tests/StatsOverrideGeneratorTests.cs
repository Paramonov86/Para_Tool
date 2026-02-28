using Xunit;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;

namespace ParaTool.Tests;

public class StatsOverrideGeneratorTests
{
    [Fact]
    public void Generate_ProducesCorrectFormat()
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

        var result = StatsOverrideGenerator.Generate(items);

        Assert.Contains("new entry \"MAG_Ring99\"", result);
        Assert.Contains("type \"Armor\"", result);
        Assert.Contains("using \"MAG_Ring99\"", result);
        Assert.Contains("data \"ValueOverride\" \"400\"", result); // Ring + Rare = 400
        Assert.Contains("data \"Unique\" \"\"", result);
    }

    [Fact]
    public void Generate_DisabledItem_IsSkipped()
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

        var result = StatsOverrideGenerator.Generate(items);

        Assert.DoesNotContain("MAG_Skipped", result);
    }

    [Fact]
    public void Generate_WeaponVeryRare_CorrectPrice()
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

        var result = StatsOverrideGenerator.Generate(items);

        Assert.Contains("data \"ValueOverride\" \"1100\"", result); // Weapon + VeryRare = 1100
    }
}
