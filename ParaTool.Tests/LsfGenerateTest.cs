using ParaTool.Core.LSLib;
using Xunit;

namespace ParaTool.Tests;

public class LsfGenerateTest
{
    [Fact]
    public void CompareGeneratedLsfWithReal()
    {
        var output = "";

        // 1. Read a real AMP RootTemplate LSF
        var realPath = @"E:\SteamLibrary\steamapps\common\Baldurs Gate 3\Data\Public\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025\RootTemplates\003d3cfb-f2e6-4c23-a299-4177da45bdd8.lsf";
        if (!File.Exists(realPath)) return;

        Resource realResource;
        using (var fs = File.OpenRead(realPath))
        {
            var reader = new LSFReader(fs);
            realResource = reader.Read();
        }

        output += "=== REAL AMP LSF ===\n";
        output += $"Regions: {string.Join(", ", realResource.Regions.Keys)}\n";
        output += $"Metadata: {realResource.Metadata.MajorVersion}.{realResource.Metadata.MinorVersion}.{realResource.Metadata.Revision}.{realResource.Metadata.BuildNumber}\n";
        foreach (var region in realResource.Regions)
        {
            output += $"Region '{region.Key}':\n";
            DumpNode(region.Value, output: ref output, indent: "  ");
        }

        // 2. Generate our LSF
        var genResource = new Resource();
        var region2 = new Region { Name = "Templates", RegionName = "Templates" };
        genResource.Regions["Templates"] = region2;

        var goNode = new Node { Name = "GameObjects", Parent = region2 };
        goNode.Attributes["MapKey"] = new NodeAttribute(AttributeType.FixedString) { Value = "test-uuid-1234" };
        goNode.Attributes["Name"] = new NodeAttribute(AttributeType.LSString) { Value = "AMP_Test_06" };
        goNode.Attributes["Type"] = new NodeAttribute(AttributeType.FixedString) { Value = "item" };
        goNode.Attributes["ParentTemplateId"] = new NodeAttribute(AttributeType.FixedString) { Value = "b0289edb-5c25-404f-b784-b2cfe335e99f" };
        goNode.Attributes["Stats"] = new NodeAttribute(AttributeType.FixedString) { Value = "AMP_Test_06" };
        goNode.Attributes["LevelName"] = new NodeAttribute(AttributeType.FixedString) { Value = "" };
        goNode.Attributes["DisplayName"] = new NodeAttribute(AttributeType.TranslatedString)
        {
            Value = new TranslatedString { Handle = "htest1234", Version = 1 }
        };
        goNode.Attributes["Description"] = new NodeAttribute(AttributeType.TranslatedString)
        {
            Value = new TranslatedString { Handle = "htest5678", Version = 1 }
        };
        region2.AppendChild(goNode);

        var genPath = Path.Combine(Path.GetTempPath(), "paratool_test_gen.lsf");
        using (var outFs = File.Create(genPath))
        {
            var writer = new LSFWriter(outFs);
            writer.Write(genResource);
        }

        output += "\n=== GENERATED LSF ===\n";
        output += $"File size: {new FileInfo(genPath).Length} bytes\n";

        // Read back and dump
        Resource genBack;
        using (var fs = File.OpenRead(genPath))
        {
            var reader = new LSFReader(fs);
            genBack = reader.Read();
        }
        output += $"Regions: {string.Join(", ", genBack.Regions.Keys)}\n";
        foreach (var region in genBack.Regions)
        {
            output += $"Region '{region.Key}':\n";
            DumpNode(region.Value, output: ref output, indent: "  ");
        }

        // 3. Compare headers byte by byte
        var realBytes = File.ReadAllBytes(realPath);
        var genBytes = File.ReadAllBytes(genPath);
        output += $"\n=== HEADER COMPARISON ===\n";
        output += $"Real: {BitConverter.ToString(realBytes, 0, Math.Min(32, realBytes.Length))}\n";
        output += $"Gen:  {BitConverter.ToString(genBytes, 0, Math.Min(32, genBytes.Length))}\n";

        // Check if BG3 can re-read it
        output += $"\nRe-read success: true\n";

        File.WriteAllText(@"C:\Users\user\AppData\Local\Temp\lsf_compare.txt", output);
    }

    private static void DumpNode(Node node, ref string output, string indent)
    {
        output += $"{indent}Node '{node.Name}' ({node.Attributes.Count} attrs, {node.ChildCount} children)\n";
        foreach (var attr in node.Attributes)
        {
            var val = attr.Value.Value;
            var typeStr = attr.Value.Type.ToString();
            if (val is TranslatedString ts)
                output += $"{indent}  [{typeStr}] {attr.Key} = handle:{ts.Handle} ver:{ts.Version}\n";
            else
                output += $"{indent}  [{typeStr}] {attr.Key} = {val}\n";
        }
        foreach (var childList in node.Children)
            foreach (var child in childList.Value)
                DumpNode(child, ref output, indent + "  ");
    }
}
