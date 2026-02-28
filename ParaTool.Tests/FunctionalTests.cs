using Xunit;
using Xunit.Abstractions;
using ParaTool.Core.Services;
using ParaTool.Core.Parsing;

namespace ParaTool.Tests;

/// <summary>
/// Functional tests that run against real BG3 Mods folder.
/// These are skipped if the Mods folder doesn't exist.
/// </summary>
public class FunctionalTests
{
    private readonly ITestOutputHelper _output;

    private static readonly string? ModsPath = FindModsFolder();

    public FunctionalTests(ITestOutputHelper output)
    {
        _output = output;
    }

    private static string? FindModsFolder()
    {
        // Try standard Windows path via WSL
        var candidate = "/mnt/c/Users/user/AppData/Local/Larian Studios/Baldur's Gate 3/Mods";
        if (Directory.Exists(candidate)) return candidate;

        // Try detector
        return ModsFolderDetector.Detect();
    }

    [SkippableFact]
    public void VanillaDatabase_LoadsAllEntries()
    {
        var db = new VanillaDatabase();
        db.Load();

        var count = db.Resolver.AllEntries.Count;
        _output.WriteLine($"Vanilla entries loaded: {count}");

        Assert.True(count > 500, $"Expected >500 vanilla entries, got {count}");
    }

    [SkippableFact]
    public async Task Scanner_FindsModsAndAmp()
    {
        Skip.If(ModsPath == null, "BG3 Mods folder not found");

        var db = new VanillaDatabase();
        db.Load();

        var scanner = new ModScanner(db);
        var result = await scanner.ScanAsync(ModsPath!);

        _output.WriteLine($"AMP pak: {result.AmpPakPath}");
        _output.WriteLine($"Error: {result.Error}");
        _output.WriteLine($"Mods with items: {result.Mods.Count}");

        foreach (var mod in result.Mods)
        {
            _output.WriteLine($"  [{mod.Name}] UUID={mod.UUID} Items={mod.Items.Count}");
            foreach (var item in mod.Items.Take(5))
            {
                _output.WriteLine($"    - {item.StatId}: Pool={item.DetectedPool}, Rarity={item.DetectedRarity}");
            }
            if (mod.Items.Count > 5)
                _output.WriteLine($"    ... and {mod.Items.Count - 5} more");
        }

        // Should find AMP or report error about it
        if (result.Error != null)
            _output.WriteLine($"Scan error (expected if no AMP): {result.Error}");
    }

    [SkippableFact]
    public void PakReader_CanReadParamagicPak()
    {
        var pakPath = "/mnt/f/Github/BG3-Vanilla-Files01/Paramagic_bd03b5f3-51bc-cd38-6c0e-d6143c69417f.pak";
        Skip.If(!File.Exists(pakPath), "Paramagic pak not found");

        using var fs = File.OpenRead(pakPath);
        var header = Core.PakReader.ReadHeader(fs);
        var entries = Core.PakReader.ReadFileList(fs, header);

        _output.WriteLine($"LSPK v{header.Version}, {entries.Count} files");

        // Find meta.lsx
        var meta = entries.FirstOrDefault(e => e.Path.EndsWith("meta.lsx", StringComparison.OrdinalIgnoreCase));
        Assert.True(meta.Path != null, "meta.lsx not found in pak");
        _output.WriteLine($"meta.lsx: {meta.Path}");

        var metaData = Core.PakReader.ExtractFileData(fs, meta);
        var modInfo = MetaLsxParser.Parse(metaData, pakPath);
        Assert.NotNull(modInfo);
        _output.WriteLine($"Mod: {modInfo.Name}, UUID={modInfo.UUID}, Folder={modInfo.Folder}");

        // Find stats files
        var statFiles = entries.Where(e =>
            e.Path.Contains("Stats/Generated/Data", StringComparison.OrdinalIgnoreCase) &&
            e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToList();

        _output.WriteLine($"Stats files: {statFiles.Count}");
        foreach (var sf in statFiles)
        {
            var data = Core.PakReader.ExtractFileData(fs, sf);
            var text = System.Text.Encoding.UTF8.GetString(data);
            var parsed = StatsParser.Parse(text);
            _output.WriteLine($"  {sf.Path}: {parsed.Count} entries");
        }
    }
}
