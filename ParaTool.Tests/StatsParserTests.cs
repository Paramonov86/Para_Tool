using Xunit;
using ParaTool.Core.Parsing;

namespace ParaTool.Tests;

public class StatsParserTests
{
    [Fact]
    public void Parse_SimpleEntry_ReturnsCorrectFields()
    {
        var text = """
            new entry "ARM_TestArmor"
            type "Armor"
            using "_Body"
            data "Slot" "Breast"
            data "ArmorType" "Leather"
            data "Rarity" "Rare"
            """;

        var entries = StatsParser.Parse(text);

        Assert.Single(entries);
        Assert.Equal("ARM_TestArmor", entries[0].Name);
        Assert.Equal("Armor", entries[0].Type);
        Assert.Equal("_Body", entries[0].Using);
        Assert.Equal("Breast", entries[0].Data["Slot"]);
        Assert.Equal("Leather", entries[0].Data["ArmorType"]);
        Assert.Equal("Rare", entries[0].Data["Rarity"]);
    }

    [Fact]
    public void Parse_MultipleEntries_ReturnsAll()
    {
        var text = """
            new entry "Item1"
            type "Armor"
            data "Slot" "Ring"

            new entry "Item2"
            type "Weapon"
            data "Slot" "Melee Main Weapon"
            """;

        var entries = StatsParser.Parse(text);

        Assert.Equal(2, entries.Count);
        Assert.Equal("Item1", entries[0].Name);
        Assert.Equal("Item2", entries[1].Name);
        Assert.Equal("Armor", entries[0].Type);
        Assert.Equal("Weapon", entries[1].Type);
    }

    [Fact]
    public void Parse_EntryWithoutUsing_UsingShouldBeNull()
    {
        var text = """
            new entry "BaseItem"
            type "Armor"
            data "Slot" "Breast"
            """;

        var entries = StatsParser.Parse(text);

        Assert.Single(entries);
        Assert.Null(entries[0].Using);
    }

    [Fact]
    public void Parse_EmptyDataValue_IsEmptyString()
    {
        var text = """
            new entry "TestItem"
            type "Armor"
            data "Unique" ""
            """;

        var entries = StatsParser.Parse(text);

        Assert.Single(entries);
        Assert.Equal("", entries[0].Data["Unique"]);
    }

    [Fact]
    public void Parse_DuplicateDataKey_LastValueWins()
    {
        var text = """
            new entry "TestItem"
            type "Armor"
            data "Slot" "Ring"
            data "Slot" "Boots"
            """;

        var entries = StatsParser.Parse(text);

        Assert.Equal("Boots", entries[0].Data["Slot"]);
    }
}
