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
}
