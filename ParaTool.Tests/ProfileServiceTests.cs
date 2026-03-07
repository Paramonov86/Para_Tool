using Xunit;
using ParaTool.Core.Models;
using ParaTool.Core.Services;

namespace ParaTool.Tests;

public class ProfileServiceTests : IDisposable
{
    private readonly string _testDir;

    public ProfileServiceTests()
    {
        // Use a temp directory for tests to avoid polluting real storage
        _testDir = Path.Combine(Path.GetTempPath(), $"ParaTool_Test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_testDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDir))
            Directory.Delete(_testDir, true);
    }

    private static List<ModInfo> CreateTestMods()
    {
        return new List<ModInfo>
        {
            new()
            {
                Name = "Mod A",
                UUID = "uuid-aaa",
                Folder = "ModA",
                PakPath = "/modA.pak",
                Items = new List<ItemEntry>
                {
                    new()
                    {
                        StatId = "Item1",
                        StatType = "Armor",
                        Enabled = true,
                        UserPool = "Armor",
                        UserRarity = "Rare",
                        UserThemes = new List<string> { "Arcane", "War" }
                    },
                    new()
                    {
                        StatId = "Item2",
                        StatType = "Weapon",
                        Enabled = false,
                        UserPool = "Weapons_2H",
                        UserRarity = "Legendary",
                        UserThemes = new List<string>()
                    }
                }
            },
            new()
            {
                Name = "Mod B",
                UUID = "uuid-bbb",
                Folder = "ModB",
                PakPath = "/modB.pak",
                Items = new List<ItemEntry>
                {
                    new()
                    {
                        StatId = "Item3",
                        StatType = "Armor",
                        Enabled = true,
                        UserPool = null,
                        DetectedPool = "Boots",
                        UserRarity = null,
                        DetectedRarity = "Uncommon",
                        UserThemes = new List<string> { "Nature" }
                    }
                }
            }
        };
    }

    [Fact]
    public void CaptureState_CapturesAllItems()
    {
        var mods = CreateTestMods();
        var profile = ProfileService.CaptureState(mods);

        Assert.Equal(2, profile.Mods.Count);
        Assert.True(profile.Mods.ContainsKey("uuid-aaa"));
        Assert.True(profile.Mods.ContainsKey("uuid-bbb"));

        var modA = profile.Mods["uuid-aaa"];
        Assert.Equal("Mod A", modA.ModName);
        Assert.Equal(2, modA.Items.Count);

        var item1 = modA.Items["Item1"];
        Assert.True(item1.Enabled);
        Assert.Equal("Armor", item1.Pool);
        Assert.Equal("Rare", item1.Rarity);
        Assert.Equal(new[] { "Arcane", "War" }, item1.Themes);

        var item2 = modA.Items["Item2"];
        Assert.False(item2.Enabled);
        Assert.Equal("Legendary", item2.Rarity);
    }

    [Fact]
    public void ApplyProfile_RestoresSettings()
    {
        var mods = CreateTestMods();
        var profile = ProfileService.CaptureState(mods);

        // Reset all items to defaults
        foreach (var mod in mods)
            foreach (var item in mod.Items)
            {
                item.Enabled = true;
                item.UserPool = null;
                item.UserRarity = null;
                item.UserThemes = new List<string>();
            }

        var result = ProfileService.ApplyProfile(profile, mods);

        Assert.Equal(3, result.RestoredCount);
        Assert.Empty(result.MissingItems);

        // Verify Item2 was restored as disabled
        Assert.False(mods[0].Items[1].Enabled);
        Assert.Equal("Legendary", mods[0].Items[1].UserRarity);
        Assert.Equal(new[] { "Arcane", "War" }, mods[0].Items[0].UserThemes);
    }

    [Fact]
    public void ApplyProfile_ReportsMissingItems()
    {
        var mods = CreateTestMods();
        var profile = ProfileService.CaptureState(mods);

        // Remove Item2 from Mod A
        mods[0].Items.RemoveAt(1);

        var result = ProfileService.ApplyProfile(profile, mods);

        Assert.Equal(2, result.RestoredCount);
        Assert.Single(result.MissingItems);
        Assert.Equal("Mod A", result.MissingItems[0].ModName);
        Assert.Equal("Item2", result.MissingItems[0].StatId);
    }

    [Fact]
    public void ApplyProfile_ReportsMissingMod()
    {
        var mods = CreateTestMods();
        var profile = ProfileService.CaptureState(mods);

        // Remove Mod B entirely
        mods.RemoveAt(1);

        var result = ProfileService.ApplyProfile(profile, mods);

        Assert.Equal(2, result.RestoredCount);
        Assert.Single(result.MissingItems);
        Assert.Equal("Mod B", result.MissingItems[0].ModName);
        Assert.Equal("Item3", result.MissingItems[0].StatId);
    }

    [Fact]
    public void SaveAndLoadProfile_RoundTrip()
    {
        var mods = CreateTestMods();

        // Save directly to test dir to avoid polluting real storage
        var profilePath = Path.Combine(_testDir, "test-profile.json");
        var data = ProfileService.CaptureState(mods);
        var json = System.Text.Json.JsonSerializer.Serialize(data);
        File.WriteAllText(profilePath, json);

        // Load and verify
        var loaded = System.Text.Json.JsonSerializer.Deserialize<ProfileData>(
            File.ReadAllText(profilePath));

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Mods.Count);
        Assert.Equal("Rare", loaded.Mods["uuid-aaa"].Items["Item1"].Rarity);
        Assert.False(loaded.Mods["uuid-aaa"].Items["Item2"].Enabled);
    }

    [Fact]
    public void GetStorageDir_ReturnsValidPath()
    {
        var dir = ProfileService.GetStorageDir();
        Assert.False(string.IsNullOrEmpty(dir));
        Assert.Contains("ParaTool", dir);
    }
}
