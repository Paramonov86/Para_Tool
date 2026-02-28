using Xunit;
using ParaTool.Core.Services;

namespace ParaTool.Tests;

public class PoolDetectionTests
{
    [Theory]
    [InlineData("Armor", "Breast", "None", "No", null, "Clothes")]
    [InlineData("Armor", "Breast", "Cloth", "No", null, "Clothes")]
    [InlineData("Armor", "Breast", "Leather", "No", null, "Armor")]
    [InlineData("Armor", "Breast", "Plate", "No", null, "Armor")]
    [InlineData("Armor", "Helmet", null, "No", null, "Hats")]
    [InlineData("Armor", "Cloak", null, "No", null, "Cloaks")]
    [InlineData("Armor", "Gloves", null, "No", null, "Gloves")]
    [InlineData("Armor", "Boots", null, "No", null, "Boots")]
    [InlineData("Armor", "Amulet", null, "No", null, "Amulets")]
    [InlineData("Armor", "Ring", null, "No", null, "Rings")]
    [InlineData("Armor", "Breast", null, "Yes", null, "Shields")]
    public void DetectPool_Armor_ReturnsCorrectPool(
        string statType, string? slot, string? armorType, string? shield, string? weaponProps, string? expected)
    {
        Assert.Equal(expected, ModScanner.DetectPool(statType, slot, armorType, shield, weaponProps));
    }

    [Theory]
    [InlineData("Weapon", "Melee Main Weapon", null, "No", "Light;Melee", "Weapons_1H")]
    [InlineData("Weapon", "Melee Offhand Weapon", null, "No", null, "Weapons_1H")]
    [InlineData("Weapon", "Ranged Main Weapon", null, "No", null, "Weapons_2H")]
    [InlineData("Weapon", "Ranged Offhand Weapon", null, "No", null, "Weapons_1H")]
    public void DetectPool_Weapon_ReturnsCorrectPool(
        string statType, string? slot, string? armorType, string? shield, string? weaponProps, string? expected)
    {
        Assert.Equal(expected, ModScanner.DetectPool(statType, slot, armorType, shield, weaponProps));
    }

    [Theory]
    [InlineData("Armor", "MusicalInstrument", null, "No", null)]
    [InlineData("Armor", "Underwear", null, "No", null)]
    [InlineData("Armor", "VanityBody", null, "No", null)]
    public void DetectPool_SkippedSlots_ReturnsNull(
        string statType, string? slot, string? armorType, string? shield, string? weaponProps)
    {
        Assert.Null(ModScanner.DetectPool(statType, slot, armorType, shield, weaponProps));
    }
}
