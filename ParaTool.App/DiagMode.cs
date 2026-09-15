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
        var diagUuids = new List<string>();
        for (int i = 0; i < args.Length; i++)
        {
            if (string.Equals(args[i], "--diag", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                foreach (var id in args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    diagStatIds.Add(id);
            }
            if (string.Equals(args[i], "--diag-uuid", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                foreach (var id in args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                    diagUuids.Add(id);
            }
        }

        // --diag-mods <dir>: scan (and, with --diag-patch, patch) a copy instead of the real Mods folder.
        var modsPath = args.SkipWhile(a => !a.Equals("--diag-mods", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault()
            ?? ModsFolderDetector.Detect();
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

        // Build StatId -> ItemEntry map. AMP wins on collisions — mirror scanner's
        // authoritative-for-AMP priority. Use TryAdd so first (AMP) stays.
        var itemEntryByStatId = new Dictionary<string, ParaTool.Core.Models.ItemEntry>(StringComparer.OrdinalIgnoreCase);
        if (result.AmpMod != null)
            foreach (var it in result.AmpMod.Items) itemEntryByStatId.TryAdd(it.StatId, it);
        foreach (var mod in result.Mods)
            foreach (var it in mod.Items) itemEntryByStatId.TryAdd(it.StatId, it);

        // --diag-perf: build the same ModVM/ItemVM graph the patcher builds, then time
        // ApplyFilters/ApplySort the way typing in the search box drives them. Reports
        // wall time + allocated bytes per pass so filter regressions are measurable.
        if (args.Contains("--diag-perf", StringComparer.OrdinalIgnoreCase))
        {
            RunFilterPerf(result, locaService);
            return 0;
        }

        // --diag-spell-ui: lay out the real Constructor with spell cards, a creature card and a
        // passive-granted spell expanded; reports realized containers and binding errors.
        if (args.Contains("--diag-spell-ui", StringComparer.OrdinalIgnoreCase))
            return RunSpellUiSmoke(result, locaService);

        // --diag-patch: uncheck one AMP item that the last submod's own TreasureTable restates,
        // run the real patcher and check the result lands in exactly one pak. Destructive for
        // the scanned folder — only use it with --diag-mods pointing at a copy.
        if (args.Contains("--diag-patch", StringComparer.OrdinalIgnoreCase))
            return await RunPatchProbeAsync(result, vanillaDb, modsPath);

        // --diag-templates: dump full LSF-aware template metadata for templates whose
        // Stats attribute matches a substring pattern (across every scanned pak).
        // Great for confirming whether the "all cloaks share one handle" is an LSF
        // extraction bug or genuine data duplication. Usage: --diag-templates MAG_Cloak
        var diagTemplates = args.SkipWhile(a => !a.Equals("--diag-templates", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagTemplates))
        {
            foreach (var pak in result.PakPaths)
            {
                using var fs = File.OpenRead(pak);
                var header = ParaTool.Core.PakReader.ReadHeader(fs);
                var entries = ParaTool.Core.PakReader.ReadFileList(fs, header);
                foreach (var entry in entries.Where(e =>
                    (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
                    (e.Path.Contains("RootTemplate", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase))))
                {
                    byte[] data;
                    try { data = ParaTool.Core.PakReader.ExtractFileData(fs, entry); } catch { continue; }
                    var meta = RootTemplateIconExtractor.ExtractFullMetadata(data);
                    foreach (var (uuid, m) in meta)
                    {
                        if (m.stats == null || !m.stats.Contains(diagTemplates, StringComparison.OrdinalIgnoreCase))
                            continue;
                        Console.WriteLine($"{Path.GetFileName(pak)}/{Path.GetFileName(entry.Path)}");
                        Console.WriteLine($"  UUID: {uuid}");
                        Console.WriteLine($"  Stats: {m.stats}");
                        Console.WriteLine($"  nameHandle: {m.nameHandle}");
                        Console.WriteLine($"  descHandle: {m.descHandle}");
                        Console.WriteLine($"  parent: {m.parent}");
                        Console.WriteLine($"  icon: {m.icon}");
                    }
                }
            }
            return 0;
        }

        // --diag-template-node: fully parse a RootTemplate GameObject by UUID and dump its
        // complete attribute set + child-node tree. Settles whether equip-slot info lives on
        // the template (Equipment child, Slot attr) or purely in the stats entry.
        var diagTemplateNode = args.SkipWhile(a => !a.Equals("--diag-template-node", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagTemplateNode))
        {
            foreach (var pak in result.PakPaths)
            {
                using var fs = File.OpenRead(pak);
                var header = ParaTool.Core.PakReader.ReadHeader(fs);
                var entries = ParaTool.Core.PakReader.ReadFileList(fs, header);
                foreach (var entry in entries.Where(e =>
                    e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) &&
                    (e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase))))
                {
                    byte[] data;
                    try { data = ParaTool.Core.PakReader.ExtractFileData(fs, entry); } catch { continue; }
                    ParaTool.Core.LSLib.Resource res;
                    try { using var ms = new MemoryStream(data); var rdr = new ParaTool.Core.LSLib.LSFReader(ms); res = rdr.Read(); }
                    catch { continue; }
                    if (!res.Regions.TryGetValue("Templates", out var region)) continue;
                    if (!region.Children.TryGetValue("GameObjects", out var gos)) continue;
                    var match = gos.FirstOrDefault(n =>
                        n.Attributes.TryGetValue("MapKey", out var mk) &&
                        diagTemplateNode.Equals(mk.Value?.ToString(), StringComparison.OrdinalIgnoreCase));
                    if (match == null) continue;
                    Console.WriteLine($"=== {Path.GetFileName(pak)}/{entry.Path} ===");
                    DumpNode(match, 0);
                    return 0;
                }
            }
            Console.WriteLine($"Template {diagTemplateNode} not found in any scanned pak.");
            return 0;
        }

        // --diag-resolve-uuid: call ItemNameResolver.ResolveFromPakFull on every pak for a UUID
        var diagResolveUuid = args.SkipWhile(a => !a.Equals("--diag-resolve-uuid", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagResolveUuid))
        {
            var uuidMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase) { [diagResolveUuid] = ["__test"] };
            foreach (var pak in result.PakPaths)
            {
                var (n, dsc, nh, dh) = ItemNameResolver.ResolveFromPakFull(pak, uuidMap, "en");
                if (n.Count > 0 || nh.Count > 0 || dh.Count > 0)
                {
                    n.TryGetValue(diagResolveUuid, out var nameVal);
                    dsc.TryGetValue(diagResolveUuid, out var descVal);
                    nh.TryGetValue(diagResolveUuid, out var nameHnd);
                    dh.TryGetValue(diagResolveUuid, out var descHnd);
                    Console.WriteLine($"{Path.GetFileName(pak)}:");
                    Console.WriteLine($"  name={nameVal} nameHandle={nameHnd}");
                    Console.WriteLine($"  desc={descVal} descHandle={descHnd}");
                }
            }
            return 0;
        }

        // --diag-parent: show template parent chain for a UUID across all paks
        var diagParent = args.SkipWhile(a => !a.Equals("--diag-parent", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagParent))
        {
            var graph = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pakPath in result.PakPaths)
            {
                var parents = ItemNameResolver.GatherTemplateParents(pakPath);
                foreach (var (k, v) in parents) graph.TryAdd(k, v);
            }
            Console.WriteLine($"Graph size: {graph.Count}");
            var cur = diagParent;
            int depth = 0;
            while (depth < 10)
            {
                Console.WriteLine($"  [{depth}] {cur}");
                if (!graph.TryGetValue(cur, out var parent)) { Console.WriteLine($"  (no parent)"); break; }
                cur = parent;
                depth++;
            }
            return 0;
        }

        // --diag-art: decrypt a saved .art file (by ArtifactId or "latest") and dump its
        // full JSON contents to stdout. Useful for inspecting what the Constructor
        // actually persisted without digging through the encrypted blob by hand.
        var diagArt = args.SkipWhile(a => !a.Equals("--diag-art", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagArt))
        {
            var dir = ArtifactStore.GetArtifactsDir();
            string artifactId = diagArt;
            if (diagArt.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                var latest = new DirectoryInfo(dir).EnumerateFiles("*.art")
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (latest == null) { Console.Error.WriteLine("No .art files found."); return 4; }
                artifactId = Path.GetFileNameWithoutExtension(latest.Name);
                Console.WriteLine($"Latest artifact: {latest.Name} (modified {latest.LastWriteTime:yyyy-MM-dd HH:mm:ss})");
            }
            var art = ArtifactStore.Load(artifactId);
            if (art == null) { Console.Error.WriteLine($"Failed to load .art: {artifactId}"); return 4; }
            var json = System.Text.Json.JsonSerializer.Serialize(art,
                new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
            Console.WriteLine(json);
            return 0;
        }

        // --diag-compile: compile a saved .art (by ArtifactId or "latest") the same way the
        // patcher does — resolver = all scanned pak stats + vanilla (vanilla added LAST so
        // canonical bases win over self-referential mod overrides). Prints the resolved
        // identity fields and the full stat block. Used to confirm equipment-type identity
        // (e.g. shield `Shield "Yes"`) is now emitted explicitly rather than left to inheritance.
        var diagCompile = args.SkipWhile(a => !a.Equals("--diag-compile", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagCompile))
        {
            var dir = ArtifactStore.GetArtifactsDir();
            string artifactId = diagCompile;
            if (diagCompile.Equals("latest", StringComparison.OrdinalIgnoreCase))
            {
                var latest = new DirectoryInfo(dir).EnumerateFiles("*.art")
                    .OrderByDescending(f => f.LastWriteTime).FirstOrDefault();
                if (latest == null) { Console.Error.WriteLine("No .art files found."); return 4; }
                artifactId = Path.GetFileNameWithoutExtension(latest.Name);
                Console.WriteLine($"Latest artifact: {latest.Name}");
            }
            var art = ArtifactStore.Load(artifactId);
            if (art == null) { Console.Error.WriteLine($"Failed to load .art: {artifactId}"); return 4; }

            var compileResolver = new ParaTool.Core.Parsing.StatsResolver();
            foreach (var pak in result.PakPaths)
            {
                using var fs = File.OpenRead(pak);
                var header = ParaTool.Core.PakReader.ReadHeader(fs);
                var entries = ParaTool.Core.PakReader.ReadFileList(fs, header);
                foreach (var e in entries.Where(e =>
                    e.Path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase) &&
                    e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                {
                    byte[] data;
                    try { data = ParaTool.Core.PakReader.ExtractFileData(fs, e); } catch { continue; }
                    try { compileResolver.AddEntries(ParaTool.Core.Parsing.StatsParser.Parse(System.Text.Encoding.UTF8.GetString(data))); } catch { }
                }
            }
            compileResolver.AddEntries(vanillaDb.Resolver.AllEntries.Values);

            bool isOverride = art.StatId.Equals(art.UsingBase, StringComparison.OrdinalIgnoreCase);
            var baseFields = compileResolver.ResolveAll(art.UsingBase);
            Console.WriteLine($"== ResolveAll('{art.UsingBase}') identity fields ==");
            foreach (var k in new[] { "Slot", "Shield", "Proficiency Group", "Armor Class Ability",
                "Weapon Group", "Weapon Properties", "Damage Type", "Damage", "WeaponRange", "VersatileDamage", "Projectile" })
                if (baseFields.TryGetValue(k, out var v)) Console.WriteLine($"  {k} = \"{v}\"");
            Console.WriteLine();

            var compiled = ArtifactCompiler.Compile(art, isOverride, compileResolver);
            Console.WriteLine("== Compiled stat block ==");
            Console.WriteLine(compiled.StatsText);
            return 0;
        }

        // --diag-handle: resolve a specific loca handle via LocaService (sanity check)
        var diagHandle = args.SkipWhile(a => !a.Equals("--diag-handle", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagHandle))
        {
            Console.WriteLine($"Handle: {diagHandle}");
            foreach (var lang in new[] { "en", "ru" })
            {
                var text = locaService.ResolveHandle(diagHandle, lang);
                Console.WriteLine($"  {lang}: {text ?? "(null)"}");
            }
            return 0;
        }

        // --diag-build: simulate BuildArtifactFromBase and dump resulting DisplayName dict
        var diagBuild = args.SkipWhile(a => !a.Equals("--diag-build", StringComparison.OrdinalIgnoreCase)).Skip(1).FirstOrDefault();
        if (!string.IsNullOrEmpty(diagBuild))
        {
            var locaResolver = new ParaTool.Core.Services.LocaResolver(resolver, locaService);
            var itemEntry = itemEntryByStatId.GetValueOrDefault(diagBuild);
            var simulated = new Dictionary<string, object?>
            {
                ["statId"] = diagBuild,
                ["itemEntryFound"] = itemEntry != null,
                ["displayNameHandle"] = itemEntry?.DisplayNameHandle,
                ["descriptionHandle"] = itemEntry?.DescriptionHandle,
                ["nameViaResolver"] = new Dictionary<string, object>(),
                ["descViaResolver"] = new Dictionary<string, object>(),
                ["handleResolveDirect"] = new Dictionary<string, object>(),
            };
            foreach (var lang in new[] { "en", "ru" })
            {
                var nr = locaResolver.ResolveName(diagBuild, lang, null, itemEntry?.DisplayNameHandle);
                var dr = locaResolver.ResolveDescription(diagBuild, lang, null, itemEntry?.DescriptionHandle);
                ((Dictionary<string, object>)simulated["nameViaResolver"]!)[lang] = new { value = nr.Value, source = nr.Source.ToString(), matched = nr.MatchedAt, depth = nr.Depth };
                ((Dictionary<string, object>)simulated["descViaResolver"]!)[lang] = new { value = dr.Value, source = dr.Source.ToString(), matched = dr.MatchedAt, depth = dr.Depth };
                if (!string.IsNullOrEmpty(itemEntry?.DisplayNameHandle))
                {
                    var direct = locaService.ResolveHandle(itemEntry.DisplayNameHandle, lang);
                    ((Dictionary<string, object>)simulated["handleResolveDirect"]!)[lang] = new { resolved = direct };
                }
            }
            var outFile = Path.Combine(ItemDiagnostics.DiagDir, $"build_{diagBuild}.json");
            File.WriteAllText(outFile, System.Text.Json.JsonSerializer.Serialize(simulated, new System.Text.Json.JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));
            Console.WriteLine($"Build simulation dumped: {outFile}");
            return 0;
        }

        // --diag-uuid: find template by UUID in every pak, dump what's inside
        if (diagUuids.Count > 0)
        {
            foreach (var uuid in diagUuids)
            {
                var uuidResult = TemplateFinder.FindUuid(uuid, result.PakPaths);
                var outPath = Path.Combine(ItemDiagnostics.DiagDir, $"uuid_{uuid}.json");
                File.WriteAllText(outPath, System.Text.Json.JsonSerializer.Serialize(uuidResult,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"UUID {uuid} → found in {uuidResult.Count} paks. Dumped: {outPath}");
            }
            return 0;
        }

        // Collect target items
        var allSavedArtifacts = ArtifactStore.LoadAll();
        var artifactByStatId = allSavedArtifacts.ToDictionary(a => a.StatId, StringComparer.OrdinalIgnoreCase);


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
                // Only run per-pak probe for single-item diag (slow: reads every pak)
                var pakPathsForProbe = diagStatIds.Count > 0 && diagStatIds.Count <= 10 ? result.PakPaths : null;
                ItemDiagnostics.Dump(id, resolver, locaService, art, itemEntry: itemEntry, pakPaths: pakPathsForProbe);
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

    /// <summary>
    /// Benchmark the patcher's filter/sort hot path against the user's real mod set.
    /// Mirrors MainWindowViewModel's VM construction so the numbers match the UI.
    /// </summary>
    private static void RunFilterPerf(ScanResult result, LocaService locaService)
    {
        var vm = new ViewModels.ItemEditorViewModel();
        if (result.AmpMod != null)
            vm.Mods.Add(new ViewModels.ModVM(result.AmpMod, locaService));
        foreach (var mod in result.Mods)
            vm.Mods.Add(new ViewModels.ModVM(mod, locaService));

        var itemCount = vm.Mods.Sum(m => m.Items.Count);
        Console.WriteLine($"\n=== filter perf: {vm.Mods.Count} mods / {itemCount} items ===");

        void Measure(string label, Action action)
        {
            GC.Collect(2, GCCollectionMode.Forced, true, true);
            var before = GC.GetAllocatedBytesForCurrentThread();
            var t = System.Diagnostics.Stopwatch.StartNew();
            action();
            t.Stop();
            var alloc = (GC.GetAllocatedBytesForCurrentThread() - before) / 1024.0 / 1024.0;
            Console.WriteLine($"  {label,-34} {t.Elapsed.TotalMilliseconds,8:F1} ms   alloc {alloc,8:F1} MB");
        }

        // One keystroke = one ApplyFilters pass. Typing "ring" is four of them.
        foreach (var q in new[] { "r", "ri", "rin", "ring" })
            Measure($"ApplyFilters(\"{q}\")", () => vm.SearchText = q);

        Measure("ApplyFilters(\"\") [clear]", () => vm.SearchText = "");
        Measure("read ItemLabel x1 (all items)", () =>
        {
            foreach (var m in vm.Mods) foreach (var i in m.Items) _ = i.ItemLabel;
        });
        Measure("SearchableText scan only", () =>
        {
            foreach (var m in vm.Mods)
                foreach (var i in m.Items)
                    _ = i.Entry.SearchableText?.Contains("ring", StringComparison.OrdinalIgnoreCase) ?? false;
        });

        Measure("ApplySort(Rarity)", () => vm.CurrentSort = ViewModels.SortMode.Rarity);
        Measure("ApplySort(Name)", () => vm.CurrentSort = ViewModels.SortMode.Name);

        var searchBytes = vm.Mods.Sum(m => m.Items.Sum(i => (long)(i.Entry.SearchableText?.Length ?? 0))) * 2;
        Console.WriteLine($"  SearchableText total: {searchBytes / 1024.0 / 1024.0:F1} MB");
        Console.WriteLine($"  Managed heap: {GC.GetTotalMemory(true) / 1024.0 / 1024.0:F1} MB   " +
                          $"WorkingSet: {Environment.WorkingSet / 1024.0 / 1024.0:F1} MB");
        foreach (var m in vm.Mods)
            Console.WriteLine($"    mod {m.Name,-40} {m.Items.Count,6} items");

        RunVisualTreePerf(vm);
    }

    private static async Task<int> RunPatchProbeAsync(ScanResult result, VanillaDatabase vanillaDb, string modsPath)
    {
        Console.WriteLine("\n=== patch probe ===");
        if (result.AmpPakPath == null || result.AmpMod == null)
        {
            Console.Error.WriteLine("  AMP not found");
            return 4;
        }

        static string? PakText(string pak, string fileName)
        {
            using var fs = File.OpenRead(pak);
            var header = ParaTool.Core.PakReader.ReadHeader(fs);
            var entry = ParaTool.Core.PakReader.ReadFileList(fs, header)
                .FirstOrDefault(e => e.Path.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase));
            return entry.Path == null ? null
                : System.Text.Encoding.UTF8.GetString(ParaTool.Core.PakReader.ExtractFileData(fs, entry));
        }
        static string TableBlock(string tt, string name)
        {
            var start = tt.IndexOf($"new treasuretable \"{name}\"", StringComparison.Ordinal);
            if (start < 0) return "";
            var next = tt.IndexOf("new treasuretable \"", start + 1, StringComparison.Ordinal);
            return next < 0 ? tt[start..] : tt[start..next];
        }
        static int Count(string text, string needle) => text.Split(needle).Length - 1;
        static string Hash(string path) =>
            Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(path)))[..16];

        var target = ParaTool.Core.Patching.AmpPatcher.SelectPatchTarget(result.Mods);
        Console.WriteLine($"  target: {target?.Name ?? "AMP"}");

        // With PARATOOL_STORAGE_DIR pointing at a scratch folder, patch a throwaway ring that
        // carries spell cards and show what reaches the written pak.
        var spellProbe = !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PARATOOL_STORAGE_DIR"))
            ? SaveSpellProbeArtifact(result) : null;
        if (target == null && spellProbe == null) return 0;

        var targetTtBefore = target == null ? ""
            : PakText(ParaTool.Core.Services.PakSource.Resolve(target.PakPath), "TreasureTable.txt") ?? "";
        var restated = TableBlock(targetTtBefore, "AMP_Para_14");
        var victim = target == null ? null : result.AmpMod.Items.FirstOrDefault(i => restated.Contains($"\"I_{i.StatId}\""));
        if (target != null && victim == null)
        {
            Console.Error.WriteLine("  no AMP item found in the submod's AMP_Para_14");
            return 5;
        }
        if (victim != null)
        {
            victim.Enabled = false;
            Console.WriteLine($"  unchecked: {victim.StatId} (in submod AMP_Para_14: {restated.Length > 0})");
        }

        var ampHash = Hash(result.AmpPakPath);
        var itemsBefore = result.Mods.Sum(m => m.Items.Count) + result.AmpMod.Items.Count;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var patch = await new ParaTool.Core.Patching.AmpPatcher().PatchAsync(result.AmpPakPath, result.Mods, result.AmpMod);
        Console.WriteLine($"  patch: success={patch.Success} target={patch.TargetName} items={patch.ItemsPatched} " +
                          $"warnings={patch.Warnings.Count} error={patch.Error} ({sw.ElapsedMilliseconds}ms)");
        foreach (var w in patch.Warnings.Take(5)) Console.WriteLine($"    warn: {w}");

        Console.WriteLine($"  AMP pak unchanged: {Hash(result.AmpPakPath) == ampHash}");
        if (target != null && victim != null)
        {
            var liveTt = PakText(target.PakPath, "TreasureTable.txt") ?? "";
            Console.WriteLine($"  AMP_Para_14 tables in submod: {Count(liveTt, "new treasuretable \"AMP_Para_14\"")}");
            Console.WriteLine($"  victim still in submod AMP_Para_14: {TableBlock(liveTt, "AMP_Para_14").Contains($"\"I_{victim.StatId}\"")}");
            Console.WriteLine($"  tables in submod TT: {Count(targetTtBefore, "new treasuretable ")} -> {Count(liveTt, "new treasuretable ")}");
            Console.WriteLine($"  marker in submod: {ParaTool.Core.Services.AmpBackupService.IsPatchedPak(target.PakPath)}; " +
                              $"marker in AMP: {ParaTool.Core.Services.AmpBackupService.IsPatchedPak(result.AmpPakPath)}");
            Console.WriteLine($"  scanner reads submod from backup: " +
                              $"{ParaTool.Core.Services.PakSource.Resolve(target.PakPath) == ParaTool.Core.Services.AmpBackupService.GetBackupPath(target.PakPath)}");
        }

        if (spellProbe != null)
        {
            Console.WriteLine("\n  --- spell cards in written pak ---");
            var written = target != null ? target.PakPath : result.AmpPakPath;
            var entries = StatsOfPak(written);
            foreach (var name in spellProbe)
            {
                var decls = entries.Where(e => e.entry.Name.Equals(name, StringComparison.OrdinalIgnoreCase)).ToList();
                Console.WriteLine($"  {name}: {decls.Count} declaration(s)");
                foreach (var (file, e) in decls)
                    Console.WriteLine($"    {file}: using={e.Using ?? "-"} " +
                                      string.Join(" ", new[] { "SpellType", "Cooldown", "SpellSuccess", "SpellProperties", "Vitality", "DisplayName", "Boosts" }
                                          .Where(e.Data.ContainsKey).Select(k => $"{k}='{e.Data[k]}'")));
            }
            if (_probeCreatureTemplate != null)
            {
                using var pfs = File.OpenRead(written);
                var files = ParaTool.Core.PakReader.ReadFileList(pfs, ParaTool.Core.PakReader.ReadHeader(pfs));
                var lsf = files.FirstOrDefault(f => f.Path.EndsWith($"/{_probeCreatureTemplate}.lsf", StringComparison.OrdinalIgnoreCase));
                if (lsf.Path == null) Console.WriteLine($"  creature template {_probeCreatureTemplate}.lsf: MISSING");
                else
                {
                    using var ms = new MemoryStream(ParaTool.Core.PakReader.ExtractFileData(pfs, lsf));
                    var node = new ParaTool.Core.LSLib.LSFReader(ms).Read().Regions["Templates"].Children["GameObjects"][0];
                    Console.WriteLine($"  creature template {lsf.Path}: " + string.Join(" ",
                        new[] { "Type", "Stats", "ParentTemplateId" }.Select(k => $"{k}={node.Attributes.GetValueOrDefault(k)?.Value}")));
                }
            }
        }

        var rescan = await new ModScanner(vanillaDb).ScanAsync(modsPath, "en");
        var itemsAfter = rescan.Mods.Sum(m => m.Items.Count) + (rescan.AmpMod?.Items.Count ?? 0);
        Console.WriteLine($"  items scanned before/after patch: {itemsBefore} / {itemsAfter}");
        return 0;
    }

    private sealed class BindingErrorSink : Avalonia.Logging.ILogSink
    {
        public readonly List<string> Errors = [];
        public bool IsEnabled(Avalonia.Logging.LogEventLevel level, string area) =>
            level >= Avalonia.Logging.LogEventLevel.Warning && area == Avalonia.Logging.LogArea.Binding;
        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate) =>
            Errors.Add(messageTemplate);
        public void Log(Avalonia.Logging.LogEventLevel level, string area, object? source, string messageTemplate, params object?[] propertyValues) =>
            Errors.Add(messageTemplate + " | " + string.Join(", ", propertyValues));
    }

    private static int RunSpellUiSmoke(ScanResult result, LocaService loca)
    {
        // Russian labels in the layout report; setting Console.OutputEncoding throws on a pipe.
        Console.SetOut(new StreamWriter(Console.OpenStandardOutput(), new System.Text.UTF8Encoding(false)) { AutoFlush = true });
        Console.WriteLine("\n=== spell card UI smoke ===");
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PARATOOL_STORAGE_DIR")))
        {
            Console.Error.WriteLine("  set PARATOOL_STORAGE_DIR to a scratch folder first");
            return 6;
        }
        Program.BuildAvaloniaApp().SetupWithoutStarting();
        var sink = new BindingErrorSink();
        Avalonia.Logging.Logger.Sink = sink;

        return Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
        {
            var resolver = result.Resolver;
            var cvm = new ViewModels.ConstructorViewModel(resolver, loca);
            var baseItem = result.AmpMod!.Items.First(i => i.StatType == "Armor");
            var art = new ParaTool.Core.Artifacts.ArtifactDefinition
            {
                StatId = "PT_UiProbe", StatType = "Armor", UsingBase = baseItem.StatId, LootPool = "Rings",
            };
            var item = new ViewModels.ArtifactItemVM(art) { GetEditingLang = () => "en" };
            cvm.SelectedArtifact = item;

            item.AddExistingSpell("Target_VampiricTouch", resolver, loca);
            item.AddExistingSpell("Projectile_MOO_BalthazarsSecrets_AnimateZombie", resolver, loca);
            foreach (var s in item.SpellVMs) s.IsExpanded = true;
            var creature = item.SpellVMs.SelectMany(s => s.Creatures).FirstOrDefault();
            if (creature != null)
            {
                item.EditCreature(creature, resolver);
                creature.EditVitality = "77";
            }
            item.SetSpellGrantedThroughPassive(item.SpellVMs[0], true);

            // A mod spell that inherits its name and description through `using` (AMP_Kyzr_Shatter_3
            // using Target_Shatter_3) must still show its text on the card.
            if (resolver.AllEntries.ContainsKey("AMP_Kyzr_Shatter_3"))
            {
                item.AddExistingSpell("AMP_Kyzr_Shatter_3", resolver, loca);
                var inherited = art.Spells[^1];
                Console.WriteLine($"  inherited text: en='{inherited.DisplayName.GetValueOrDefault("en")}' ru='{inherited.DisplayName.GetValueOrDefault("ru")}' " +
                                  $"desc en={inherited.Description.GetValueOrDefault("en")?.Length ?? 0} chars, source handle={inherited.SourceDisplayNameHandle}");
                var desc = inherited.Description.GetValueOrDefault("en") ?? "";
                Console.WriteLine($"  inherited desc: raw markup left={desc.Contains("LSTag") || desc.Contains("&lt;")} '{desc}'");
            }

            var view = new Views.ConstructorView { DataContext = cvm };
            var root = new Avalonia.Controls.Window { Width = 1600, Height = 4000, Content = view };
            for (int pass = 0; pass < 10; pass++)
            {
                root.InvalidateMeasure();
                root.Measure(new Avalonia.Size(1600, 4000));
                root.Arrange(new Avalonia.Rect(0, 0, 1600, 4000));
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            }

            var controls = new List<Avalonia.Controls.Control>();
            void Walk(Avalonia.Visual v)
            {
                if (v is Avalonia.Controls.Control c) controls.Add(c);
                foreach (var ch in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(v)) Walk(ch);
            }
            Walk(root);

            int Realized<T>() => controls.OfType<Avalonia.Controls.Presenters.ContentPresenter>().Count(c => c.DataContext is T);
            Console.WriteLine($"  spells={item.SpellVMs.Count} creatures={item.SpellVMs.Sum(s => s.Creatures.Count)} " +
                              $"creature edited={creature?.IsEdited} HP={creature?.EditVitality} name='{creature?.CreatureName}'");
            Console.WriteLine($"  SpellsOnEquip='{art.SpellsOnEquip}' passives={string.Join(",", art.Passives.Select(p => $"{p.Name}:{p.Boosts}"))}");
            Console.WriteLine($"  first card grant via passive={item.SpellVMs[0].GrantThroughPassive}");
            Console.WriteLine($"  realized: spell cards={Realized<ViewModels.SpellVM>()} creature rows={Realized<ViewModels.SummonVM>()} " +
                              $"passive cards={Realized<ViewModels.PassiveVM>()} (controls {controls.Count})");
            Console.WriteLine($"  spell name boxes: {string.Join(" | ", controls.OfType<Avalonia.Controls.TextBox>().Where(t => t.DataContext is ViewModels.SpellVM && t.FontWeight == Avalonia.Media.FontWeight.SemiBold).Select(t => t.Text))}");

            // Max's settings: font +50% in a narrow window, English and Russian. A label wider than
            // its grid column draws over the box next to it, and a chip row that does not wrap runs
            // past its card — report both.
            item.SpellVMs[0].EditSpellProperties =
                "IF(HasStatus('BURNING')):RestoreResource(SELF,ChannelDivinity,1,0);" + item.SpellVMs[0].EditSpellProperties;
            // Cost badges must not write back on load: odd vanilla spellings stay byte for byte.
            art.Spells[^1].UseCosts = "Movement:Distance*0.5; ActionPoint:1\t;SpellSlotsGroup:2:2:4;Weird:1:2";
            var costsBefore = art.Spells.Select(s => s.UseCosts).ToList();
            int costBadges = 0;
            static void ApplyFontScale(double scale)
            {
                Services.FontScale.Factor = scale;
                foreach (var bs in new[] { 10, 11, 12, 13, 14, 16, 18, 20, 22, 24 })
                    Avalonia.Application.Current!.Resources[$"FontSize{bs}"] = Math.Round(bs * scale);
                Services.FontScale.NotifyChanged();
            }
            static IEnumerable<Avalonia.Visual> Descendants(Avalonia.Visual v)
            {
                foreach (var ch in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(v))
                {
                    yield return ch;
                    foreach (var d in Descendants(ch)) yield return d;
                }
            }
            foreach (var (lang, scale, width) in new[] { ("en", 1.0, 1600.0), ("en", 1.5, 1000.0), ("ru", 1.5, 1000.0) })
            {
                Localization.Loc.Instance.SetLanguage(lang);
                ApplyFontScale(scale);
                // A window that is never shown lays out at most ~1000 px wide, so the width comes
                // from a fixed-width host inside it.
                var probeRoot = new Avalonia.Controls.Window
                {
                    Width = 1000, Height = 4000,
                    Content = new Avalonia.Controls.Border
                    {
                        Width = width, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                        Child = new Views.ConstructorView { DataContext = cvm },
                    },
                };
                void Settle()
                {
                    // The window is never shown, so nothing re-arranges containers added under a panel
                    // that was already arranged — invalidate the whole tree on every pass.
                    for (int pass = 0; pass < 10; pass++)
                    {
                        foreach (var l in Descendants(probeRoot).OfType<Avalonia.Layout.Layoutable>()) l.InvalidateMeasure();
                        probeRoot.InvalidateMeasure();
                        probeRoot.Measure(new Avalonia.Size(1000, 4000));
                        probeRoot.Arrange(new Avalonia.Rect(0, 0, 1000, 4000));
                        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    }
                }
                Settle();
                // Loading the view rebuilds the card VMs, so expand the ones it ended up with.
                foreach (var s in item.SpellVMs) s.IsExpanded = true;
                foreach (var p in item.PassiveVMs) p.IsExpanded = true;
                Settle();

                var allCards = Descendants(probeRoot).OfType<Avalonia.Controls.ItemsControl>()
                    .SelectMany(ic => ic.GetRealizedContainers())
                    .Where(c => c.DataContext is ViewModels.SpellVM or ViewModels.PassiveVM)
                    .Distinct().ToList();
                var cards = allCards.Where(c => c.Bounds.Width > 0).ToList();
                var problems = new List<string>();
                foreach (var card in cards)
                foreach (var v in Descendants(card))
                {
                    if (v is not Avalonia.Controls.Control c || !c.IsEffectivelyVisible || c.Bounds.Width <= 0) continue;
                    if (c is not (Avalonia.Controls.TextBlock or Avalonia.Controls.TextBox or Controls.TumblerChipEditor
                        or Avalonia.Controls.Button or Controls.SearchPickerChip)) continue;
                    if (Avalonia.VisualTree.VisualExtensions.FindAncestorOfType<Avalonia.Controls.TextBox>(c) != null) continue;
                    var what = $"{c.GetType().Name} '{(c as Avalonia.Controls.TextBlock)?.Text ?? (c as Controls.TumblerChipEditor)?.Text ?? (c as Avalonia.Controls.TextBox)?.Text}'";
                    var right = Avalonia.VisualExtensions.TranslatePoint(c, new Avalonia.Point(c.Bounds.Width, 0), card)?.X ?? 0;
                    if (right > card.Bounds.Width + 1)
                    {
                        var left = Avalonia.VisualExtensions.TranslatePoint(c, new Avalonia.Point(0, 0), card)?.X ?? 0;
                        int depth = 0;
                        for (var a = Avalonia.VisualTree.VisualExtensions.GetVisualParent(c); a != null && a != card; a = Avalonia.VisualTree.VisualExtensions.GetVisualParent(a))
                            if (a is Controls.BoostBlocksEditor or Controls.ConditionBlocksEditor) depth++;
                        problems.Add($"runs out: {what} left={left:0} right={right:0} card={card.Bounds.Width:0} editors={depth}");
                    }
                    if (c is Avalonia.Controls.TextBlock tb && !string.IsNullOrEmpty(tb.Text)
                        && tb.TextWrapping == Avalonia.Media.TextWrapping.NoWrap && tb.TextTrimming == Avalonia.Media.TextTrimming.None)
                    {
                        var needed = new Avalonia.Media.TextFormatting.TextLayout(tb.Text,
                            new Avalonia.Media.Typeface(tb.FontFamily, tb.FontStyle, tb.FontWeight), tb.FontSize, null).Width;
                        if (needed > tb.Bounds.Width + 1)
                            problems.Add($"label cut: '{tb.Text}' needs {needed:0} has {tb.Bounds.Width:0}");
                    }
                }
                costBadges = Descendants(probeRoot).OfType<Controls.UseCostsEditor>()
                    .Sum(e => Descendants(e).OfType<Controls.TumblerChipEditor>().Count());
                var distinct = problems.Distinct().ToList();
                Console.WriteLine($"  layout {lang} font x{scale} width {width}: cards={cards.Count} problems={distinct.Count}");
                foreach (var p in distinct.Take(20)) Console.WriteLine($"    {p}");
                probeRoot.Content = null;
            }
            Localization.Loc.Instance.SetLanguage("en");
            ApplyFontScale(1.0);
            var costsAfter = art.Spells.Select(s => s.UseCosts).ToList();
            Console.WriteLine($"  UseCosts untouched by layout: {costsBefore.SequenceEqual(costsAfter)} " +
                              $"[{string.Join(" | ", costsAfter.Select(c => c.Replace("\t", "\\t")))}] cost tumblers={costBadges}");

            Console.WriteLine($"  binding errors: {sink.Errors.Count}");
            foreach (var e in sink.Errors.Distinct().Take(15)) Console.WriteLine($"    {e}");
            return 0;
        });
    }

    /// <summary>Every stats entry in a pak's Stats/Generated/Data files, with its file name.</summary>
    private static List<(string file, ParaTool.Core.Parsing.StatsEntry entry)> StatsOfPak(string pak)
    {
        var list = new List<(string, ParaTool.Core.Parsing.StatsEntry)>();
        using var fs = File.OpenRead(pak);
        var header = ParaTool.Core.PakReader.ReadHeader(fs);
        foreach (var e in ParaTool.Core.PakReader.ReadFileList(fs, header))
        {
            if (!e.Path.Contains("Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase)
                || !e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)) continue;
            var text = System.Text.Encoding.UTF8.GetString(ParaTool.Core.PakReader.ExtractFileData(fs, e));
            foreach (var entry in ParaTool.Core.Parsing.StatsParser.Parse(text))
                list.Add((Path.GetFileName(e.Path), entry));
        }
        return list;
    }

    /// <summary>
    /// Saves a ring with three spell cards into the (scratch) artifact store: a copy of a vanilla
    /// spell, an edited original AMP declares itself, and an edited original vanilla spell.
    /// Returns the entry names to look for in the written pak.
    /// </summary>
    private static List<string>? SaveSpellProbeArtifact(ScanResult result)
    {
        var resolver = result.Resolver;
        var ring = result.AmpMod!.Items.FirstOrDefault(i =>
            i.StatType == "Armor" && resolver.Resolve(i.StatId, "Slot") == "Ring"
            && !string.IsNullOrEmpty(resolver.Resolve(i.StatId, "RootTemplate")));
        if (ring == null) { Console.Error.WriteLine("  spell probe: no AMP ring"); return null; }

        var art = new ParaTool.Core.Artifacts.ArtifactDefinition
        {
            StatId = "PT_SpellProbe_Ring",
            StatType = "Armor",
            UsingBase = ring.StatId,
            Slot = "Ring",
            ParentTemplateUuid = resolver.Resolve(ring.StatId, "RootTemplate")!,
            Rarity = "Rare",
            LootPool = "Rings",
            AddToLoot = false,
        };
        art.DisplayName["en"] = "Spell probe ring";

        var copy = ParaTool.Core.Artifacts.SpellCloner.CloneFrom("Target_VampiricTouch", resolver);
        copy.Cooldown = "OncePerShortRest";
        copy.DisplayName["en"] = "Probe Leech";
        var ampEdit = ParaTool.Core.Artifacts.SpellCloner.CloneFrom("Projectile_Ring32", resolver);
        ampEdit.EditOriginal = true;
        ampEdit.SpellSuccess = "DealDamage(4d12,Fire,Magical)";
        var vanillaName = new[] { "Projectile_MagicMissile", "Target_Shield", "Shout_Bless" }
            .First(n => resolver.AllEntries.ContainsKey(n));
        var vanillaEdit = ParaTool.Core.Artifacts.SpellCloner.CloneFrom(vanillaName, resolver);
        vanillaEdit.EditOriginal = true;
        vanillaEdit.Cooldown = "OncePerTurn";

        art.Spells.AddRange([copy, ampEdit, vanillaEdit]);
        art.SpellsOnEquip = $"Target_VampiricTouch;Projectile_Ring32;{vanillaName}";
        var names = new List<string> { "PT_SpellProbe_Ring", "PT_SpellProbe_Ring_Spell_1", "Projectile_Ring32", vanillaName };

        // A vanilla spell that summons a creature ParaTool knows, with a tougher copy of it.
        var summonSpell = resolver.AllEntries.Values
            .Where(e => e.Type == "SpellData")
            .Select(e => (e.Name, uuid: ParaTool.Core.Artifacts.ArtifactCompiler
                .SummonedTemplates(resolver.Resolve(e.Name, "SpellProperties")).FirstOrDefault()))
            .FirstOrDefault(x => x.uuid != null && ParaTool.Core.Services.SummonTemplateIndex.Find(x.uuid) != null);
        if (summonSpell.uuid != null
            && ParaTool.Core.Artifacts.SummonCloner.CloneFrom(summonSpell.uuid, resolver) is { } creature)
        {
            var summonCard = ParaTool.Core.Artifacts.SpellCloner.CloneFrom(summonSpell.Name, resolver);
            creature.Stats["Vitality"] = "123";
            art.Spells.Add(summonCard);
            art.Summons.Add(creature);
            art.SpellsOnEquip += ";" + summonSpell.Name;
            names.AddRange(["PT_SpellProbe_Ring_Spell_4", "PT_SpellProbe_Ring_Summon_1"]);
            _probeCreatureTemplate = creature.TemplateUuid;
            Console.WriteLine($"  spell probe: {summonSpell.Name} summons {summonSpell.uuid} -> copy {creature.TemplateUuid} ({creature.UsingBase})");
        }

        ParaTool.Core.Artifacts.ArtifactStore.Save(art);
        Console.WriteLine($"  spell probe: saved {art.StatId} (base {ring.StatId}) to {ParaTool.Core.Artifacts.ArtifactStore.GetArtifactsDir()}");
        return names;
    }

    private static string? _probeCreatureTemplate;

    /// <summary>
    /// Spin up Avalonia without a window and lay out the real ItemEditorView over the
    /// real VM, so container realization cost (the actual freeze) is measurable.
    /// </summary>
    private static void RunVisualTreePerf(ViewModels.ItemEditorViewModel vm)
    {
        Console.WriteLine("\n=== visual tree perf ===");
        try
        {
            Program.BuildAvaloniaApp().SetupWithoutStarting();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  (skipped: Avalonia setup failed: {ex.Message})");
            return;
        }

        Avalonia.Threading.Dispatcher.UIThread.Invoke(() =>
        {
            // Item labels are relabelled by the owning view model now, not by a per-item
            // subscription — check a language switch still reaches them.
            var probe = vm.Mods.SelectMany(m => m.Items).First();
            var langBefore = probe.ItemLabel;
            var poolBefore = ViewModels.ItemVM.PoolOptions[0].Display;
            Localization.Loc.Instance.SetLanguage("ru");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Console.WriteLine($"  lang en->ru: item \"{langBefore}\" -> \"{probe.ItemLabel}\"; " +
                              $"pool option \"{poolBefore}\" -> \"{ViewModels.ItemVM.PoolOptions[0].Display}\"");
            Localization.Loc.Instance.SetLanguage("en");
            Avalonia.Threading.Dispatcher.UIThread.RunJobs();
            Console.WriteLine($"  back to en: item \"{probe.ItemLabel}\"; " +
                              $"pool option \"{ViewModels.ItemVM.PoolOptions[0].Display}\"");

            static int CountVisuals(Avalonia.Visual v)
            {
                int n = 1;
                foreach (var c in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(v))
                    n += CountVisuals(c);
                return n;
            }

            // Each scenario gets a fresh view so the number is "cost of displaying this
            // state from scratch" — an already-laid-out tree caches its containers and
            // would hide the realization cost we are hunting.
            void Layout(string label, Action setup)
            {
                setup();
                GC.Collect(2, GCCollectionMode.Forced, true, true);
                var heapBefore = GC.GetTotalMemory(true);

                var view = new Views.ItemEditorView { DataContext = vm };
                var root = new Avalonia.Controls.Window { Width = 1400, Height = 900, Content = view };
                var t = System.Diagnostics.Stopwatch.StartNew();
                // A virtualizing panel fills its viewport over several layout passes, so
                // one Measure/Arrange under-reports. Settle first, then read the numbers.
                int visuals = 0, passes = 0;
                for (; passes < 20; passes++)
                {
                    root.InvalidateMeasure();
                    root.Measure(new Avalonia.Size(1400, 900));
                    root.Arrange(new Avalonia.Rect(0, 0, 1400, 900));
                    // A virtualizing panel that could not size its viewport during measure
                    // schedules the fill-in as a dispatcher job — pump it.
                    Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                    var n = CountVisuals(root);
                    if (n == visuals) break;
                    visuals = n;
                }
                t.Stop();
                var heap = GC.GetTotalMemory(true);

                // Count realized rows so "cheap" can be told apart from "rendered nothing".
                int modRows = 0, itemRows = 0;
                var scroll = Avalonia.VisualTree.VisualExtensions
                    .GetVisualDescendants(view)
                    .OfType<Avalonia.Controls.ScrollViewer>()
                    .FirstOrDefault(s => s.Name == "ModListScroll");
                void CountRows(Avalonia.Visual v)
                {
                    if (v is Avalonia.Controls.Control c)
                    {
                        if (c.DataContext is ViewModels.ItemVM && c is Avalonia.Controls.Border) itemRows++;
                        if (c.DataContext is ViewModels.ModVM && c is Avalonia.Controls.Border) modRows++;
                    }
                    foreach (var ch in Avalonia.VisualTree.VisualExtensions.GetVisualChildren(v))
                        CountRows(ch);
                }
                if (scroll != null) CountRows(scroll);

                Console.WriteLine($"  {label,-38} {t.Elapsed.TotalMilliseconds,7:F0} ms   " +
                                  $"visuals {visuals,8}   heap +{(heap - heapBefore) / 1024.0 / 1024.0,7:F1} MB   " +
                                  $"rows {modRows}mod/{itemRows}item of {vm.Rows.Count}   " +
                                  $"viewport {scroll?.Viewport.Height ?? -1:F0}px  passes {passes}");

                // Render the laid-out tree to a PNG so the list can be eyeballed without
                // touching the desktop.
                var shot = Path.Combine(Path.GetTempPath(), "paratool-perf-" +
                    string.Concat(label.Where(char.IsLetterOrDigit)) + ".png");
                try
                {
                    using var rtb = new Avalonia.Media.Imaging.RenderTargetBitmap(
                        new Avalonia.PixelSize(1400, 900), new Avalonia.Vector(96, 96));
                    rtb.Render(view);
                    rtb.Save(shot);
                    Console.WriteLine($"        -> {shot}");
                }
                catch (Exception ex) { Console.WriteLine($"        (no shot: {ex.Message})"); }

                root.Content = null;
            }

            void Reset()
            {
                vm.SearchText = "";
                foreach (var m in vm.Mods) m.IsExpanded = false;
            }

            var big = vm.Mods.OrderByDescending(m => m.Items.Count).First();

            Layout("warmup", Reset);
            Layout("all mods collapsed", Reset);
            Layout($"one mod expanded ({big.Items.Count} items)", () =>
            {
                Reset();
                big.IsExpanded = true;
            });
            // What typing in the search box actually does today: auto-expand every mod
            // that has a match, which realizes a container for EVERY item in that mod,
            // not just the matching ones.
            Layout("search \"ring\" (auto-expands)", () =>
            {
                Reset();
                vm.SearchText = "ring";
            });
            Layout("search \"zzzznomatch\"", () =>
            {
                Reset();
                vm.SearchText = "zzzznomatch";
            });
            Reset();
        });
    }

    /// <summary>Recursively print a template node's attributes and child tree.</summary>
    private static void DumpNode(ParaTool.Core.LSLib.Node node, int depth)
    {
        var indent = new string(' ', depth * 2);
        Console.WriteLine($"{indent}<{node.Name}>  ({node.Children.Sum(c => c.Value.Count)} child groups: {string.Join(", ", node.Children.Keys)})");
        foreach (var (k, v) in node.Attributes.OrderBy(a => a.Key))
            Console.WriteLine($"{indent}  {k} [{v.Type}] = {v.Value}");
        foreach (var (_, list) in node.Children)
            foreach (var child in list)
                DumpNode(child, depth + 1);
    }
}
