using ParaTool.Core.Models;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// What counts as an edit in the patcher list: only a value that differs from the detected one.
/// </summary>
public class ItemEntryTests
{
    private static ItemEntry Item() => new()
    {
        StatId = "AMP_Item", StatType = "Armor",
        DetectedPool = "Hats", DetectedRarity = "VeryRare", DetectedThemes = ["Arcane", "Primal"],
    };

    [Fact]
    public void Untouched_IsNotModified() => Assert.False(Item().IsModified);

    [Fact]
    public void ThemesEqualToDetected_InAnyOrder_AreNotAnEdit()
    {
        var item = Item();
        item.UserThemes = ["Primal", "Arcane"];

        Assert.False(item.IsModified);
    }

    [Fact]
    public void DifferentThemes_AreAnEdit()
    {
        var item = Item();
        item.UserThemes = ["Arcane"];

        Assert.True(item.IsModified);
    }

    [Fact]
    public void PoolOrRarityEqualToDetected_AreNotAnEdit_DifferentOnesAre()
    {
        var same = Item();
        same.UserPool = "Hats";
        same.UserRarity = "VeryRare";
        Assert.False(same.IsModified);

        var pool = Item();
        pool.UserPool = "Cloaks";
        Assert.True(pool.IsModified);

        var rarity = Item();
        rarity.UserRarity = "Legendary";
        Assert.True(rarity.IsModified);
    }

    [Fact]
    public void Unchecked_IsAnEdit()
    {
        var item = Item();
        item.Enabled = false;

        Assert.True(item.IsModified);
    }
}
