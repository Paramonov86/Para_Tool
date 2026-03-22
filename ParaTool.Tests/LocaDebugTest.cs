using Xunit;
using Xunit.Abstractions;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;

namespace ParaTool.Tests;

public class LocaDebugTest
{
    private readonly ITestOutputHelper _o;
    public LocaDebugTest(ITestOutputHelper o) => _o = o;

    [Fact]
    public void FullPipelineForMergedItems()
    {
        var ampPak = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            @"Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak");
        if (!File.Exists(ampPak)) return;

        // Build resolver like ResolveDisplayNames does
        var db = new VanillaDatabase(); db.Load();
        var resolver = new StatsResolver();
        foreach (var kvp in db.Resolver.AllEntries)
            resolver.AddEntries(new[] { kvp.Value });

        using var fs = File.OpenRead(ampPak);
        var header = Core.PakReader.ReadHeader(fs);
        var entries = Core.PakReader.ReadFileList(fs, header);

        foreach (var sf in entries.Where(e =>
            e.Path.Contains("/Stats/Generated/Data/") && e.Path.EndsWith(".txt")))
        {
            var data = Core.PakReader.ExtractFileData(fs, sf);
            var parsed = StatsParser.Parse(System.Text.Encoding.UTF8.GetString(data));
            // Safe add: don't let StatusData overwrite Armor/Weapon
            foreach (var pe in parsed)
            {
                var existing = resolver.Get(pe.Name);
                if (existing != null && (existing.Type == "Armor" || existing.Type == "Weapon")
                    && pe.Type != "Armor" && pe.Type != "Weapon")
                    continue;
                resolver.AddEntries(new[] { pe });
            }
        }

        // Debug: check WPN_Spear_u directly
        var spearEntry = resolver.Get("WPN_Spear_u");
        if (spearEntry != null)
        {
            _o.WriteLine($"WPN_Spear_u in resolver: Type={spearEntry.Type} Using={spearEntry.Using}");
            _o.WriteLine($"  Data keys: {string.Join(", ", spearEntry.Data.Keys)}");
            _o.WriteLine($"  Has RootTemplate: {spearEntry.Data.ContainsKey("RootTemplate")}");
            if (spearEntry.Data.TryGetValue("RootTemplate", out var rt))
                _o.WriteLine($"  RootTemplate: '{rt}'");
        }
        else
            _o.WriteLine("WPN_Spear_u NOT in resolver!");

        var spearVanilla = db.Resolver.Get("WPN_Spear");
        if (spearVanilla != null)
        {
            _o.WriteLine($"WPN_Spear (vanilla): Type={spearVanilla.Type} Using={spearVanilla.Using}");
            _o.WriteLine($"  Has RootTemplate: {spearVanilla.Data.ContainsKey("RootTemplate")}");
        }

        // Test items
        var testItems = new[] { "WPN_Spear_u", "MAG_Neck1", "MAG_Neck14_2", "MAG_Neck14_3" };
        var uuidMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var statId in testItems)
        {
            var current = statId;
            int depth = 0;
            string? foundUuid = null;
            while (current != null && depth < 20)
            {
                var entry = resolver.Get(current);
                if (entry != null && entry.Data.TryGetValue("RootTemplate", out var uuid) && !string.IsNullOrEmpty(uuid))
                {
                    foundUuid = uuid;
                    break;
                }
                current = entry?.Using;
                depth++;
            }
            _o.WriteLine($"{statId}: UUID={foundUuid ?? "NONE"} (chain depth={depth})");
            if (foundUuid != null)
            {
                if (!uuidMap.ContainsKey(foundUuid))
                    uuidMap[foundUuid] = new();
                uuidMap[foundUuid].Add(statId);
            }
        }

        _o.WriteLine($"\nUUID map: {uuidMap.Count} unique UUIDs");
        foreach (var (uuid, sids) in uuidMap)
            _o.WriteLine($"  {uuid} -> [{string.Join(", ", sids)}]");

        // Call ResolveFromPak
        fs.Position = 0;
        var resolved = ItemNameResolver.ResolveFromPak(ampPak, uuidMap, "en");
        _o.WriteLine($"\nResolveFromPak: {resolved.Count} resolved");
        foreach (var (uuid, name) in resolved)
            _o.WriteLine($"  {uuid} -> {name}");

        // Check: which UUIDs are NOT resolved?
        foreach (var uuid in uuidMap.Keys)
        {
            if (!resolved.ContainsKey(uuid))
                _o.WriteLine($"  MISSING: {uuid}");
        }
    }
}
