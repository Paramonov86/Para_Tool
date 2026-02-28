using Xunit;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;

namespace ParaTool.Tests;

public class TreasureTablePatcherTests
{
    [Fact]
    public void Patch_SingleItem_AddsToCorrectTables()
    {
        var original = "// Original TreasureTable content\n";

        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "MAG_TestRing",
                StatType = "Armor",
                DetectedPool = "Rings",
                DetectedRarity = "Rare",
                Enabled = true,
                UserThemes = new() { "Arcane" }
            }
        };

        var result = TreasureTablePatcher.Patch(original, items);

        // Layer 1: REL_Rare_Rings
        Assert.Contains("new treasuretable \"REL_Rare_Rings\"", result);
        Assert.Contains("object category \"I_MAG_TestRing\",1,0,0,0,0,0,0,0", result);

        // Layer 2: REL_All_Rare
        Assert.Contains("new treasuretable \"REL_All_Rare\"", result);

        // Layer 3: REL_Rare_Arcane
        Assert.Contains("new treasuretable \"REL_Rare_Arcane\"", result);

        // Layer 4: AMP_Para_9 (Rings)
        Assert.Contains("new treasuretable \"AMP_Para_9\"", result);

        // Layer 4: AMP_Para_16 (Arcane)
        Assert.Contains("new treasuretable \"AMP_Para_16\"", result);
    }

    [Fact]
    public void Patch_VeryRareItem_UsesEpicInTableName()
    {
        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "MAG_TestArmor",
                StatType = "Armor",
                DetectedPool = "Armor",
                DetectedRarity = "VeryRare",
                Enabled = true
            }
        };

        var result = TreasureTablePatcher.Patch("", items);

        Assert.Contains("REL_Epic_Armor", result);
        Assert.Contains("REL_All_Epic", result);
    }

    [Fact]
    public void Patch_DisabledItem_IsSkipped()
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

        var result = TreasureTablePatcher.Patch("", items);

        Assert.DoesNotContain("MAG_Skipped", result);
    }

    [Fact]
    public void Patch_Weapon1H_AddsToWeaponsAndWeapons1H()
    {
        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "WPN_TestSword",
                StatType = "Weapon",
                DetectedPool = "Weapons_1H",
                DetectedRarity = "Uncommon",
                Enabled = true
            }
        };

        var result = TreasureTablePatcher.Patch("", items);

        // Should be in both Weapons and Weapons_1H pools
        Assert.Contains("REL_Uncommon_Weapons_1H", result);
        Assert.Contains("REL_Uncommon_Weapons", result);
        // Paragon: AMP_Para_11 (Weapons_1H) + AMP_Para_10 (Weapons)
        Assert.Contains("AMP_Para_11", result);
        Assert.Contains("AMP_Para_10", result);
    }

    [Fact]
    public void Patch_ParagonEntries_UseSubtable11()
    {
        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "MAG_TestItem",
                StatType = "Armor",
                DetectedPool = "Boots",
                DetectedRarity = "Legendary",
                Enabled = true
            }
        };

        var result = TreasureTablePatcher.Patch("", items);

        // Paragon should use subtable "1,1"
        Assert.Contains("new subtable \"1,1\"", result);
        Assert.Contains("AMP_Para_7", result); // Boots
    }
}
