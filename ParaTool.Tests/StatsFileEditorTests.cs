using Xunit;
using ParaTool.Core.Patching;

namespace ParaTool.Tests;

public class StatsFileEditorTests
{
    private const string SampleStatFile = """
        new entry "ARM_Shield_Base"
        type "Armor"
        using "_Shield"
        data "ArmorClass" "2"
        data "Rarity" "Legendary"
        data "Unique" "1"

        new entry "ARM_Cloth_Robe"
        type "Armor"
        using "_Body"
        data "ArmorType" "Cloth"
        data "Rarity" "VeryRare"
        data "ValueOverride" "800"
        """;

    [Fact]
    public void ModifyEntries_ReplacesExistingFields()
    {
        var mods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ARM_Shield_Base"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Rarity"] = "Rare",
                ["Unique"] = ""
            }
        };

        var (result, modified) = StatsFileEditor.ModifyEntries(SampleStatFile, mods);

        Assert.Contains("ARM_Shield_Base", modified);
        Assert.Contains("data \"Rarity\" \"Rare\"", result);
        Assert.Contains("data \"Unique\" \"\"", result);
        // Other entry unchanged
        Assert.Contains("data \"Rarity\" \"VeryRare\"", result);
    }

    [Fact]
    public void ModifyEntries_AddsNewFields()
    {
        var mods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ARM_Shield_Base"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["ValueOverride"] = "500"
            }
        };

        var (result, modified) = StatsFileEditor.ModifyEntries(SampleStatFile, mods);

        Assert.Contains("ARM_Shield_Base", modified);
        Assert.Contains("data \"ValueOverride\" \"500\"", result);
    }

    [Fact]
    public void ModifyEntries_HandlesMultipleEntries()
    {
        var mods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ARM_Shield_Base"] = new(StringComparer.OrdinalIgnoreCase) { ["Rarity"] = "Uncommon" },
            ["ARM_Cloth_Robe"] = new(StringComparer.OrdinalIgnoreCase) { ["Rarity"] = "Rare" }
        };

        var (result, modified) = StatsFileEditor.ModifyEntries(SampleStatFile, mods);

        Assert.Equal(2, modified.Count);
        // Check both entries got modified (Rare for shield, Rare for robe)
        // The original VeryRare should be gone
        Assert.DoesNotContain("\"VeryRare\"", result);
        Assert.DoesNotContain("\"Legendary\"", result);
    }

    [Fact]
    public void ModifyEntries_EntryNotFound_ReturnsEmpty()
    {
        var mods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["NONEXISTENT_Item"] = new(StringComparer.OrdinalIgnoreCase) { ["Rarity"] = "Rare" }
        };

        var (result, modified) = StatsFileEditor.ModifyEntries(SampleStatFile, mods);

        Assert.Empty(modified);
        Assert.Equal(SampleStatFile, result);
    }

    [Fact]
    public void ModifyEntries_DuplicateEntriesInFile_ModifiesAll()
    {
        // BG3 skeleton pattern: same entry appears twice in the same file
        var text = """
            new entry "ARM_Test"
            type "Armor"
            using "_Shield"
            data "ArmorClass" "3"
            data "Rarity" "Legendary"

            new entry "ARM_Test"
            type "Armor"
            using "ARM_Test"
            data "RootTemplate" "abc-123"
            """;

        var mods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["ARM_Test"] = new(StringComparer.OrdinalIgnoreCase)
            {
                ["Rarity"] = "Rare",
                ["ValueOverride"] = "400"
            }
        };

        var (result, modified) = StatsFileEditor.ModifyEntries(text, mods);

        Assert.Contains("ARM_Test", modified);
        Assert.Contains("data \"Rarity\" \"Rare\"", result);
        Assert.Contains("data \"ValueOverride\" \"400\"", result);
        Assert.DoesNotContain("\"Legendary\"", result);
    }

    [Fact]
    public void AppendSkeletonEntries_AddsToEnd()
    {
        var text = "new entry \"ARM_Test\"\ntype \"Armor\"\n";
        var skeleton = "new entry \"MOD_Item\"\ntype \"Armor\"\nusing \"MOD_Item\"\n";

        var result = StatsFileEditor.AppendSkeletonEntries(text, skeleton);

        Assert.EndsWith(skeleton, result);
        Assert.Contains("ARM_Test", result);
    }

    // Stats names are case-sensitive: AMP has the weapon WPN_Longsword_l and the status
    // WPN_LONGSWORD_L, the passive MAG_Weapon57_StatusHeal and the status MAG_WEAPON57_STATUSHEAL.
    private const string CaseVariants =
        "new entry \"WPN_Longsword_l\"\ntype \"Weapon\"\ndata \"Rarity\" \"Rare\"\n\n" +
        "new entry \"WPN_LONGSWORD_L\"\ntype \"StatusData\"\ndata \"StatusType\" \"BOOST\"\n\n" +
        "new entry \"MAG_Weapon57_StatusHeal\"\ntype \"PassiveData\"\ndata \"Boosts\" \"AC(1)\"\n\n" +
        "new entry \"MAG_WEAPON57_STATUSHEAL\"\ntype \"StatusData\"\ndata \"StatusType\" \"BOOST\"\n";

    [Fact]
    public void ModifyEntries_LeavesACaseVariantOfAnotherType_Alone()
    {
        var mods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["WPN_Longsword_l"] = new() { ["Rarity"] = "Legendary" },
        };
        var (text, found) = StatsFileEditor.ModifyEntries(CaseVariants, mods);
        var status = text[text.IndexOf("new entry \"WPN_LONGSWORD_L\"")..text.IndexOf("new entry \"MAG_Weapon57")];
        Assert.DoesNotContain("Rarity", status);
        Assert.Contains("data \"Rarity\" \"Legendary\"", text);
        Assert.Equal(["WPN_Longsword_l"], found);
    }

    [Fact]
    public void RemoveEntries_RemovesOnlyTheExactName()
    {
        var text = StatsFileEditor.RemoveEntries(CaseVariants, new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "MAG_WEAPON57_STATUSHEAL" });
        Assert.Contains("new entry \"MAG_Weapon57_StatusHeal\"", text);
        Assert.DoesNotContain("new entry \"MAG_WEAPON57_STATUSHEAL\"", text);
    }
}
