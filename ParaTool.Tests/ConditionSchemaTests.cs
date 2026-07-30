using Xunit;
using ParaTool.Core.Schema;

namespace ParaTool.Tests;

/// <summary>
/// Tests for the condition catalog built from the embedded .khn files. Acts as a canary
/// that the bundled amp_conditions.khn stays in sync with the AMP release (the file is a
/// schema source for the chip menu — out-of-date = missing conditions for users).
/// </summary>
public class ConditionSchemaTests
{
    [Fact]
    public void Schema_Loads_WithoutError()
    {
        var schema = ConditionSchema.Instance;
        Assert.NotEmpty(schema.Functions);
        // Core tag condition must be present.
        Assert.True(schema.ByName.ContainsKey("Tagged"));
    }

    [Theory]
    // Conditions introduced in the June AMP release (Ilmater Expansion + helpers).
    [InlineData("IsBladeWarded")]
    [InlineData("IsAMPIlmaterStigmaApplied")]
    [InlineData("HasNegativeStatus")]
    public void Schema_Includes_ReleaseAmpConditions(string name)
    {
        Assert.True(ConditionSchema.Instance.ByName.ContainsKey(name),
            $"Expected AMP release condition '{name}' — bundled amp_conditions.khn may be stale.");
    }

    // ── Argument dialect ───────────────────────────────────────
    // Shipped BG3/AMP stats write SavingThrow(Ability.Constitution,13) and
    // SkillCheck(Skill.Stealth,15). A quoted 'Constitution' is a string to the engine,
    // so the condition evaluates to nothing and the surrounding IF never fires.

    [Fact]
    public void SavingThrow_DcIsNumeric_NotAString()
    {
        var def = ConditionSchema.Instance.ByName["SavingThrow"];
        Assert.Equal("int", def.Params[1].Type);
    }

    [Fact]
    public void SavingThrow_AdvantageFlags_AreOptional()
    {
        var def = ConditionSchema.Instance.ByName["SavingThrow"];
        Assert.True(def.Params[2].IsOptional, "advantage must not be forced into every chip");
        Assert.True(def.Params[3].IsOptional, "disadvantage must not be forced into every chip");
        Assert.False(def.Params[0].IsOptional);
        Assert.False(def.Params[1].IsOptional);
    }

    [Theory]
    [InlineData("SavingThrow", 0, "Constitution", "Ability.Constitution")]
    [InlineData("SkillCheck", 0, "Stealth", "Skill.Stealth")]
    [InlineData("HasDamageDoneForType", 0, "Fire", "DamageType.Fire")]
    public void FormatArg_DottedEnums_AreUnquoted(string func, int idx, string value, string expected)
    {
        var def = ConditionSchema.Instance.ByName[func];
        Assert.Equal(expected, ConditionSchema.FormatArg(def.Params[idx], value));
    }

    [Fact]
    public void FormatArg_PlainStringEnums_StayQuoted()
    {
        var def = ConditionSchema.Instance.ByName["InSurface"];
        Assert.Equal("'SurfaceFire'", ConditionSchema.FormatArg(def.Params[0], "SurfaceFire"));
    }

    [Fact]
    public void NormalizeConditionEnums_RepairsLegacySavingThrow()
    {
        var repaired = ConditionSchema.NormalizeConditionEnums(
            "IF(not SavingThrow('Constitution','13',true,true)):ApplyStatus(POISONED,100,1)");
        Assert.Equal("IF(not SavingThrow(Ability.Constitution,13,true,true)):ApplyStatus(POISONED,100,1)", repaired);
    }

    [Theory]
    // Already correct, or deliberately quoted — must come back byte-for-byte.
    [InlineData("SavingThrow(Ability.Constitution,13)")]
    [InlineData("HasStatus('POISONED',context.Target)")]
    [InlineData("InSurface('SurfaceFire')")]
    [InlineData("HasStatusGroup(context.StatusId, 'SG_Rage')")]
    [InlineData("not SavingThrow(Ability.Constitution,ManeuverSaveDC()+2)")]
    [InlineData("Tagged('SORCERER')")]
    public void NormalizeConditionEnums_LeavesValidInputAlone(string input)
    {
        Assert.Equal(input, ConditionSchema.NormalizeConditionEnums(input));
    }
}
