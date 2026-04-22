using ParaTool.Core.Artifacts;
using ParaTool.Core.Services;

namespace ParaTool.App;

/// <summary>
/// Headless diagnostic runner. Invoked from Program.Main when CLI args contain
/// --diag-all or --diag &lt;statId&gt;. Performs the same scan the UI does, then
/// dumps per-item JSON snapshots to %LocalAppData%/ParaTool/diag/ and exits.
///
/// Usage (from dev):
///   ParaTool.App.exe --diag-all
///   ParaTool.App.exe --diag MAG_Weapon26_4
///   ParaTool.App.exe --diag MAG_Weapon26_4,MAG_Neck16_2
/// </summary>
internal static class DiagMode
{
    public static async Task<int> RunAsync(string[] args)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        Console.WriteLine("ParaTool diagnostic mode");

        // Parse args
        var diagAll = args.Contains("--diag-all", StringComparer.OrdinalIgnoreCase);
        var diagStatIds = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--diag", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                foreach (var id in args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    diagStatIds.Add(id);
            }
        }

        var modsPath = ModsFolderDetector.Detect();
        if (modsPath == null)
        {
            Console.Error.WriteLine("ERROR: Mods folder not found.");
            return 2;
        }
        Console.WriteLine($"Mods folder: {modsPath}");

        var step = System.Diagnostics.Stopwatch.StartNew();
        var vanillaDb = new VanillaDatabase();
        vanillaDb.Load();
        Console.WriteLine($"  vanilla DB loaded in {step.ElapsedMilliseconds}ms");

        step.Restart();
        var scanner = new ModScanner(vanillaDb);
        var result = await scanner.ScanAsync(modsPath, "en");
        if (result.Error != null)
        {
            Console.Error.WriteLine($"SCAN ERROR: {result.Error}");
            return 3;
        }
        Console.WriteLine($"  mod scan done in {step.ElapsedMilliseconds}ms ({result.Mods.Count + (result.AmpMod != null ? 1 : 0)} mods, {result.PakPaths.Length} paks, {result.Resolver.AllEntries.Count} stats entries)");

        var resolver = result.Resolver;
        var locaService = new LocaService(result.PakPaths);
        locaService.SeedCache("en", result.LocaMap);
        if (result.HandleOwnership.Count > 0)
            locaService.SetHandleOwnership(result.HandleOwnership);

        step.Restart();
        foreach (var lang in new[] { "en", "ru" })
            try { locaService.GetLocaMap(lang); } catch { }
        Console.WriteLine($"  extra langs loaded in {step.ElapsedMilliseconds}ms");

        // Collect target items
        var allSavedArtifacts = ArtifactStore.LoadAll();
        var artifactByStatId = allSavedArtifacts.ToDictionary(a => a.StatId, StringComparer.OrdinalIgnoreCase);

        // Build StatId -> ItemEntry map (from all mods) so we can dump ItemEntry-level fields
        var itemEntryByStatId = new Dictionary<string, ParaTool.Core.Models.ItemEntry>(StringComparer.OrdinalIgnoreCase);
        if (result.AmpMod != null)
            foreach (var it in result.AmpMod.Items) itemEntryByStatId[it.StatId] = it;
        foreach (var mod in result.Mods)
            foreach (var it in mod.Items) itemEntryByStatId[it.StatId] = it;

        var targetStatIds = new List<string>();
        if (diagAll)
        {
            foreach (var id in resolver.AllEntries.Keys) targetStatIds.Add(id);
            // Also include saved artifacts whose StatId isn't in the resolver
            // (e.g. freshly-created AMP_X artifacts that haven't been patched yet)
            foreach (var a in allSavedArtifacts)
                if (!resolver.AllEntries.ContainsKey(a.StatId))
                    targetStatIds.Add(a.StatId);
        }
        else if (diagStatIds.Count > 0)
        {
            targetStatIds.AddRange(diagStatIds);
        }
        else
        {
            // Default: dump all saved artifacts
            foreach (var a in allSavedArtifacts) targetStatIds.Add(a.StatId);
            Console.WriteLine($"(no --diag / --diag-all flag: dumping all {targetStatIds.Count} saved artifacts only)");
        }

        // Clear previous full dumps if doing full sweep
        if (diagAll)
        {
            var dir = ItemDiagnostics.DiagDir;
            foreach (var f in Directory.GetFiles(dir, "*.json")) File.Delete(f);
        }

        // Dump each
        Console.WriteLine($"Dumping {targetStatIds.Count} items to: {ItemDiagnostics.DiagDir}");
        int dumped = 0, skipped = 0;
        foreach (var id in targetStatIds)
        {
            try
            {
                artifactByStatId.TryGetValue(id, out var art);
                itemEntryByStatId.TryGetValue(id, out var itemEntry);
                ItemDiagnostics.Dump(id, resolver, locaService, art, itemEntry: itemEntry);
                dumped++;
                if (dumped % 500 == 0) Console.WriteLine($"  ... {dumped}/{targetStatIds.Count}");
            }
            catch (Exception ex)
            {
                skipped++;
                Console.Error.WriteLine($"  {id}: {ex.Message}");
            }
        }

        // Write a summary/index
        var summary = new
        {
            timestamp = DateTime.UtcNow.ToString("O"),
            durationMs = sw.ElapsedMilliseconds,
            modsPath,
            modsCount = result.Mods.Count + (result.AmpMod != null ? 1 : 0),
            pakCount = result.PakPaths.Length,
            resolverEntries = resolver.AllEntries.Count,
            savedArtifactsCount = allSavedArtifacts.Count,
            savedArtifactStatIds = allSavedArtifacts.Select(a => a.StatId).ToArray(),
            dumpedItems = dumped,
            skippedItems = skipped,
            diagDir = ItemDiagnostics.DiagDir,
        };
        var summaryPath = Path.Combine(ItemDiagnostics.DiagDir, "_summary.json");
        File.WriteAllText(summaryPath,
            System.Text.Json.JsonSerializer.Serialize(summary, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        Console.WriteLine($"Done in {sw.Elapsed.TotalSeconds:F1}s. Summary: {summaryPath}");
        return 0;
    }
}
