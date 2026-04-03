using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

public class IconDebugTest
{
    private readonly ITestOutputHelper _output;

    public IconDebugTest(ITestOutputHelper output) => _output = output;

    [Fact]
    public void Debug_MAG_Ring35_1_IconChain()
    {
        // Find AMP pak (skip on CI — requires local game installation)
        var modsDir = ModsFolderDetector.Detect();
        if (modsDir == null) return; // Skip gracefully on CI

        var ampPak = Directory.GetFiles(modsDir, "REL_Full_Ancient*.pak").FirstOrDefault();
        Assert.NotNull(ampPak);
        _output.WriteLine($"AMP pak: {ampPak}");

        // Build resolver
        var vanillaDb = new VanillaDatabase();
        vanillaDb.Load();
        var resolver = new StatsResolver();
        foreach (var kvp in vanillaDb.Resolver.AllEntries)
            resolver.AddEntries(new[] { kvp.Value });

        // Add AMP stats
        using var fs = File.OpenRead(ampPak);
        var header = ParaTool.Core.PakReader.ReadHeader(fs);
        var entries = ParaTool.Core.PakReader.ReadFileList(fs, header);

        var statFiles = entries.Where(e =>
            e.Path.Contains("Stats/Generated/Data", StringComparison.OrdinalIgnoreCase) &&
            e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToList();

        foreach (var sf in statFiles)
        {
            var data = ParaTool.Core.PakReader.ExtractFileData(fs, sf);
            var text = System.Text.Encoding.UTF8.GetString(data);
            var parsed = StatsParser.Parse(text);
            resolver.AddEntries(parsed);
        }

        // Walk using chain for MAG_Ring35_1
        _output.WriteLine("\n=== Using chain for MAG_Ring35_1 ===");
        var current = "MAG_Ring35_1";
        int depth = 0;
        string? rootTemplateUuid = null;

        while (current != null && depth < 20)
        {
            var entry = resolver.Get(current);
            if (entry == null)
            {
                _output.WriteLine($"  [{depth}] {current} → NOT FOUND");
                break;
            }

            var rt = entry.Data.TryGetValue("RootTemplate", out var rtVal) ? rtVal : null;
            _output.WriteLine($"  [{depth}] {current} (type={entry.Type}, using={entry.Using}, RootTemplate={rt ?? "none"})");

            if (rt != null && rootTemplateUuid == null)
                rootTemplateUuid = rt;

            current = entry.Using;
            depth++;
        }

        _output.WriteLine($"\nResolved RootTemplate UUID: {rootTemplateUuid ?? "NONE"}");

        // Now search for this UUID in RootTemplate files and find Icon
        if (rootTemplateUuid != null)
        {
            _output.WriteLine($"\n=== Searching RootTemplates for UUID {rootTemplateUuid} ===");

            var rtFiles = entries.Where(e =>
                (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
                 e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
                (e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) ||
                 e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase))).ToList();

            var uuids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { rootTemplateUuid };

            foreach (var rtFile in rtFiles)
            {
                var data = ParaTool.Core.PakReader.ExtractFileData(fs, rtFile);
                var text = System.Text.Encoding.Latin1.GetString(data);

                if (!text.Contains(rootTemplateUuid, StringComparison.OrdinalIgnoreCase))
                {
                    // Try binary GUID
                    if (Guid.TryParse(rootTemplateUuid, out var guid))
                    {
                        var guidBytes = guid.ToByteArray();
                        bool found = false;
                        for (int i = 0; i <= data.Length - guidBytes.Length; i++)
                        {
                            bool match = true;
                            for (int j = 0; j < guidBytes.Length; j++)
                                if (data[i + j] != guidBytes[j]) { match = false; break; }
                            if (match) { found = true; break; }
                        }
                        if (!found) continue;
                    }
                    else continue;
                }

                _output.WriteLine($"  Found UUID in: {rtFile.Path}");

                // Try to find Icon
                var icons = LsfScanner.FindIconNamesForUuids(data, uuids);
                foreach (var (uuid, icon) in icons)
                    _output.WriteLine($"  Icon found: {uuid} → {icon}");

                if (icons.Count == 0)
                    _output.WriteLine($"  No Icon extracted by FindIconNamesForUuids");

                // Try handles too
                var (nh, dh) = LsfScanner.FindHandlesForUuidsEx(data, uuids);
                foreach (var (uuid, h) in nh) _output.WriteLine($"  DisplayName handle: {uuid} → {h}");
                foreach (var (uuid, h) in dh) _output.WriteLine($"  Description handle: {uuid} → {h}");
            }
        }

        // Also check what IconService.FindIconName returns
        var iconService = new IconService(new[] { ampPak });
        var foundIcon = iconService.FindIconName("MAG_Ring35_1", resolver);
        _output.WriteLine($"\nIconService.FindIconName result: {foundIcon ?? "NULL"}");
    }
}
