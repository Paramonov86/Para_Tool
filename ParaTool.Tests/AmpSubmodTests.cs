using ParaTool.Core.Models;
using ParaTool.Core.Parsing;
using ParaTool.Core.Patching;
using Xunit;

namespace ParaTool.Tests;

/// <summary>
/// AMP submods (paks that declare AMP as a dependency, e.g. Ancient Mega Pack Plus) load after
/// AMP and rebalance its items. Writing them into AMP's own dependencies creates a load cycle.
/// </summary>
public class AmpSubmodTests : IDisposable
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

    // Rarity/price overrides written into the submod a patch goes into. A name declared twice
    // inside one pak does not build on the first declaration, so an entry the submod declares
    // itself is edited where it stands; only the rest gets a thin `using "<self>"` behind it.

    private string SubmodStats(params (string file, string text)[] files)
    {
        var dir = Path.Combine(Path.GetTempPath(), "pt_submodstats_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        _dirs.Add(dir);
        foreach (var (file, text) in files)
            File.WriteAllText(Path.Combine(dir, file), text);
        return dir;
    }

    private readonly List<string> _dirs = [];

    public void Dispose()
    {
        foreach (var d in _dirs)
            try { Directory.Delete(d, true); } catch { }
    }

    private static List<StatsEntry> Declarations(string dir, string name) => Directory
        .GetFiles(dir, "*.txt").OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
        .SelectMany(f => StatsParser.Parse(File.ReadAllText(f)))
        .Where(e => e.Name == name).ToList();

    private const string RapierFile = """
        new entry "AmpPlus_Rapier"
        type "Weapon"
        using "MAG_Harpers_Harmonizing_Rapier"
        data "RootTemplate" "77ee7797-c3f4-4067-bc2e-0151daf6b5ad"
        data "Rarity" "VeryRare"
        data "PassivesOnEquip" "AmpPlus_Rapier_falseNote"

        """;

    private const string CapsFile = """
        new entry "MAG_Armour_H_115"
        type "Armor"
        using "MAG_Armour_H_115"
        data "Boosts" "Ability(Charisma,1,22)"

        """;

    [Fact]
    public void SubmodOverrides_SubmodOwnItem_EditedInPlace_NeverRedeclared()
    {
        // A second `new entry "AmpPlus_Rapier" using "AmpPlus_Rapier"` in its own pak left the
        // rapier inheriting from nothing: it vanished from every chest after any patch.
        var dir = SubmodStats(("AMP_Plus_Rapiers.txt", RapierFile), ("ZZ_Last.txt", "new entry \"Other\"\ntype \"Armor\"\n"));

        AmpPatcher.WriteSubmodStatOverrides(dir, [Item("AmpPlus_Rapier", rarity: "Legendary")]);

        var decl = Assert.Single(Declarations(dir, "AmpPlus_Rapier"));
        Assert.Equal("MAG_Harpers_Harmonizing_Rapier", decl.Using);
        Assert.Equal("Legendary", decl.Data["Rarity"]);
        Assert.Equal("77ee7797-c3f4-4067-bc2e-0151daf6b5ad", decl.Data["RootTemplate"]);
        Assert.Equal("AmpPlus_Rapier_falseNote", decl.Data["PassivesOnEquip"]);
    }

    [Fact]
    public void SubmodOverrides_AmpItemTheSubmodRebalances_KeepsTheRebalance()
    {
        var dir = SubmodStats(("AMP_Plus_AbilityCaps.txt", CapsFile), ("ZZ_Last.txt", ""));

        AmpPatcher.WriteSubmodStatOverrides(dir, [Item("MAG_Armour_H_115", rarity: "VeryRare")]);

        var decl = Assert.Single(Declarations(dir, "MAG_Armour_H_115"));
        Assert.Equal("Ability(Charisma,1,22)", decl.Data["Boosts"]);
        Assert.Equal("VeryRare", decl.Data["Rarity"]);
    }

    [Fact]
    public void SubmodOverrides_AmpItemTheSubmodDoesNotDeclare_ThinRedeclarationInLastFile()
    {
        var dir = SubmodStats(("AMP_Plus_AbilityCaps.txt", CapsFile), ("ZZ_Last.txt", "new entry \"Other\"\ntype \"Armor\"\n"));

        AmpPatcher.WriteSubmodStatOverrides(dir, [Item("AMP_Boots_PerfectCrime")]);

        var decl = Assert.Single(Declarations(dir, "AMP_Boots_PerfectCrime"));
        Assert.Equal("AMP_Boots_PerfectCrime", decl.Using);
        Assert.Equal("Rare", decl.Data["Rarity"]);
        Assert.Contains("AMP_Boots_PerfectCrime", File.ReadAllText(Path.Combine(dir, "ZZ_Last.txt")));
    }

    [Fact]
    public void SubmodOverrides_EveryDeclarationInThePakIsEdited()
    {
        // The last declaration of a name is the one that counts.
        var dir = SubmodStats(("AMP_Plus_A.txt", CapsFile), ("AMP_Plus_B.txt", CapsFile));

        AmpPatcher.WriteSubmodStatOverrides(dir, [Item("MAG_Armour_H_115", rarity: "Legendary")]);

        var decls = Declarations(dir, "MAG_Armour_H_115");
        Assert.Equal(2, decls.Count);
        Assert.All(decls, d => Assert.Equal("Legendary", d.Data["Rarity"]));
    }

    [Fact]
    public void SubmodOverrides_DeduplicateStatIdsAcrossSources()
    {
        var dir = SubmodStats(("ZZ_Last.txt", ""));

        AmpPatcher.WriteSubmodStatOverrides(dir, [Item("AMP_Boots_PerfectCrime"), Item("AMP_Boots_PerfectCrime")]);

        Assert.Single(Declarations(dir, "AMP_Boots_PerfectCrime"));
    }

    [Fact]
    public void SubmodOverrides_SkipCommonItems()
    {
        var dir = SubmodStats(("AMP_Plus_Rapiers.txt", RapierFile));

        AmpPatcher.WriteSubmodStatOverrides(dir, [Item("AmpPlus_Rapier", rarity: "Common"), Item("AmpPlus_Junk", rarity: "Common")]);

        Assert.Equal(RapierFile, File.ReadAllText(Path.Combine(dir, "AMP_Plus_Rapiers.txt")));
        Assert.Single(Directory.GetFiles(dir));
    }

    private static ModInfo Submod(string name, params string[] dependsOn) => new()
    {
        Name = name,
        UUID = $"{name}-uuid",
        Folder = name,
        PakPath = $"/{name}.pak",
        IsAmpSubmod = true,
        DependencyUuids = new HashSet<string>(
            dependsOn.Select(d => $"{d}-uuid").Append(AmpUuid), StringComparer.OrdinalIgnoreCase),
    };

    [Fact]
    public void PatchTarget_OnlyAmp_IsAmpItself()
    {
        Assert.Null(AmpPatcher.SelectPatchTarget([Mod("RegularMod", isSubmod: false)]));
    }

    [Fact]
    public void PatchTarget_WithSubmod_IsTheSubmod()
    {
        var target = AmpPatcher.SelectPatchTarget([Mod("RegularMod", isSubmod: false), Submod("AMP_Plus")]);

        Assert.Equal("AMP_Plus", target?.Name);
    }

    [Fact]
    public void PatchTarget_SubmodThatDependsOnAnother_LoadsLastAndWins()
    {
        // Name order alone would pick AMP_Plus; the dependency says AAA_Patch loads after it.
        var target = AmpPatcher.SelectPatchTarget([Submod("AMP_Plus"), Submod("AAA_Patch", "AMP_Plus")]);

        Assert.Equal("AAA_Patch", target?.Name);
    }

    [Fact]
    public void PatchTarget_UnrelatedSubmods_PickDoesNotDependOnScanOrder()
    {
        var a = Submod("AMP_Plus");
        var b = Submod("zAMP_DND24_Patch");

        Assert.Equal(AmpPatcher.SelectPatchTarget([a, b])?.Name, AmpPatcher.SelectPatchTarget([b, a])?.Name);
    }
}
