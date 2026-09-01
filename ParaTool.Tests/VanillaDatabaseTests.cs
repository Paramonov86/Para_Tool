using Xunit;
using ParaTool.Core.Services;

namespace ParaTool.Tests;

public class VanillaDatabaseTests
{
    [Fact]
    public void Load_PopulatesResolver()
    {
        var db = new VanillaDatabase();
        db.Load();

        // Should have loaded entries from all 3 files
        Assert.True(db.Resolver.AllEntries.Count > 100);
    }

    [Fact]
    public void Load_CanResolveKnownEntry()
    {
        var db = new VanillaDatabase();
        db.Load();

        // _Body is the base armor entry in Armor.txt
        var slot = db.Resolver.Resolve("_Body", "Slot");
        Assert.Equal("Breast", slot);
    }

    [Fact]
    public void Load_CanResolveInheritedEntry()
    {
        var db = new VanillaDatabase();
        db.Load();

        // ARM_Padded_Body uses _Body, which has Slot=Breast
        var slot = db.Resolver.Resolve("ARM_Padded_Body", "Slot");
        Assert.Equal("Breast", slot);

        // ARM_Padded_Body has its own ArmorType
        var armorType = db.Resolver.Resolve("ARM_Padded_Body", "ArmorType");
        Assert.Equal("Padded", armorType);
    }

    [Fact]
    public void Load_PassiveHasStatsFunctors()
    {
        var db = new VanillaDatabase();
        db.Load();

        var fields = db.Resolver.ResolveAll("MAG_ChargedLightning_Charge_OnDamage_Passive");
        Assert.True(fields.Count > 0, "Passive should exist");
        Assert.True(fields.ContainsKey("StatsFunctors"), "Should have StatsFunctors");
        Assert.True(fields.ContainsKey("StatsFunctorContext"), "Should have StatsFunctorContext");
        Assert.Contains("ApplyStatus", fields["StatsFunctors"]);
        Assert.Equal("OnDamage", fields["StatsFunctorContext"]);
    }

    [Fact]
    public void Load_WeaponEntriesPresent()
    {
        var db = new VanillaDatabase();
        db.Load();

        var entry = db.Resolver.Get("WPN_Battleaxe");
        Assert.NotNull(entry);
        Assert.Equal("Weapon", entry.Type);
    }

    /// <summary>
    /// Bases whose RootTemplate the embedded dump was missing: the Constructor left
    /// ParentTemplateUuid empty, and the artifact compiled to a template with nothing to inherit
    /// from — present in the pak, unspawnable in game, and reported as a clean patch.
    /// </summary>
    [Theory]
    [InlineData("MAG_SHA_SeluneBlessing_Spear", "2eeabe97-8f29-4f4f-827e-6cfcd8fd1779")]
    [InlineData("MAG_SHA_SharBlessing_Spear", null)]
    [InlineData("UNI_Cazador_RitualDagger", null)]
    [InlineData("MAG_Gortash_Gloves", null)]
    [InlineData("WPN_Mace_Deva", null)]
    [InlineData("WPN_Djinni_Scimitar_PlanarAlly", null)]
    [InlineData("MAG_HAV_Sylvan_Scimitar", null)]
    [InlineData("MAG_TWN_Brewery_Greatclub", null)]
    [InlineData("MAG_TWN_Taxblade_Morningstar", null)]
    public void Load_ResolvesRootTemplate_ForAmpThinOverriddenVanillaItems(string statId, string? expectedUuid)
    {
        var db = new VanillaDatabase();
        db.Load();

        var rootTemplate = db.Resolver.Resolve(statId, "RootTemplate");

        Assert.False(string.IsNullOrEmpty(rootTemplate), $"{statId} must carry a RootTemplate");
        if (expectedUuid != null)
            Assert.Equal(expectedUuid, rootTemplate);
    }

    [Fact]
    public void Load_ResolvesSlotAndWeaponFields_ForARestoredEntry()
    {
        // AMP restates this spear with a self-referencing `using`, so everything the Constructor
        // needs has to come from the vanilla entry behind it.
        var db = new VanillaDatabase();
        db.Load();

        var fields = db.Resolver.ResolveAll("MAG_SHA_SeluneBlessing_Spear");

        Assert.Equal("Melee Main Weapon", fields["Slot"]);
        Assert.Equal("Piercing", fields["Damage Type"]);
        Assert.Equal("Spears;SimpleWeapons", fields["Proficiency Group"]);
    }
}
