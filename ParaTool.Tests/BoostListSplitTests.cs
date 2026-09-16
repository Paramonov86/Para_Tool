using ParaTool.Core.Schema;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// A boost list is ";"-separated, but an argument can hold its own ";"-list
/// (<c>BlockRegainHP(Undead;Construct)</c>). Splitting blindly turned that into two broken boosts.
/// </summary>
public class BoostListSplitTests
{
    [Fact]
    public void SplitBoostList_KeepsSemicolonsInsideParentheses()
    {
        Assert.Equal(["BlockRegainHP(Undead;Construct)"], BoostMapping.SplitBoostList("BlockRegainHP(Undead;Construct)"));
        Assert.Equal(["AC(2)", "BlockRegainHP(Undead;Construct)", "Advantage(AttackRoll)"],
            BoostMapping.SplitBoostList("AC(2);BlockRegainHP(Undead;Construct);Advantage(AttackRoll)"));
    }

    [Fact]
    public void SplitBoostList_KeepsSemicolonsInsideQuotes()
    {
        Assert.Equal(["GROUND:Summon(9c2ed0d0,-1,,,'Stack;Id')"],
            BoostMapping.SplitBoostList("GROUND:Summon(9c2ed0d0,-1,,,'Stack;Id')"));
    }

    [Fact]
    public void SplitBoostList_TrimsAndDropsEmpties()
    {
        Assert.Equal(["AC(2)", "Ability(Strength,2)"], BoostMapping.SplitBoostList(" AC(2) ;; Ability(Strength,2);"));
        Assert.Empty(BoostMapping.SplitBoostList(""));
        Assert.Empty(BoostMapping.SplitBoostList(null));
    }

    [Fact]
    public void SanitizeBoosts_LeavesAListArgumentAlone()
    {
        const string raw = "BlockRegainHP(Undead;Construct)";
        Assert.Equal(raw, BoostMapping.SanitizeBoosts(raw));
    }

    [Fact]
    public void ParseBoostCall_ReadsTheWholeListArgument()
    {
        var parsed = BoostMapping.ParseBoostCall("BlockRegainHP(Undead;Construct)");
        Assert.NotNull(parsed);
        Assert.Equal("BlockRegainHP", parsed!.Value.funcName);
        Assert.Equal(["Undead;Construct"], parsed.Value.args);
    }

    [Fact]
    public void Summon_HasItsFullSignature()
    {
        var summon = BoostMapping.Functors.First(f => f.FuncName == "Summon");

        Assert.Equal(11, summon.Params.Length);
        Assert.Equal("guid", summon.Params[0].Type);
        Assert.Equal(["Template", "Duration", "AiSpellOverride", "ExtendExistingConcentration", "StackId",
                      "StatusToApply1", "StatusToApply2", "StatusToApply3", "StatusToApply4",
                      "LateJoinPenalty", "UseOwnerPassives"],
            summon.Params.Select(p => p.Name));
    }
}
