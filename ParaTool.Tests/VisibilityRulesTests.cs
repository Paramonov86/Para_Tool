using ParaTool.Core.Schema;
using Xunit;

namespace ParaTool.Tests;

public class VisibilityRulesTests
{
    [Fact]
    public void Advantage_AttackRoll_HidesAndClearsSecondArg()
    {
        var def = BoostMapping.Boosts.First(d => d.FuncName == "Advantage");
        string[] args = ["AttackRoll", "Strength"];
        var cleared = VisibilityRules.ClearHiddenArgs(def, args);
        Assert.Equal("", cleared[1]);
        Assert.True(VisibilityRules.IsHidden(def, 1, cleared));
    }

    [Fact]
    public void Advantage_SavingThrow_KeepsSecondArg()
    {
        var def = BoostMapping.Boosts.First(d => d.FuncName == "Advantage");
        string[] args = ["SavingThrow", "Dexterity"];
        var cleared = VisibilityRules.ClearHiddenArgs(def, args);
        Assert.Equal("Dexterity", cleared[1]);
        Assert.False(VisibilityRules.IsHidden(def, 1, cleared));
    }

    [Fact]
    public void DamageReduction_Half_ClearsThirdArg()
    {
        var def = BoostMapping.Boosts.First(d => d.FuncName == "DamageReduction");
        string[] args = ["Bludgeoning", "Half", "5"];
        var cleared = VisibilityRules.ClearHiddenArgs(def, args);
        Assert.Equal("", cleared[2]);
    }

    [Fact]
    public void DamageReduction_Flat_KeepsThirdArg()
    {
        var def = BoostMapping.Boosts.First(d => d.FuncName == "DamageReduction");
        string[] args = ["Bludgeoning", "Flat", "5"];
        var cleared = VisibilityRules.ClearHiddenArgs(def, args);
        Assert.Equal("5", cleared[2]);
    }

    [Fact]
    public void Ability_NonConstitution_ClearsSavant()
    {
        var def = BoostMapping.Boosts.First(d => d.FuncName == "Ability");
        string[] args = ["Strength", "2", "24", "true"];
        var cleared = VisibilityRules.ClearHiddenArgs(def, args);
        Assert.Equal("", cleared[3]);
    }

    [Fact]
    public void Ability_Constitution_KeepsSavant()
    {
        var def = BoostMapping.Boosts.First(d => d.FuncName == "Ability");
        string[] args = ["Constitution", "2", "24", "true"];
        var cleared = VisibilityRules.ClearHiddenArgs(def, args);
        Assert.Equal("true", cleared[3]);
    }
}
