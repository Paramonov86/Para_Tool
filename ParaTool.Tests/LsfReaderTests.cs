using Xunit;
using Xunit.Abstractions;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;

namespace ParaTool.Tests;

public class LsfReaderTests
{
    private readonly ITestOutputHelper _o;
    public LsfReaderTests(ITestOutputHelper o) => _o = o;

    private static readonly string AmpPak = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        @"Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak");

    [SkippableFact]
    public void ScannerFindsHandleNearUuid()
    {
        var lsfPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
            @"BG3mods\Temp\temp\01\003d3cfb-f2e6-4c23-a299-4177da45bdd8.lsf");
        Skip.If(!File.Exists(lsfPath), "Test LSF not found");

        var data = File.ReadAllBytes(lsfPath);
        var handle = LsfScanner.FindHandleNearUuid(data, "003d3cfb-f2e6-4c23-a299-4177da45bdd8");

        Assert.NotNull(handle);
        Assert.StartsWith("h", handle);
        _o.WriteLine($"UUID -> Handle: {handle}");
    }

    [SkippableFact]
    public void ResolverFindsNamesViaUuid()
    {
        Skip.If(!File.Exists(AmpPak), "AMP PAK not found");

        // MAG_Neck14 has RootTemplate "49456989-f123-4db8-8bb3-4ea38df29556" (in merged)
        // AMP_Boots_InterruptMastery has RootTemplate "003d3cfb-..." (individual)
        var uuidMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase)
        {
            ["003d3cfb-f2e6-4c23-a299-4177da45bdd8"] = new() { "AMP_Boots_InterruptMastery" },
            ["49456989-f123-4db8-8bb3-4ea38df29556"] = new() { "MAG_Neck14", "MAG_Neck14_1" },
        };

        var names = ItemNameResolver.ResolveFromPak(AmpPak, uuidMap, "en");

        _o.WriteLine($"Resolved {names.Count} UUIDs:");
        foreach (var (uuid, name) in names)
            _o.WriteLine($"  {uuid} -> {name}");

        Assert.True(names.Count >= 1, "Expected at least one resolved name");
    }

    [SkippableFact]
    public void LocaReaderParsesXml()
    {
        Skip.If(!File.Exists(AmpPak), "AMP PAK not found");

        using var fs = File.OpenRead(AmpPak);
        var header = Core.PakReader.ReadHeader(fs);
        var entries = Core.PakReader.ReadFileList(fs, header);

        var locaEntry = entries.FirstOrDefault(e =>
            e.Path.Contains("English", StringComparison.OrdinalIgnoreCase) &&
            e.Path.EndsWith(".loca.xml", StringComparison.OrdinalIgnoreCase));
        Skip.If(locaEntry.Path == null, "No English loca found");

        var data = Core.PakReader.ExtractFileData(fs, locaEntry);
        var parsed = LocaReader.ParseXml(data);

        _o.WriteLine($"Parsed {parsed.Count} entries from {locaEntry.Path}");
        Assert.True(parsed.Count > 0);
    }
}
