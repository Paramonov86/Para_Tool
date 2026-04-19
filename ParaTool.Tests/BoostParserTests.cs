using Xunit;
using ParaTool.Core.Schema;

namespace ParaTool.Tests;

/// <summary>
/// Tests for BoostMapping.ParseBoostCall — the low-level parser that splits
/// raw boost strings into function name + argument array. All preview and
/// UI logic depends on this, so regression coverage is critical.
/// </summary>
public class BoostParserTests
{
    [Fact]
    public void Simple_NoArgs()
    {
        var r = BoostMapping.ParseBoostCall("Invulnerable()");
        Assert.NotNull(r);
        Assert.Equal("Invulnerable", r!.Value.funcName);
        Assert.Empty(r.Value.args);
    }

    [Fact]
    public void Simple_SingleArg()
    {
        var r = BoostMapping.ParseBoostCall("AC(1)");
        Assert.Equal("AC", r!.Value.funcName);
        Assert.Single(r.Value.args);
        Assert.Equal("1", r.Value.args[0]);
    }

    [Fact]
    public void MultipleArgs()
    {
        var r = BoostMapping.ParseBoostCall("CriticalHit(AttackTarget,Success,Never)");
        Assert.Equal("CriticalHit", r!.Value.funcName);
        Assert.Equal(3, r.Value.args.Length);
        Assert.Equal("AttackTarget", r.Value.args[0]);
        Assert.Equal("Success", r.Value.args[1]);
        Assert.Equal("Never", r.Value.args[2]);
    }

    [Fact]
    public void ArgsWithSpaces_AreTrimmed()
    {
        var r = BoostMapping.ParseBoostCall("RollBonus( SavingThrow , 1 , Charisma )");
        Assert.Equal(3, r!.Value.args.Length);
        Assert.Equal("SavingThrow", r.Value.args[0]);
        Assert.Equal("1", r.Value.args[1]);
        Assert.Equal("Charisma", r.Value.args[2]);
    }

    [Fact]
    public void NestedParentheses_NotSplit()
    {
        // Formula with nested parens shouldn't split at inner commas
        var r = BoostMapping.ParseBoostCall("DealDamage((1d4+Level),Fire)");
        Assert.Equal("DealDamage", r!.Value.funcName);
        Assert.Equal(2, r.Value.args.Length);
        Assert.Equal("(1d4+Level)", r.Value.args[0]);
        Assert.Equal("Fire", r.Value.args[1]);
    }

    [Fact]
    public void NestedCommasInFormula_NotSplit()
    {
        // Condition expressions can have commas inside parens
        var r = BoostMapping.ParseBoostCall("IF(SavingThrow(Ability.Charisma,11)):ApplyStatus(X,100,1)");
        Assert.Equal("IF", r!.Value.funcName);
    }

    [Fact]
    public void NegativeNumberArg()
    {
        var r = BoostMapping.ParseBoostCall("Ability(Charisma,-2)");
        Assert.Equal("-2", r!.Value.args[1]);
    }

    [Fact]
    public void DiceFormulaArg()
    {
        var r = BoostMapping.ParseBoostCall("WeaponDamage(1d4,Fire)");
        Assert.Equal("1d4", r!.Value.args[0]);
        Assert.Equal("Fire", r.Value.args[1]);
    }

    [Fact]
    public void NoParens_FuncNameOnly()
    {
        var r = BoostMapping.ParseBoostCall("IgnoreFallDamage");
        Assert.Equal("IgnoreFallDamage", r!.Value.funcName);
        Assert.Empty(r.Value.args);
    }

    [Fact]
    public void TrailingWhitespace_Ignored()
    {
        var r = BoostMapping.ParseBoostCall("  AC(1)  ");
        Assert.Equal("AC", r!.Value.funcName);
    }

    [Fact]
    public void QuotedArg_Preserved()
    {
        // ActionResource uses quoted first arg in vanilla
        var r = BoostMapping.ParseBoostCall("ActionResource('BonusActionPoint',1,0)");
        Assert.Equal(3, r!.Value.args.Length);
        Assert.Equal("'BonusActionPoint'", r.Value.args[0]);
    }

    [Fact]
    public void DeeplyNested_SplitAtRightLevel()
    {
        var r = BoostMapping.ParseBoostCall("X(a,b(c,d(e,f)),g)");
        Assert.Equal(3, r!.Value.args.Length);
        Assert.Equal("a", r.Value.args[0]);
        Assert.Equal("b(c,d(e,f))", r.Value.args[1]);
        Assert.Equal("g", r.Value.args[2]);
    }

    [Fact]
    public void FindBoost_KnownName_Returns()
    {
        Assert.NotNull(BoostMapping.FindBoost("Ability"));
        Assert.NotNull(BoostMapping.FindBoost("CriticalHit"));
        Assert.NotNull(BoostMapping.FindBoost("RollBonus"));
    }

    [Fact]
    public void FindBoost_CaseInsensitive()
    {
        Assert.NotNull(BoostMapping.FindBoost("ability"));
        Assert.NotNull(BoostMapping.FindBoost("ABILITY"));
    }

    [Fact]
    public void FindBoost_Unknown_ReturnsNull()
    {
        Assert.Null(BoostMapping.FindBoost("ThisDoesNotExist"));
    }

    [Fact]
    public void FindFunctor_KnownName_Returns()
    {
        Assert.NotNull(BoostMapping.FindFunctor("DealDamage"));
        Assert.NotNull(BoostMapping.FindFunctor("ApplyStatus"));
    }

    [Fact]
    public void EmptyArgs_ReturnsEmptyArray()
    {
        var r = BoostMapping.ParseBoostCall("Invulnerable()");
        Assert.Empty(r!.Value.args);
    }

    [Fact]
    public void TrailingComma_DroppedSilently()
    {
        // Vanilla sometimes has trailing commas; parser keeps only non-empty args
        var r = BoostMapping.ParseBoostCall("WeaponDamage(-2,)");
        Assert.Single(r!.Value.args);
        Assert.Equal("-2", r.Value.args[0]);
    }
}
