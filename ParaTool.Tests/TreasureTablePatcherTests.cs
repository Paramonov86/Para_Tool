using Xunit;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;

namespace ParaTool.Tests;

public class TreasureTablePatcherTests
{
    /// <summary>
    /// Helper: creates a minimal treasure table with given name, a subtable "-1" and one existing item.
    /// </summary>
    private static string MakePoolTable(string name, string existingItem = "I_ExistingItem")
    {
        return $"""
            new treasuretable "{name}"
            new subtable "-1"
            object category "{existingItem}",1,0,0,0,0,0,0,0
            """;
    }

    /// <summary>
    /// Helper: creates a minimal paragon table with given name and one existing subtable "1,1".
    /// </summary>
    private static string MakeParagonTable(string name, string existingItem = "I_ExistingParagon")
    {
        return $"""
            new treasuretable "{name}"
            new subtable "1,1"
            object category "{existingItem}",1,0,0,0,0,0,0,0
            """;
    }

    [Fact]
    public void Patch_SingleItem_InsertsIntoCorrectTables()
    {
        var original = string.Join("\n",
            MakePoolTable("REL_Rare_Rings"),
            MakePoolTable("REL_All_Rare"),
            MakePoolTable("REL_Rare_Arcane"),
            MakeParagonTable("AMP_Para_9"),
            MakeParagonTable("AMP_Para_16"));

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

        var objLine = "object category \"I_MAG_TestRing\",1,0,0,0,0,0,0,0";

        // All original tables still exist
        Assert.Contains("new treasuretable \"REL_Rare_Rings\"", result);
        Assert.Contains("new treasuretable \"REL_All_Rare\"", result);
        Assert.Contains("new treasuretable \"REL_Rare_Arcane\"", result);
        Assert.Contains("new treasuretable \"AMP_Para_9\"", result);
        Assert.Contains("new treasuretable \"AMP_Para_16\"", result);

        // Item was inserted
        Assert.Contains(objLine, result);

        // Existing items preserved
        Assert.Contains("I_ExistingItem", result);
        Assert.Contains("I_ExistingParagon", result);
    }

    [Fact]
    public void Patch_VeryRareItem_UsesEpicInTableName()
    {
        var original = string.Join("\n",
            MakePoolTable("REL_Epic_Armor"),
            MakePoolTable("REL_All_Epic"),
            MakeParagonTable("AMP_Para_2"));

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

        var result = TreasureTablePatcher.Patch(original, items);

        Assert.Contains("object category \"I_MAG_TestArmor\"", result);

        // Verify the item appears within Epic tables, not VeryRare
        var lines = result.Split('\n');
        bool foundInEpicArmor = false;
        bool inEpicArmor = false;
        for (int i = 0; i < lines.Length; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("new treasuretable \"REL_Epic_Armor\"")) inEpicArmor = true;
            else if (t.StartsWith("new treasuretable ")) inEpicArmor = false;
            if (inEpicArmor && t.Contains("I_MAG_TestArmor")) foundInEpicArmor = true;
        }
        Assert.True(foundInEpicArmor, "Item should be inside REL_Epic_Armor table");
    }

    [Fact]
    public void Patch_DisabledItem_IsSkipped()
    {
        var original = MakePoolTable("REL_Rare_Rings");

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

        var result = TreasureTablePatcher.Patch(original, items);

        Assert.DoesNotContain("MAG_Skipped", result);
    }

    [Fact]
    public void Patch_Weapon1H_AddsToWeaponsAndWeapons1H()
    {
        var original = string.Join("\n",
            MakePoolTable("REL_Uncommon_Weapons_1H"),
            MakePoolTable("REL_Uncommon_Weapons"),
            MakePoolTable("REL_All_Uncommon"),
            MakeParagonTable("AMP_Para_11"),
            MakeParagonTable("AMP_Para_10"));

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

        var result = TreasureTablePatcher.Patch(original, items);

        // Should be in both Weapons and Weapons_1H pool tables
        Assert.Contains("new treasuretable \"REL_Uncommon_Weapons_1H\"", result);
        Assert.Contains("new treasuretable \"REL_Uncommon_Weapons\"", result);

        // Count occurrences of the object line — should be in multiple tables
        var count = result.Split('\n').Count(l => l.TrimStart().Contains("I_WPN_TestSword"));
        Assert.True(count >= 3, $"Expected item in at least 3 tables, found in {count}");
    }

    [Fact]
    public void Patch_ParagonEntries_UseSubtable11()
    {
        var original = string.Join("\n",
            MakePoolTable("REL_Legendary_Boots"),
            MakePoolTable("REL_All_Legendary"),
            MakeParagonTable("AMP_Para_7"));

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

        var result = TreasureTablePatcher.Patch(original, items);

        // Paragon table should have the new item with subtable "1,1"
        var lines = result.Split('\n');
        bool inPara7 = false;
        bool foundNewSubtable = false;
        bool foundItemAfterSubtable = false;
        for (int i = 0; i < lines.Length; i++)
        {
            var t = lines[i].TrimStart();
            if (t.StartsWith("new treasuretable \"AMP_Para_7\"")) inPara7 = true;
            else if (t.StartsWith("new treasuretable ")) inPara7 = false;

            if (inPara7 && t == "new subtable \"1,1\"" && !foundNewSubtable)
            {
                // Skip the existing one (first subtable "1,1" is for ExistingParagon)
                if (i + 1 < lines.Length && lines[i + 1].TrimStart().Contains("I_MAG_TestItem"))
                {
                    foundNewSubtable = true;
                    foundItemAfterSubtable = true;
                }
            }
        }
        Assert.True(foundNewSubtable && foundItemAfterSubtable,
            "AMP_Para_7 should contain a new subtable \"1,1\" block with the test item");
    }

    [Fact]
    public void Patch_DuplicateItems_NotInsertedTwice()
    {
        var original = string.Join("\n",
            MakePoolTable("REL_Rare_Rings", "I_MAG_AlreadyHere"),
            MakePoolTable("REL_All_Rare"),
            MakeParagonTable("AMP_Para_9"));

        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "MAG_AlreadyHere",
                StatType = "Armor",
                DetectedPool = "Rings",
                DetectedRarity = "Rare",
                Enabled = true
            }
        };

        var result = TreasureTablePatcher.Patch(original, items);

        // Count occurrences in REL_Rare_Rings — should be exactly 1 (the existing one)
        var lines = result.Split('\n');
        bool inTable = false;
        int count = 0;
        foreach (var line in lines)
        {
            var t = line.TrimStart();
            if (t.StartsWith("new treasuretable \"REL_Rare_Rings\"")) inTable = true;
            else if (t.StartsWith("new treasuretable ")) inTable = false;
            if (inTable && t.Contains("I_MAG_AlreadyHere")) count++;
        }
        Assert.Equal(1, count);
    }

    [Fact]
    public void Patch_PreservesOriginalContent()
    {
        var original = string.Join("\n",
            "// Header comment",
            MakePoolTable("REL_Rare_Boots"),
            "// Middle comment",
            MakePoolTable("REL_All_Rare"),
            MakeParagonTable("AMP_Para_7"));

        var items = new List<ItemEntry>
        {
            new()
            {
                StatId = "MAG_NewBoots",
                StatType = "Armor",
                DetectedPool = "Boots",
                DetectedRarity = "Rare",
                Enabled = true
            }
        };

        var result = TreasureTablePatcher.Patch(original, items);

        // Comments preserved
        Assert.Contains("// Header comment", result);
        Assert.Contains("// Middle comment", result);

        // Existing items preserved
        Assert.Contains("I_ExistingItem", result);
        Assert.Contains("I_ExistingParagon", result);

        // New item added
        Assert.Contains("I_MAG_NewBoots", result);
    }
}
