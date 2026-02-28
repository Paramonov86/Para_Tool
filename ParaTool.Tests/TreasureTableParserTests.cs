using Xunit;
using ParaTool.Core.Parsing;

namespace ParaTool.Tests;

public class TreasureTableParserTests
{
    [Fact]
    public void Parse_SingleTable_CorrectStructure()
    {
        var text = """
            new treasuretable "REL_Rare_Rings"
            new subtable "-1"
            object category "I_MAG_Ring01",1,0,0,0,0,0,0,0
            object category "I_MAG_Ring02",1,0,0,0,0,0,0,0
            """;

        var doc = TreasureTableParser.Parse(text);

        Assert.Single(doc.Tables);
        Assert.Equal("REL_Rare_Rings", doc.Tables[0].Name);
        Assert.Single(doc.Tables[0].Subtables);
        Assert.Equal("-1", doc.Tables[0].Subtables[0].Spec);
        Assert.Equal(2, doc.Tables[0].Subtables[0].Items.Count);
    }

    [Fact]
    public void Parse_ParagonTable_MultipleSubtables()
    {
        var text = """
            new treasuretable "AMP_Para_9"
            new subtable "1,1"
            object category "I_MAG_Ring01",1,0,0,0,0,0,0,0
            new subtable "1,1"
            object category "I_MAG_Ring02",1,0,0,0,0,0,0,0
            """;

        var doc = TreasureTableParser.Parse(text);

        Assert.Single(doc.Tables);
        Assert.Equal(2, doc.Tables[0].Subtables.Count);
        Assert.All(doc.Tables[0].Subtables, s => Assert.Equal("1,1", s.Spec));
    }

    [Fact]
    public void Parse_MultipleTables()
    {
        var text = """
            new treasuretable "Table1"
            new subtable "-1"
            object category "I_Item1",1,0,0,0,0,0,0,0

            new treasuretable "Table2"
            new subtable "-1"
            object category "I_Item2",1,0,0,0,0,0,0,0
            """;

        var doc = TreasureTableParser.Parse(text);

        Assert.Equal(2, doc.Tables.Count);
        Assert.Equal("Table1", doc.Tables[0].Name);
        Assert.Equal("Table2", doc.Tables[1].Name);
    }
}
