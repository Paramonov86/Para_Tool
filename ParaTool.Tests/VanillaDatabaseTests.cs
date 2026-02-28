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
    public void Load_WeaponEntriesPresent()
    {
        var db = new VanillaDatabase();
        db.Load();

        var entry = db.Resolver.Get("WPN_Battleaxe");
        Assert.NotNull(entry);
        Assert.Equal("Weapon", entry.Type);
    }
}
