using ParaTool.Core;
using ParaTool.Core.LSLib;
using Xunit;

namespace ParaTool.Tests;

public class TemplateDumpTest
{
    [Fact]
    public void DumpArtifactTemplate()
    {
        var pakPath = @"C:\Users\user\AppData\Local\Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak";
        if (!File.Exists(pakPath)) return;

        using var fs = File.OpenRead(pakPath);
        var header = PakReader.ReadHeader(fs);
        var entries = PakReader.ReadFileList(fs, header);

        var output = "";

        // Find our generated template
        var rtEntry = entries.FirstOrDefault(e => e.Path.Contains("76664a3f"));
        if (rtEntry.Path != null)
        {
            var data = PakReader.ExtractFileData(fs, rtEntry);
            output += $"=== {rtEntry.Path} ({data.Length} bytes) ===\n";

            using var ms = new MemoryStream(data);
            var reader = new LSFReader(ms);
            var resource = reader.Read();

            foreach (var region in resource.Regions)
                DumpNode(region.Value, ref output, "");
        }
        else
        {
            output += "Template 76664a3f NOT FOUND in pak\n";
        }

        // Also dump parent for comparison
        var parentEntry = entries.FirstOrDefault(e => e.Path.Contains("b0289edb"));
        if (parentEntry.Path != null)
        {
            var data = PakReader.ExtractFileData(fs, parentEntry);
            output += $"\n=== PARENT {parentEntry.Path} ({data.Length} bytes) ===\n";

            using var ms = new MemoryStream(data);
            var reader = new LSFReader(ms);
            var resource = reader.Read();

            foreach (var region in resource.Regions)
                DumpNode(region.Value, ref output, "");
        }

        File.WriteAllText(@"C:\Users\user\AppData\Local\Temp\template_dump.txt", output);
    }

    private static void DumpNode(Node node, ref string output, string indent)
    {
        output += $"{indent}{node.Name}";
        if (node.Attributes.Count > 0)
        {
            output += " {";
            foreach (var a in node.Attributes)
            {
                var v = a.Value.Value;
                if (v is TranslatedString ts)
                    output += $" {a.Key}={ts.Handle};";
                else if (v is byte[])
                    output += $" {a.Key}=[bytes];";
                else
                    output += $" {a.Key}={v};";
            }
            output += " }\n";
        }
        else output += "\n";

        foreach (var cl in node.Children)
            foreach (var c in cl.Value)
                DumpNode(c, ref output, indent + "  ");
    }
}
