using Xunit;
using ParaTool.Core.Schema;

namespace ParaTool.Tests;

/// <summary>
/// Tests for BoostMapping.FormatBoostsForPreview — guards against regressions
/// in the human-readable preview of common boost forms.
/// </summary>
public class BoostPreviewTests
{
    // Simple translator: identity for enum.* lookups, "en" language.
    private static string Translate(string key) =>
        key == "_lang" ? "en" : key.StartsWith("enum.") ? key[5..] : key;

    private static string Preview(string boost) =>
        BoostMapping.FormatBoostsForPreview(boost, Translate);

    // ── CriticalHit: all 6 meaningful variants ──────────────────

    [Fact]
    public void CriticalHit_Immunity()
    {
        Assert.Contains("can't land Critical Hits",
            Preview("CriticalHit(AttackTarget,Success,Never)"));
    }

    [Fact]
    public void CriticalHit_TargetAlwaysCrit()
    {
        Assert.Contains("All attacks against the wearer are critical",
            Preview("CriticalHit(AttackTarget,Success,ForcedAlways)"));
    }

    [Fact]
    public void CriticalHit_AutoCrit()
    {
        Assert.Contains("Guaranteed critical hits",
            Preview("CriticalHit(AttackRoll,Success,Always)"));
    }

    [Fact]
    public void CriticalHit_NoCritHit()
    {
        Assert.Contains("cannot score critical hits",
            Preview("CriticalHit(AttackRoll,Success,Never)"));
    }

    [Fact]
    public void CriticalHit_NoCritMiss()
    {
        Assert.Contains("Protects from critical misses",
            Preview("CriticalHit(AttackRoll,Failure,Never)"));
    }

    [Fact]
    public void CriticalHit_AlwaysCritFail()
    {
        Assert.Contains("always critically fail",
            Preview("CriticalHit(AttackRoll,Failure,ForcedAlways)"));
    }

    // ── Advantage / Disadvantage ────────────────────────────────

    [Fact]
    public void Advantage_AttackRoll()
    {
        Assert.Contains("Advantage on Attack Rolls", Preview("Advantage(AttackRoll)"));
    }

    [Fact]
    public void Advantage_AllSavingThrows()
    {
        Assert.Contains("Advantage on all Saving Throws",
            Preview("Advantage(AllSavingThrows)"));
    }

    [Fact]
    public void Advantage_SavingThrow_Ability()
    {
        Assert.Equal("Advantage on Dexterity Saving Throws.",
            Preview("Advantage(SavingThrow,Dexterity)"));
    }

    [Fact]
    public void Disadvantage_Concentration()
    {
        Assert.Contains("Disadvantage on Concentration",
            Preview("Disadvantage(Concentration)"));
    }

    [Fact]
    public void Advantage_Skill()
    {
        Assert.Equal("Advantage on Stealth Checks.", Preview("Advantage(Skill,Stealth)"));
    }

    // ── RollBonus ────────────────────────────────────────────────

    [Fact]
    public void RollBonus_Attack()
    {
        Assert.Equal("+1 Attack Rolls.", Preview("RollBonus(Attack,1)"));
    }

    [Fact]
    public void RollBonus_SavingThrow_Generic()
    {
        Assert.Equal("+2 Saving Throws.", Preview("RollBonus(SavingThrow,2)"));
    }

    [Fact]
    public void RollBonus_SavingThrow_Ability()
    {
        Assert.Equal("+1 Charisma Saving Throws.",
            Preview("RollBonus(SavingThrow,1,Charisma)"));
    }

    [Fact]
    public void RollBonus_SavingThrow_Negative()
    {
        Assert.Equal("-1 Wisdom Saving Throws.",
            Preview("RollBonus(SavingThrow,-1,Wisdom)"));
    }

    [Fact]
    public void RollBonus_DeathSavingThrow()
    {
        Assert.Equal("+2 Death Saving Throws.", Preview("RollBonus(DeathSavingThrow,2)"));
    }

    // ── Skill / Ability / Proficiency ───────────────────────────

    [Fact]
    public void Skill_Bonus()
    {
        Assert.Equal("+1 to Stealth Checks.", Preview("Skill(Stealth,1)"));
    }

    [Fact]
    public void Ability_Bonus()
    {
        Assert.Equal("Strength +2", Preview("Ability(Strength,2)"));
    }

    [Fact]
    public void Ability_Negative()
    {
        Assert.Equal("Charisma -1", Preview("Ability(Charisma,-1)"));
    }

    [Fact]
    public void Proficiency_Group()
    {
        Assert.Contains("Proficiency with Rapiers", Preview("Proficiency(Rapiers)"));
    }

    // ── WeaponDamage / WeaponProperty ───────────────────────────

    [Fact]
    public void WeaponDamage_Typed()
    {
        Assert.Equal("1d4 Fire damage on weapon attacks.",
            Preview("WeaponDamage(1d4,Fire)"));
    }

    [Fact]
    public void WeaponDamage_Untyped()
    {
        Assert.Equal("+1 Weapon Damage.", Preview("WeaponDamage(1)"));
    }

    [Fact]
    public void WeaponProperty_Magical()
    {
        Assert.Contains("Counts as Magical", Preview("WeaponProperty(Magical)"));
    }

    // ── Numeric single-arg boosts ───────────────────────────────

    [Fact]
    public void Initiative_Positive()
    {
        Assert.Equal("+3 Initiative.", Preview("Initiative(3)"));
    }

    [Fact]
    public void Initiative_Negative()
    {
        Assert.Equal("-1 Initiative.", Preview("Initiative(-1)"));
    }

    [Fact]
    public void SpellSaveDC_Bonus()
    {
        Assert.Equal("+2 Spell Save DC.", Preview("SpellSaveDC(2)"));
    }

    [Fact]
    public void IncreaseMaxHP_Formula()
    {
        Assert.Contains("-1d4", Preview("IncreaseMaxHP(-1d4)"));
    }

    // ── Existing coverage (regression guard) ────────────────────

    [Fact]
    public void AC_Existing()
    {
        Assert.Equal("Armour Class +1", Preview("AC(1)"));
    }

    [Fact]
    public void Resistance_Immune()
    {
        Assert.Contains("Immunity to Fire", Preview("Resistance(Fire,Immune)"));
    }

    [Fact]
    public void MultipleBoosts_JoinedByNewline()
    {
        var result = Preview("AC(1);Initiative(2)");
        Assert.Contains("Armour Class", result);
        Assert.Contains("Initiative", result);
        Assert.Contains("\n", result);
    }

    [Fact]
    public void Empty_Returns_Empty()
    {
        Assert.Equal("", Preview(""));
        Assert.Equal("", Preview("   "));
    }

    [Fact]
    public void UnknownBoost_FallsBackToChipStyle()
    {
        var result = Preview("SomeUnknownBoost(42)");
        // Should not be empty — fallback to raw or chip rendering
        Assert.NotEmpty(result);
    }

    // ── Reroll / Flags / Single-enum boosts ────────────────────

    [Fact]
    public void Reroll_Conditional()
    {
        Assert.Equal("Reroll Attack rolls of 10 or below.", Preview("Reroll(Attack,10,false)"));
    }

    [Fact]
    public void Reroll_Always()
    {
        Assert.Equal("Always reroll Damage rolls of 2 or below.",
            Preview("Reroll(Damage,2,true)"));
    }

    [Fact]
    public void IgnoreResistance_FireImmune()
    {
        Assert.Equal("Ignore Fire Resistance.", Preview("IgnoreResistance(Fire,Immune)"));
    }

    [Fact]
    public void Savant_School()
    {
        Assert.Contains("Savant of Evocation", Preview("Savant(Evocation)"));
    }

    [Fact]
    public void StatusImmunity_ById()
    {
        Assert.Contains("Immune to POISONED", Preview("StatusImmunity(POISONED)"));
    }

    [Fact]
    public void Invulnerable_Zero_Args()
    {
        Assert.Equal("Invulnerable.", Preview("Invulnerable()"));
    }

    [Fact]
    public void CannotBeDisarmed_Zero_Args()
    {
        Assert.Equal("Cannot be disarmed.", Preview("CannotBeDisarmed()"));
    }

    [Fact]
    public void BlockSpellCast_Zero_Args()
    {
        Assert.Equal("Cannot cast spells.", Preview("BlockSpellCast()"));
    }
}
