using Xunit;
using ParaTool.Core.Models;

namespace ParaTool.Tests;

public class PricingGridTests
{
    [Theory]
    [InlineData(PricingGrid.SlotCategory.Ring, "Uncommon", 150)]
    [InlineData(PricingGrid.SlotCategory.Ring, "Rare", 400)]
    [InlineData(PricingGrid.SlotCategory.Ring, "VeryRare", 800)]
    [InlineData(PricingGrid.SlotCategory.Ring, "Legendary", 2500)]
    [InlineData(PricingGrid.SlotCategory.Hat, "Legendary", 3500)]
    [InlineData(PricingGrid.SlotCategory.Weapon, "Rare", 550)]
    [InlineData(PricingGrid.SlotCategory.Armor, "VeryRare", 1000)]
    [InlineData(PricingGrid.SlotCategory.Shield, "Legendary", 3100)]
    public void GetPrice_ReturnsCorrectPrice(PricingGrid.SlotCategory category, string rarity, int expected)
    {
        Assert.Equal(expected, PricingGrid.GetPrice(category, rarity));
    }

    [Theory]
    [InlineData("Rings", PricingGrid.SlotCategory.Ring)]
    [InlineData("Gloves", PricingGrid.SlotCategory.Accessory)]
    [InlineData("Boots", PricingGrid.SlotCategory.Accessory)]
    [InlineData("Cloaks", PricingGrid.SlotCategory.Accessory)]
    [InlineData("Armor", PricingGrid.SlotCategory.Armor)]
    [InlineData("Clothes", PricingGrid.SlotCategory.Armor)]
    [InlineData("Amulets", PricingGrid.SlotCategory.Amulet)]
    [InlineData("Weapons", PricingGrid.SlotCategory.Weapon)]
    [InlineData("Shields", PricingGrid.SlotCategory.Shield)]
    [InlineData("Hats", PricingGrid.SlotCategory.Hat)]
    public void GetSlotCategory_MapsCorrectly(string pool, PricingGrid.SlotCategory expected)
    {
        Assert.Equal(expected, PricingGrid.GetSlotCategory(pool));
    }
}
