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
