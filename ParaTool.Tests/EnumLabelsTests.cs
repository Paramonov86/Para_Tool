using Xunit;
using ParaTool.Core.Schema;

namespace ParaTool.Tests;

public class EnumLabelsTests
{
    [Fact]
    public void Ability_HasBothLanguages()
    {
        Assert.Equal("STR", EnumLabels.GetLabel("Strength", "en"));
        Assert.Equal("СИЛ", EnumLabels.GetLabel("Strength", "ru"));
    }

    [Fact]
    public void DamageType_HasBothLanguages()
    {
        Assert.Equal("Fire", EnumLabels.GetLabel("Fire", "en"));
        Assert.Equal("Огонь", EnumLabels.GetLabel("Fire", "ru"));
    }

    [Fact]
    public void UnknownValue_FallsBackToRaw()
    {
        Assert.Equal("NotARealEnum", EnumLabels.GetLabel("NotARealEnum", "en"));
        Assert.Equal("NotARealEnum", EnumLabels.GetLabel("NotARealEnum", "ru"));
    }

    [Fact]
    public void CaseInsensitive_Lookup()
    {
        Assert.Equal("STR", EnumLabels.GetLabel("strength", "en"));
        Assert.Equal("STR", EnumLabels.GetLabel("STRENGTH", "en"));
    }

    [Fact]
    public void GetDisplayLabels_MapsAllValues()
    {
        string[] abilities = ["Strength", "Dexterity", "Constitution"];
        var en = EnumLabels.GetDisplayLabels(abilities, "en");
        var ru = EnumLabels.GetDisplayLabels(abilities, "ru");

        Assert.Equal(3, en.Length);
        Assert.Equal(["STR", "DEX", "CON"], en);
        Assert.Equal(["СИЛ", "ЛОВ", "ТЕЛ"], ru);
    }

    [Fact]
    public void UnknownLang_TreatedAsEn()
    {
        // Non-"ru" codes should use the English path
        Assert.Equal("STR", EnumLabels.GetLabel("Strength", "de"));
        Assert.Equal("STR", EnumLabels.GetLabel("Strength", ""));
    }

    // For these "has label" data-consistency tests we check the Russian
    // label only — the English label is sometimes identical to the raw
    // enum value (e.g. "Fire" → "Fire") which is an intentional no-op
    // mapping, not a missing entry.

    [Fact]
    public void AllAbilities_HaveRussianLabels()
    {
        foreach (var a in BoostMapping.Abilities)
            Assert.NotEqual(a, EnumLabels.GetLabel(a, "ru"));
    }

    [Fact]
    public void AllDamageTypes_HaveRussianLabels()
    {
        foreach (var dt in BoostMapping.DamageTypes)
            Assert.NotEqual(dt, EnumLabels.GetLabel(dt, "ru"));
    }

    [Fact]
    public void AllSkills_HaveRussianLabels()
    {
        foreach (var sk in BoostMapping.SkillType)
            Assert.NotEqual(sk, EnumLabels.GetLabel(sk, "ru"));
    }
}
