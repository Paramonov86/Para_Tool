using Xunit;
using Xunit.Abstractions;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using ParaTool.Core.Models;

namespace ParaTool.Tests;

public class QuickResolveTest
{
    private readonly ITestOutputHelper _o;
    public QuickResolveTest(ITestOutputHelper o) => _o = o;

    [Fact]
    public void EndToEndNameResolve()
    {
        var ampPak = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak");
        if (!File.Exists(ampPak)) { _o.WriteLine("NO PAK"); return; }

        // Step 1: Build UUID map (same as ResolveDisplayNames does)
        var uuidMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["0c9fd9cd-b317-4f00-be1a-c062b06894cb"] = new() { "AMP_Bloodlupus_Robe" }
        };

        // Step 2: Resolve
        var result = ItemNameResolver.ResolveFromPak(ampPak, uuidMap, "en");
        _o.WriteLine($"ResolveFromPak result: {result.Count}");
        foreach (var (k, v) in result)
            _o.WriteLine($"  {k} -> {v}");

        // Step 3: Manual check - scan the specific RT file
        using var fs = File.OpenRead(ampPak);
        var header = Core.PakReader.ReadHeader(fs);
        var entries = Core.PakReader.ReadFileList(fs, header);

        var rtEntries = entries.Where(e =>
            e.Path.Contains("RootTemplates") && e.Path.EndsWith(".lsf")).ToList();
        _o.WriteLine($"RT files: {rtEntries.Count}");

        var target = rtEntries.FirstOrDefault(e =>
            e.Path.Contains("0c9fd9cd"));
        _o.WriteLine($"Target file: {target.Path ?? "NOT FOUND"}");

        if (target.Path != null)
        {
            var data = Core.PakReader.ExtractFileData(fs, target);
            var fileName = Path.GetFileNameWithoutExtension(target.Path);
            _o.WriteLine($"FileName: '{fileName}'");
            _o.WriteLine($"UUID matches: {fileName.Equals("0c9fd9cd-b317-4f00-be1a-c062b06894cb", StringComparison.OrdinalIgnoreCase)}");

            var handle = LsfScanner.FindHandleNearUuid(data, "0c9fd9cd-b317-4f00-be1a-c062b06894cb");
            _o.WriteLine($"Handle from scanner: {handle ?? "null"}");

            // Check loca
            var handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (handle != null) handles.Add(handle);
            var loca = ItemNameResolver.ReadLocalization(fs, entries, handles, "en");
            _o.WriteLine($"Loca result: {loca.Count}");
            foreach (var (k, v) in loca) _o.WriteLine($"  {k} -> {v}");
        }
    }
}
