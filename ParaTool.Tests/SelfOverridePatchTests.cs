using ParaTool.Core.Parsing;
using ParaTool.Core.Patching;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// Edited original spells reach the patched pak as self-`using` overrides: edited in place when
/// the pak declares the spell itself, appended when the declaration lives elsewhere.
/// </summary>
public class SelfOverridePatchTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "pt_selfoverride_" + Guid.NewGuid().ToString("N"));

    public SelfOverridePatchTests() => Directory.CreateDirectory(_dir);
    public void Dispose() { try { Directory.Delete(_dir, true); } catch { } }

    private const string Compiled =
        "new entry \"TEST_Ring\"\ntype \"Armor\"\nusing \"ARM_Ring_A\"\ndata \"Boosts\" \"UnlockSpell(Projectile_Ring32)\"\n\n" +
        "new entry \"TEST_Ring_Spell_1\"\ntype \"SpellData\"\ndata \"SpellType\" \"Target\"\nusing \"Target_VampiricTouch\"\ndata \"Cooldown\" \"OncePerShortRest\"\n\n" +
        "new entry \"Projectile_Ring32\"\ntype \"SpellData\"\ndata \"SpellType\" \"Projectile\"\nusing \"Projectile_Ring32\"\ndata \"SpellSuccess\" \"DealDamage(4d12,Fire,Magical)\"\n\n" +
        "new entry \"Shout_Bless\"\ntype \"SpellData\"\ndata \"SpellType\" \"Shout\"\nusing \"Shout_Bless\"\ndata \"Cooldown\" \"OncePerTurn\"\n\n";

    [Fact]
    public void Split_KeepsItemsAndCopies_TakesSelfOverrides()
    {
        var self = new List<StatsEntry>();
        var rest = AmpPatcher.SplitSelfOverrides(Compiled, self);

        Assert.Equal(["Projectile_Ring32", "Shout_Bless"], self.Select(e => e.Name));
        var kept = StatsParser.Parse(rest).Select(e => e.Name).ToList();
        Assert.Equal(["TEST_Ring", "TEST_Ring_Spell_1"], kept);
    }

    [Fact]
    public void Apply_EditsDeclarationInPak_AppendsTheRest()
    {
        var jewelry = Path.Combine(_dir, "AncientJewelry.txt");
        var last = Path.Combine(_dir, "ZZ_Last.txt");
        File.WriteAllText(jewelry,
            "new entry \"Projectile_Ring32\"\ntype \"SpellData\"\ndata \"SpellType\" \"Projectile\"\n" +
            "data \"SpellSuccess\" \"DealDamage(3d12,Fire,Magical)\"\ndata \"ProjectileCount\" \"1\"\n\n");
        File.WriteAllText(last, "new entry \"Other\"\ntype \"PassiveData\"\n\n");

        var self = new List<StatsEntry>();
        AmpPatcher.SplitSelfOverrides(Compiled, self);
        AmpPatcher.ApplySelfOverrides([jewelry, last], self);

        var ring = StatsParser.Parse(File.ReadAllText(jewelry)).Single(e => e.Name == "Projectile_Ring32");
        Assert.Null(ring.Using);
        Assert.Equal("DealDamage(4d12,Fire,Magical)", ring.Data["SpellSuccess"]);
        Assert.Equal("1", ring.Data["ProjectileCount"]);
        Assert.DoesNotContain("Projectile_Ring32", File.ReadAllText(last));

        var bless = StatsParser.Parse(File.ReadAllText(last)).Single(e => e.Name == "Shout_Bless");
        Assert.Equal("Shout_Bless", bless.Using);
        Assert.Equal("Shout", bless.Data["SpellType"]);
        Assert.Equal("OncePerTurn", bless.Data["Cooldown"]);
    }
}
