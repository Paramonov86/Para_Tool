using System.Reflection;
using System.Text.RegularExpressions;
using ParaTool.Core.Schema;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// The hardcoded value lists the chip editors offer hold every value the game defines (ValueLists.txt)
/// and every one its own stats use — a value missing from a drum can't be picked, and one a stat
/// already has used to read as the list's first item.
/// </summary>
public class EnumListCoverageTests
{
    private static List<string> GameList(string name, params string[] skip) =>
        (StatsSchema.Instance.GetValueList(name)?.Values ?? throw new InvalidOperationException($"no value list {name}"))
            .Where(v => !skip.Contains(v)).ToList();

    private static string VanillaStats()
    {
        var asm = typeof(StatsSchema).Assembly;
        return string.Join("\n", asm.GetManifestResourceNames()
            .Where(n => n.Contains(".Vanilla.") && n.EndsWith(".txt"))
            .Select(n => { using var s = asm.GetManifestResourceStream(n)!; return new StreamReader(s).ReadToEnd(); }));
    }

    [Theory]
    [InlineData("Surface Type", nameof(BoostMapping.SurfaceTypes))]
    [InlineData("Death Type", nameof(BoostMapping.DeathTypes))]
    [InlineData("AttributeFlags", nameof(BoostMapping.AttributeFlags))]
    public void BoostList_HoldsTheGamesValueList(string valueList, string field)
    {
        var ours = (string[])typeof(BoostMapping).GetField(field, BindingFlags.Public | BindingFlags.Static)!.GetValue(null)!;
        Assert.Empty(GameList(valueList).Except(ours));
    }

    [Fact]
    public void StatusGroups_HoldTheGamesGroups_AndNoInventedOnes()
    {
        var game = GameList("StatusGroupFlags", "SG_None");
        Assert.Empty(game.Except(ConditionSchema.StatusGroups));
        // Only the game's groups, and one its statuses use beyond the list.
        Assert.Equal(["SG_Sleeping_Magical"], ConditionSchema.StatusGroups.Except(game));
    }

    [Fact]
    public void ActionResources_HoldEveryResourceTheGamesStatsUse()
    {
        var text = VanillaStats();
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (Match m in Regex.Matches(text, @"\b(?:ActionResource\w*|RestoreResource)\(\s*(?:SELF\s*,\s*|TARGET\s*,\s*)?([A-Za-z]\w*)"))
            used.Add(m.Groups[1].Value);
        foreach (Match m in Regex.Matches(text, "data \"(?:UseCosts|DualWieldingUseCosts|HitCosts|RitualCosts)\" \"([^\"]*)\""))
            foreach (var part in m.Groups[1].Value.Split(';'))
                if (part.Split(':')[0].Trim() is { Length: > 0 } name) used.Add(name);
        // Charges of one feature (Interrupt_Portent_3, SneakAttack_Charge…) are not resources to offer.
        used.RemoveWhere(n => n.StartsWith("Interrupt_") || (n.EndsWith("Charge") && n is not ("LayOnHandsCharge" or "LegendaryResistanceCharge"))
                              || n is "EyeStalkActionPoint" or "AstralPlanePoint");
        Assert.Empty(used.Except(BoostMapping.ActionResources));
        Assert.DoesNotContain("NaturalRecovery", BoostMapping.ActionResources);
    }
}
