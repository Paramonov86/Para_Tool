using ParaTool.Core.Schema;
using Xunit;

namespace ParaTool.Tests;

public class EngineSignaturesTests
{
    [Fact]
    public void DealDamage_HasEveryArgumentTheGameTakes()
    {
        var args = EngineSignatures.Args("DealDamage", functor: true);
        Assert.Equal(["Damage", "DamageType", "Magical", "Nonlethal", "CoinMultiplier", "Tooltip",
            "IgnoreDamageBonus", "IgnoreOnDamage", "ConsumeCoin", "IgnoreImmune"], args);
    }

    [Fact]
    public void EveryBlock_HasNoMoreArgumentsThanTheGame()
    {
        // A block longer than the game's signature names an argument BG3 does not read.
        foreach (var (defs, functor) in new[] { (BoostMapping.Boosts, false), (BoostMapping.Functors, true) })
            foreach (var def in defs)
            {
                var engine = EngineSignatures.Args(def.FuncName, functor);
                if (engine.Length == 0) continue; // added by hand (Knockback, GreatWeaponMaster)
                Assert.True(def.Params.Length <= engine.Length,
                    $"{def.FuncName}: {def.Params.Length} params, the game takes {engine.Length}");
            }
    }
}
