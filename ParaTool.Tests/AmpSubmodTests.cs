using ParaTool.Core.Models;
using ParaTool.Core.Parsing;
using ParaTool.Core.Patching;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// AMP submods (paks that declare AMP as a dependency, e.g. Ancient Mega Pack Plus) load after
/// AMP and rebalance its items. Writing them into AMP's own dependencies creates a load cycle.
/// </summary>
public class AmpSubmodTests
{
    private const string AmpUuid = "c6c0d2bd-6198-de9e-30ad-e8cda1793025";

    private static byte[] SubmodMeta(string uuid, params string[] dependencyUuids)
    {
        var deps = string.Join("\n", dependencyUuids.Select(d => $"""
                                <node id="ModuleShortDesc">
                                    <attribute id="Folder" type="LSString" value="Dep" />
                                    <attribute id="Name" type="LSString" value="Dep" />
                                    <attribute id="UUID" type="guid" value="{d}" />
                                </node>
            """));

        var xml = $"""
            <?xml version="1.0" encoding="UTF-8"?>
            <save>
                <region id="Config">
                    <node id="root">
                        <children>
                            <node id="Dependencies">
                                <children>
            {deps}
                                </children>
                            </node>
                            <node id="ModuleInfo">
                                <attribute id="Folder" type="LSString" value="Submod" />
                                <attribute id="Name" type="LSString" value="Test Submod" />
                                <attribute id="UUID" type="guid" value="{uuid}" />
                            </node>
                        </children>
                    </node>
                </region>
            </save>
            """;
        return System.Text.Encoding.UTF8.GetBytes(xml);
    }

    [Fact]
    public void Parser_ReadsDependencyUuids()
    {
        var mod = MetaLsxParser.Parse(SubmodMeta("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee", AmpUuid), "/test.pak");

        Assert.NotNull(mod);
        Assert.Contains(AmpUuid, mod.DependencyUuids);
    }

    [Fact]
    public void Parser_ReturnsEmptyDependencies_WhenNoneDeclared()
    {
        var xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <save>
                <region id="Config">
                    <node id="root">
                        <children>
                            <node id="ModuleInfo">
                                <attribute id="Folder" type="LSString" value="TestMod" />
                                <attribute id="Name" type="LSString" value="Test Mod" />
                                <attribute id="UUID" type="guid" value="aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" />
                            </node>
                        </children>
                    </node>
                </region>
            </save>
            """;

        var mod = MetaLsxParser.Parse(System.Text.Encoding.UTF8.GetBytes(xml), "/test.pak");

        Assert.NotNull(mod);
        Assert.Empty(mod.DependencyUuids);
    }

    private static ModInfo Mod(string name, bool isSubmod, bool enabledItem = true) => new()
    {
        Name = name,
        UUID = Guid.NewGuid().ToString(),
        Folder = name,
        PakPath = $"/{name}.pak",
        IsAmpSubmod = isSubmod,
        Items =
        [
            new ItemEntry { StatId = $"{name}_Item", StatType = "Armor", Enabled = enabledItem }
        ]
    };

    private static ItemEntry Item(string statId, string rarity = "Rare") =>
        new() { StatId = statId, StatType = "Armor", Enabled = true, UserRarity = rarity };

    [Fact]
    public void SelectDependencyMods_ExcludesAmpSubmods()
    {
        var mods = new List<ModInfo> { Mod("RegularMod", isSubmod: false), Mod("AmpPlus", isSubmod: true) };

        var selected = AmpPatcher.SelectDependencyMods(mods);

        Assert.Single(selected);
        Assert.Equal("RegularMod", selected[0].Name);
    }

    [Fact]
    public void MetaPatch_DoesNotAddSubmod_SoNoDependencyCycle()
    {
        var ampMeta = """
            <?xml version="1.0" encoding="UTF-8"?>
            <save>
                <region id="Config">
                    <node id="root">
                        <children>
                            <node id="Dependencies">
                                <children />
                            </node>
                            <node id="ModuleInfo">
                                <attribute id="Folder" type="LSString" value="AMP" />
                                <attribute id="Name" type="LSString" value="Ancient Mega Pack" />
                                <attribute id="UUID" type="guid" value="c6c0d2bd-6198-de9e-30ad-e8cda1793025" />
                            </node>
                        </children>
                    </node>
                </region>
            </save>
            """;

        var mods = new List<ModInfo> { Mod("RegularMod", isSubmod: false), Mod("AmpPlus", isSubmod: true) };
        var submodUuid = mods[1].UUID;

        var patched = MetaLsxPatcher.Patch(ampMeta, AmpPatcher.SelectDependencyMods(mods));

        Assert.Contains(mods[0].UUID, patched);
        Assert.DoesNotContain(submodUuid, patched);
    }

    [Fact]
    public void SubmodOverrides_IncludeSubmodOwnItems()
    {
        // Before the submod pass these items were dropped entirely: their skeleton could not go
        // into AMP (the base does not exist there yet), so rarity/price edits reached nothing.
        var text = AmpPatcher.BuildSubmodOverrideText([], [], [Item("AmpPlus_Boots")], "");

        Assert.Contains("new entry \"AmpPlus_Boots\"", text);
        Assert.Contains("using \"AmpPlus_Boots\"", text);
        Assert.Contains("data \"Rarity\" \"Rare\"", text);
    }

    [Fact]
    public void SubmodOverrides_DeduplicateStatIdsAcrossSources()
    {
        var text = AmpPatcher.BuildSubmodOverrideText(
            [Item("AMP_Boots_PerfectCrime")], [], [Item("AMP_Boots_PerfectCrime")], "");

        Assert.Equal(1, CountOccurrences(text, "new entry \"AMP_Boots_PerfectCrime\""));
    }

    [Fact]
    public void SubmodOverrides_PutArtifactOverridesLast_SoTheyWin()
    {
        // BG3 takes the last declaration of an entry, so the Constructor's fuller override
        // (Boosts, slot identity) has to sit behind the rarity/price skeleton for the same item.
        var artifact = "new entry \"AMP_Boots_PerfectCrime\"\ntype \"Armor\"\n"
            + "using \"AMP_Boots_PerfectCrime\"\ndata \"Boosts\" \"Ability(Dexterity,2,24)\"\n";

        var text = AmpPatcher.BuildSubmodOverrideText([Item("AMP_Boots_PerfectCrime")], [], [], artifact);

        Assert.True(text.IndexOf("data \"Boosts\"", StringComparison.Ordinal)
            > text.IndexOf("data \"Rarity\"", StringComparison.Ordinal));
    }

    [Fact]
    public void SubmodOverrides_SkipCommonItems()
    {
        var text = AmpPatcher.BuildSubmodOverrideText([], [], [Item("AmpPlus_Junk", rarity: "Common")], "");

        Assert.Equal("", text);
    }

    private const string RestatedParagonTable = "new treasuretable \"AMP_Para_14\"\n"
        + "new subtable \"1,1\"\nobject category \"I_AMP_Kept\",1,0,0,0,0,0,0,0\n"
        + "new subtable \"1,1\"\nobject category \"I_AMP_Unchecked\",1,0,0,0,0,0,0,0\n";

    private static ItemEntry AmpItem(string statId, bool enabled) => new()
    {
        StatId = statId, StatType = "Armor", DetectedPool = "Rings", DetectedRarity = "Rare",
        IsAmpItem = true, Enabled = enabled
    };

    /// <summary>Packs a submod into root/Mods/Sub.pak, so its backup lands in root.</summary>
    private static string BuildSubmodPak(string root, string treasureTable)
    {
        var src = Path.Combine(root, "src");
        var metaDir = Path.Combine(src, "Mods", "Sub");
        var generated = Path.Combine(src, "Public", "Sub", "Stats", "Generated");
        Directory.CreateDirectory(metaDir);
        Directory.CreateDirectory(generated);
        File.WriteAllBytes(Path.Combine(metaDir, "meta.lsx"), SubmodMeta(Guid.NewGuid().ToString(), AmpUuid));
        File.WriteAllText(Path.Combine(generated, "TreasureTable.txt"), treasureTable);

        var modsDir = Path.Combine(root, "Mods");
        Directory.CreateDirectory(modsDir);
        var pak = Path.Combine(modsDir, "Sub.pak");
        ParaTool.Core.PakWriter.CreatePak(src, pak);
        return pak;
    }

    private static Dictionary<string, string> ReadPakFiles(string pak)
    {
        using var fs = File.OpenRead(pak);
        var header = ParaTool.Core.PakReader.ReadHeader(fs);
        return ParaTool.Core.PakReader.ReadFileList(fs, header).ToDictionary(
            e => e.Path,
            e => System.Text.Encoding.UTF8.GetString(ParaTool.Core.PakReader.ExtractFileData(fs, e)));
    }

    private static void WithTempRoot(Action<string> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "paratool-submod-" + Guid.NewGuid().ToString("N"));
        try { body(root); }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [Fact]
    public void SubmodPak_RestatedLootTable_GetsTheSameEdits()
    {
        // AMP Plus restates AMP_Para_14 (Aquatic) and loads after AMP, so removing an item only
        // from AMP's copy left it in the chest the game actually rolls.
        WithTempRoot(root =>
        {
            var pak = BuildSubmodPak(root, RestatedParagonTable);

            var (patched, dropped) = AmpPatcher.PatchSubmodPak(pak, "",
                [AmpItem("AMP_Kept", enabled: true), AmpItem("AMP_Unchecked", enabled: false)]);

            Assert.True(patched);
            Assert.False(dropped);
            var files = ReadPakFiles(pak);
            var tt = files.Single(f => f.Key.EndsWith("TreasureTable.txt")).Value;
            Assert.Contains("I_AMP_Kept", tt);
            Assert.DoesNotContain("I_AMP_Unchecked", tt);
            Assert.Contains(files.Keys, k => k.EndsWith("ZZZ_ParaTool_Overrides.txt"));
        });
    }

    [Fact]
    public void SubmodPak_NothingToChange_IsNotPatched()
    {
        WithTempRoot(root =>
        {
            var pak = BuildSubmodPak(root, RestatedParagonTable);

            var (patched, dropped) = AmpPatcher.PatchSubmodPak(pak, "",
                [AmpItem("AMP_Kept", enabled: true), AmpItem("AMP_Unchecked", enabled: true)]);

            Assert.False(patched);
            Assert.False(dropped);
        });
    }

    [Fact]
    public void SubmodPak_OverridesWithoutStatFiles_AreReportedDropped()
    {
        WithTempRoot(root =>
        {
            var pak = BuildSubmodPak(root, RestatedParagonTable);

            var (patched, dropped) = AmpPatcher.PatchSubmodPak(pak,
                "new entry \"AMP_Kept\"\ntype \"Armor\"\nusing \"AMP_Kept\"\n",
                [AmpItem("AMP_Kept", enabled: true)]);

            Assert.False(patched);
            Assert.True(dropped);
        });
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        int count = 0, i = 0;
        while ((i = haystack.IndexOf(needle, i, StringComparison.Ordinal)) >= 0) { count++; i += needle.Length; }
        return count;
    }
}
