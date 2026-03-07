using Xunit;
using ParaTool.Core.Models;
using ParaTool.Core.Parsing;
using ParaTool.Core.Patching;

namespace ParaTool.Tests;

public class Version64Tests
{
    private const string MetaXmlTemplate = """
        <?xml version="1.0" encoding="UTF-8"?>
        <save>
            <version major="4" minor="0" revision="0" build="49" />
            <region id="Config">
                <node id="root">
                    <children>
                        <node id="Dependencies">
                            <children />
                        </node>
                        <node id="ModuleInfo">
                            <attribute id="Folder" type="LSString" value="TestMod" />
                            <attribute id="Name" type="LSString" value="Test Mod" />
                            <attribute id="UUID" type="guid" value="aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee" />
                            <attribute id="Version64" type="int64" value="{0}" />
                        </node>
                    </children>
                </node>
            </region>
        </save>
        """;

    [Fact]
    public void Parser_ReadsVersion64()
    {
        var xml = string.Format(MetaXmlTemplate, "361695366548029450");
        var data = System.Text.Encoding.UTF8.GetBytes(xml);

        var mod = MetaLsxParser.Parse(data, "/test.pak");

        Assert.NotNull(mod);
        Assert.Equal("361695366548029450", mod.Version64);
    }

    [Fact]
    public void Parser_DefaultsVersion64_WhenMissing()
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
        var data = System.Text.Encoding.UTF8.GetBytes(xml);

        var mod = MetaLsxParser.Parse(data, "/test.pak");

        Assert.NotNull(mod);
        Assert.Equal("36028797018963968", mod.Version64);
    }

    [Fact]
    public void Patcher_WritesRealVersion64()
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
                                <attribute id="UUID" type="guid" value="11111111-2222-3333-4444-555555555555" />
                            </node>
                        </children>
                    </node>
                </region>
            </save>
            """;

        var mods = new List<ModInfo>
        {
            new()
            {
                Name = "Test Mod",
                UUID = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee",
                Folder = "TestMod",
                PakPath = "/test.pak",
                Version64 = "361695366548029450"
            }
        };

        var result = MetaLsxPatcher.Patch(ampMeta, mods);

        Assert.Contains("361695366548029450", result);
        Assert.DoesNotContain("36028797018963968", result);
    }

    [Fact]
    public void Patcher_RoundTrip_PreservesVersion64()
    {
        var version = "361695366548029450";
        var xml = string.Format(MetaXmlTemplate, version);
        var data = System.Text.Encoding.UTF8.GetBytes(xml);

        var mod = MetaLsxParser.Parse(data, "/test.pak")!;

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
                            </node>
                        </children>
                    </node>
                </region>
            </save>
            """;

        var result = MetaLsxPatcher.Patch(ampMeta, new[] { mod });

        Assert.Contains($"value=\"{version}\"", result);
    }
}
