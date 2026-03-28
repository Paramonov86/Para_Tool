using Xunit;
using Xunit.Abstractions;
using ParaTool.Core.Schema;

namespace ParaTool.Tests;

public class StatsSchemaTests
{
    private readonly ITestOutputHelper _o;
    public StatsSchemaTests(ITestOutputHelper o) => _o = o;

    [Fact]
    public void Schema_LoadsAllTypes()
    {
        var schema = StatsSchema.Instance;

        _o.WriteLine($"Types: {schema.Types.Count}");
        foreach (var t in schema.Types)
            _o.WriteLine($"  {t.Key}: {t.Value.Fields.Count} fields");

        Assert.True(schema.Types.Count >= 6, "Expected at least 6 types (Armor, Weapon, PassiveData, StatusData, SpellData, InterruptData)");
        Assert.Contains("Armor", schema.Types.Keys);
        Assert.Contains("Weapon", schema.Types.Keys);
        Assert.Contains("PassiveData", schema.Types.Keys);
        Assert.Contains("StatusData", schema.Types.Keys);
        Assert.Contains("SpellData", schema.Types.Keys);
    }

    [Fact]
    public void Schema_ArmorHasExpectedFields()
    {
        var armor = StatsSchema.Instance.GetType("Armor");
        Assert.NotNull(armor);

        Assert.NotNull(armor!.GetField("Rarity"));
        Assert.NotNull(armor.GetField("ArmorClass"));
        Assert.NotNull(armor.GetField("PassivesOnEquip"));
        Assert.NotNull(armor.GetField("StatusOnEquip"));
        Assert.NotNull(armor.GetField("Boosts"));
        Assert.NotNull(armor.GetField("RootTemplate"));
        Assert.NotNull(armor.GetField("ValueOverride"));

        _o.WriteLine($"Armor fields: {armor.Fields.Count}");
        Assert.True(armor.Fields.Count >= 40);
    }

    [Fact]
    public void Schema_FieldTypes()
    {
        var armor = StatsSchema.Instance.GetType("Armor")!;

        var armorClass = armor.GetField("ArmorClass")!;
        Assert.True(armorClass.IsNumeric);
        Assert.False(armorClass.IsEnum);

        var rarity = armor.GetField("Rarity")!;
        Assert.True(rarity.IsEnum);
        Assert.False(rarity.IsNumeric);

        var passives = armor.GetField("PassivesOnEquip")!;
        Assert.True(passives.IsFreeText);
    }

    [Fact]
    public void Schema_LoadsValueLists()
    {
        var schema = StatsSchema.Instance;

        _o.WriteLine($"ValueLists: {schema.ValueLists.Count}");
        foreach (var vl in schema.ValueLists.Take(10))
            _o.WriteLine($"  {vl.Key}: {vl.Value.Values.Count} values");

        Assert.True(schema.ValueLists.Count >= 10);
    }

    [Fact]
    public void Schema_RarityValues()
    {
        var rarities = StatsSchema.Instance.GetAllowedValues("Armor", "Rarity");
        Assert.NotNull(rarities);
        _o.WriteLine($"Rarity values: {string.Join(", ", rarities!)}");

        Assert.Contains("Uncommon", rarities);
        Assert.Contains("Legendary", rarities);
    }

    [Fact]
    public void Schema_LoadsConditions()
    {
        var schema = StatsSchema.Instance;

        _o.WriteLine($"Conditions: {schema.Conditions.Count}");
        var bySource = schema.Conditions.GroupBy(c => c.Source).ToList();
        foreach (var g in bySource)
            _o.WriteLine($"  {g.Key}: {g.Count()} functions");

        Assert.True(schema.Conditions.Count >= 20, "Expected at least 20 condition functions");

        // Check specific known functions
        var maneuver = schema.Conditions.FirstOrDefault(c => c.Name == "ManeuverSaveDC");
        Assert.NotNull(maneuver);
        _o.WriteLine($"  ManeuverSaveDC sig: {maneuver!.Signature}");
    }

    [Fact]
    public void Schema_SearchConditions()
    {
        var results = StatsSchema.Instance.SearchConditions("Save");
        _o.WriteLine($"Search 'Save': {results.Count} results");
        foreach (var r in results.Take(5))
            _o.WriteLine($"  {r.Signature} ({r.Source})");

        Assert.True(results.Count >= 1);
    }

    [Fact]
    public void Schema_SpellDataHasManyFields()
    {
        var spell = StatsSchema.Instance.GetType("SpellData");
        Assert.NotNull(spell);
        _o.WriteLine($"SpellData fields: {spell!.Fields.Count}");
        Assert.True(spell.Fields.Count >= 150, "SpellData should have 150+ fields");
    }

    [Fact]
    public void Schema_StatusDataHasExpectedFields()
    {
        var status = StatsSchema.Instance.GetType("StatusData");
        Assert.NotNull(status);

        Assert.NotNull(status!.GetField("StatusType"));
        Assert.NotNull(status.GetField("StackType"));
        Assert.NotNull(status.GetField("TickFunctors"));
        Assert.NotNull(status.GetField("StatusPropertyFlags"));
        Assert.NotNull(status.GetField("StatusGroups"));

        _o.WriteLine($"StatusData fields: {status.Fields.Count}");
        Assert.True(status.Fields.Count >= 100);
    }
}
