using Xunit;
using ParaTool.Core.Artifacts;

namespace ParaTool.Tests;

/// <summary>
/// Tests for ArtifactCompiler.Compile — locks in the shape of the generated
/// stats text so changes to compile logic don't silently break patched items.
/// Uses minimal ArtifactDefinitions with no resolver.
/// </summary>
public class ArtifactCompilerTests
{
    private static ArtifactDefinition NewArmor(string statId = "TEST_Armor") => new()
    {
        StatId = statId,
        StatType = "Armor",
        UsingBase = "ARM_Gloves_Leather",
        Rarity = "Rare",
        LootPool = "Gloves",
        ValueOverride = 200,
        Unique = false,
        Weight = 0.5,
    };

    private static ArtifactDefinition NewWeapon(string statId = "TEST_Weapon") => new()
    {
        StatId = statId,
        StatType = "Weapon",
        UsingBase = "WPN_Longsword_1",
        Rarity = "VeryRare",
        LootPool = "Weapons",
        ValueOverride = 500,
        Unique = false,
        Weight = 3.0,
    };

    // ── Basic structure ─────────────────────────────────────

    [Fact]
    public void Compile_Armor_HasRequiredHeader()
    {
        var art = NewArmor();
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("new entry \"TEST_Armor\"", result.StatsText);
        Assert.Contains("type \"Armor\"", result.StatsText);
        Assert.Contains("using \"ARM_Gloves_Leather\"", result.StatsText);
        Assert.Contains("data \"Rarity\" \"Rare\"", result.StatsText);
    }

    [Fact]
    public void Compile_WritesRootTemplate_WhenNotOverride()
    {
        var art = NewArmor();
        art.TemplateUuid = "test-uuid-123";
        var result = ArtifactCompiler.Compile(art, isOverride: false);

        Assert.Contains("data \"RootTemplate\" \"test-uuid-123\"", result.StatsText);
    }

    [Fact]
    public void Compile_SkipsRootTemplate_WhenIsOverride()
    {
        var art = NewArmor();
        art.TemplateUuid = "test-uuid-123";
        var result = ArtifactCompiler.Compile(art, isOverride: true);

        Assert.DoesNotContain("RootTemplate", result.StatsText);
    }

    [Fact]
    public void Compile_TemplateUuid_Returned()
    {
        var art = NewArmor();
        art.TemplateUuid = "foo";
        art.ParentTemplateUuid = "bar";
        var result = ArtifactCompiler.Compile(art);

        Assert.Equal("foo", result.TemplateUuid);
        Assert.Equal("bar", result.ParentTemplateUuid);
    }

    // ── Unique flag ─────────────────────────────────────────

    [Fact]
    public void Unique_True_WritesOne()
    {
        var art = NewArmor();
        art.Unique = true;
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"Unique\" \"1\"", result.StatsText);
    }

    [Fact]
    public void Unique_False_WritesEmptyString()
    {
        var art = NewArmor();
        art.Unique = false;
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"Unique\" \"\"", result.StatsText);
    }

    // ── Weight ──────────────────────────────────────────────

    [Fact]
    public void Weight_Negative_NotWritten()
    {
        var art = NewArmor();
        art.Weight = -1;
        var result = ArtifactCompiler.Compile(art);

        Assert.DoesNotContain("\"Weight\"", result.StatsText);
    }

    [Fact]
    public void Weight_Decimal_UsesInvariantCulture()
    {
        var art = NewArmor();
        art.Weight = 0.55;
        var result = ArtifactCompiler.Compile(art);

        // Must use dot, not comma, regardless of system locale
        Assert.Contains("data \"Weight\" \"0.55\"", result.StatsText);
    }

    // ── Armor-specific ──────────────────────────────────────

    [Fact]
    public void Armor_SkipsNoneValues()
    {
        var art = NewArmor();
        art.ArmorClass = -1;
        art.ArmorType = "None";
        art.ProficiencyGroup = "None";
        var result = ArtifactCompiler.Compile(art);

        Assert.DoesNotContain("ArmorClass", result.StatsText);
        Assert.DoesNotContain("\"ArmorType\"", result.StatsText);
        Assert.DoesNotContain("Proficiency Group", result.StatsText);
    }

    [Fact]
    public void Armor_WritesValuesWhenSet()
    {
        var art = NewArmor();
        art.ArmorClass = 15;
        art.ArmorType = "Leather";
        art.ProficiencyGroup = "LightArmor";
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"ArmorClass\" \"15\"", result.StatsText);
        Assert.Contains("data \"ArmorType\" \"Leather\"", result.StatsText);
        Assert.Contains("data \"Proficiency Group\" \"LightArmor\"", result.StatsText);
    }

    // ── Weapon-specific ─────────────────────────────────────

    [Fact]
    public void Weapon_WritesDamageFields()
    {
        var art = NewWeapon();
        art.Damage = "1d8";
        art.DamageType = "Slashing";
        art.VersatileDamage = "1d10";
        art.WeaponProperties = "Versatile;Finesse";
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"Damage\" \"1d8\"", result.StatsText);
        Assert.Contains("data \"Damage Type\" \"Slashing\"", result.StatsText);
        Assert.Contains("data \"VersatileDamage\" \"1d10\"", result.StatsText);
        Assert.Contains("data \"Weapon Properties\" \"Versatile;Finesse\"", result.StatsText);
    }

    [Fact]
    public void Weapon_SkipsDamageTypeWhenNone()
    {
        var art = NewWeapon();
        art.DamageType = "None";
        var result = ArtifactCompiler.Compile(art);

        Assert.DoesNotContain("Damage Type", result.StatsText);
    }

    // ── Boosts / SpellsOnEquip merging ──────────────────────

    [Fact]
    public void Boosts_AlwaysWritten_EvenIfEmpty()
    {
        var art = NewArmor();
        art.Boosts = "";
        var result = ArtifactCompiler.Compile(art);

        // Must be written to block inheritance from base
        Assert.Contains("data \"Boosts\" \"\"", result.StatsText);
    }

    [Fact]
    public void Boosts_Merges_WithSpellsOnEquip()
    {
        var art = NewArmor();
        art.Boosts = "AC(2)";
        art.SpellsOnEquip = "Target_MagicMissile;Shout_Bless";
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains(
            "data \"Boosts\" \"AC(2);UnlockSpell(Target_MagicMissile);UnlockSpell(Shout_Bless)\"",
            result.StatsText);
    }

    [Fact]
    public void BoostsOnEquipMainHand_AlwaysWritten()
    {
        var art = NewWeapon();
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("BoostsOnEquipMainHand", result.StatsText);
        Assert.Contains("BoostsOnEquipOffHand", result.StatsText);
    }

    // ── PassivesOnEquip merging ────────────────────────────

    [Fact]
    public void PassivesOnEquip_MergesExplicitAndPassiveArray()
    {
        var art = NewArmor();
        art.PassivesOnEquip = "Vanilla_Passive_A";
        art.Passives = [
            new PassiveDefinition { Name = "TEST_Armor_Custom_1" }
        ];
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"PassivesOnEquip\"", result.StatsText);
        Assert.Contains("Vanilla_Passive_A", result.StatsText);
        Assert.Contains("TEST_Armor_Custom_1", result.StatsText);
    }

    [Fact]
    public void PassivesOnEquip_AlwaysWritten_EvenIfEmpty()
    {
        var art = NewArmor();
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"PassivesOnEquip\" \"\"", result.StatsText);
    }

    // ── StatusOnEquip ───────────────────────────────────────

    [Fact]
    public void StatusOnEquip_AlwaysWritten()
    {
        var art = NewArmor();
        art.StatusOnEquip = "BOOST_WET";
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"StatusOnEquip\" \"BOOST_WET\"", result.StatsText);
    }

    // ── Passive rename for custom passives ─────────────────

    [Fact]
    public void Passive_NameMatchingStatId_NotRenamed()
    {
        var art = NewArmor("ARM_X");
        art.Passives = [
            new PassiveDefinition { Name = "ARM_X_Custom", UsingBase = null }
        ];
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("new entry \"ARM_X_Custom\"", result.StatsText);
    }

    [Fact]
    public void Passive_CustomName_Renamed_And_UsingBaseSet()
    {
        var art = NewArmor("ARM_X");
        art.Passives = [
            new PassiveDefinition { Name = "MAG_Vanilla_Passive", UsingBase = null }
        ];
        var result = ArtifactCompiler.Compile(art);

        // Renamed to {StatId}_Passive_1 with using=original
        Assert.Contains("new entry \"ARM_X_Passive_1\"", result.StatsText);
        Assert.Contains("using \"MAG_Vanilla_Passive\"", result.StatsText);
    }

    // ── Pricing safety net ─────────────────────────────────

    [Fact]
    public void Price_RareWithLowValue_CorrectsToGrid()
    {
        var art = NewArmor();
        art.Rarity = "Rare";
        art.ValueOverride = 100; // too low for Rare
        var result = ArtifactCompiler.Compile(art);

        // Should not write the stale 100 — PricingGrid snaps up
        Assert.DoesNotContain("data \"ValueOverride\" \"100\"", result.StatsText);
    }

    [Fact]
    public void Price_ValidOverride_Respected()
    {
        var art = NewArmor();
        art.Rarity = "Uncommon";
        art.ValueOverride = 175;
        var result = ArtifactCompiler.Compile(art);

        Assert.Contains("data \"ValueOverride\" \"175\"", result.StatsText);
    }

    // ── Localization output ────────────────────────────────

    [Fact]
    public void Loca_EmptyTexts_Skipped()
    {
        var art = NewArmor();
        art.DisplayName = new() { ["en"] = "", ["ru"] = "" };
        art.DisplayNameHandle = "h123";
        var result = ArtifactCompiler.Compile(art);

        Assert.Empty(result.LocalizationEntries["en"]);
        Assert.Empty(result.LocalizationEntries["ru"]);
    }

    [Fact]
    public void Loca_NonEmptyText_WrittenForThatLanguage()
    {
        var art = NewArmor();
        art.DisplayName = new() { ["en"] = "My Gloves", ["ru"] = "" };
        art.DisplayNameHandle = "h123";
        var result = ArtifactCompiler.Compile(art);

        Assert.Single(result.LocalizationEntries["en"]);
        Assert.Equal("h123", result.LocalizationEntries["en"][0].handle);
        Assert.Empty(result.LocalizationEntries["ru"]);
    }

    // ── GenerateLocaXml helper ─────────────────────────────

    [Fact]
    public void GenerateLocaXml_WellFormed()
    {
        var entries = new List<(string, string)>
        {
            ("h1", "My Item"),
            ("h2", "Description here"),
        };
        var xml = ArtifactCompiler.GenerateLocaXml(entries);

        Assert.Contains("<?xml version=\"1.0\"", xml);
        Assert.Contains("<contentList>", xml);
        Assert.Contains("contentuid=\"h1\"", xml);
        Assert.Contains(">My Item<", xml);
        Assert.Contains("</contentList>", xml);
    }
}
