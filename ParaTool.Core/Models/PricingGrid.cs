namespace ParaTool.Core.Models;

public static class PricingGrid
{
    public enum SlotCategory
    {
        Ring,
        Accessory, // Gloves, Boots, Cloaks
        Armor,     // Armor + Clothes
        Amulet,
        Weapon,
        Shield,
        Hat
    }

    private static readonly Dictionary<(SlotCategory, string), int> Prices = new()
    {
        // Uncommon
        { (SlotCategory.Ring, "Uncommon"), 150 },
        { (SlotCategory.Accessory, "Uncommon"), 150 },
        { (SlotCategory.Armor, "Uncommon"), 200 },
        { (SlotCategory.Amulet, "Uncommon"), 200 },
        { (SlotCategory.Weapon, "Uncommon"), 250 },
        { (SlotCategory.Shield, "Uncommon"), 250 },
        { (SlotCategory.Hat, "Uncommon"), 300 },

        // Rare
        { (SlotCategory.Ring, "Rare"), 400 },
        { (SlotCategory.Accessory, "Rare"), 400 },
        { (SlotCategory.Armor, "Rare"), 500 },
        { (SlotCategory.Amulet, "Rare"), 500 },
        { (SlotCategory.Weapon, "Rare"), 550 },
        { (SlotCategory.Shield, "Rare"), 550 },
        { (SlotCategory.Hat, "Rare"), 600 },

        // VeryRare
        { (SlotCategory.Ring, "VeryRare"), 800 },
        { (SlotCategory.Accessory, "VeryRare"), 800 },
        { (SlotCategory.Armor, "VeryRare"), 1000 },
        { (SlotCategory.Amulet, "VeryRare"), 1000 },
        { (SlotCategory.Weapon, "VeryRare"), 1100 },
        { (SlotCategory.Shield, "VeryRare"), 1100 },
        { (SlotCategory.Hat, "VeryRare"), 1200 },

        // Legendary
        { (SlotCategory.Ring, "Legendary"), 2500 },
        { (SlotCategory.Accessory, "Legendary"), 2500 },
        { (SlotCategory.Armor, "Legendary"), 3000 },
        { (SlotCategory.Amulet, "Legendary"), 3000 },
        { (SlotCategory.Shield, "Legendary"), 3100 },
        { (SlotCategory.Weapon, "Legendary"), 3300 },
        { (SlotCategory.Hat, "Legendary"), 3500 },
    };

    public static int GetPrice(SlotCategory category, string rarity)
    {
        return Prices.TryGetValue((category, rarity), out var price) ? price : 200;
    }

    public static SlotCategory GetSlotCategory(string pool)
    {
        return pool switch
        {
            "Rings" => SlotCategory.Ring,
            "Gloves" or "Boots" or "Cloaks" => SlotCategory.Accessory,
            "Armor" or "Clothes" => SlotCategory.Armor,
            "Amulets" => SlotCategory.Amulet,
            "Weapons" or "Weapons_1H" or "Weapons_2H" => SlotCategory.Weapon,
            "Shields" => SlotCategory.Shield,
            "Hats" => SlotCategory.Hat,
            _ => SlotCategory.Armor
        };
    }
}
