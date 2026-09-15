using System.Text;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Models;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;

namespace ParaTool.Core.Patching;

public sealed class PatchProgress
{
    public string Stage { get; init; } = "";
    public int Percent { get; init; }
}

public sealed class PatchResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public int ItemsPatched { get; init; }

    /// <summary>
    /// Name of the AMP submod the patch was written into, or null when it went into AMP itself.
    /// </summary>
    public string? TargetName { get; init; }

    /// <summary>
    /// Non-fatal warnings collected from ArtifactCompiler while building each artifact's
    /// stats text (placeholder tokens, missing status references, auto-generated passive
    /// names). Patching still succeeds; the UI should surface these to the user.
    /// </summary>
    public List<string> Warnings { get; init; } = [];
}

public sealed class AmpPatcher
{
    /// <summary>
    /// Mods that get written into AMP's meta.lsx dependencies so the game loads them before AMP.
    /// AMP submods are excluded: they already declare AMP as their own dependency, so listing
    /// them here forms a cycle (AMP → submod → AMP) and the game fails to load. They also load
    /// after AMP by design, which is exactly what their rebalances need.
    /// </summary>
    public static List<ModInfo> SelectDependencyMods(IReadOnlyList<ModInfo> mods) => mods
        .Where(m => m.Items.Any(i => i.Enabled))
        .Where(m => !m.IsAmp)
        .Where(m => !m.IsAmpSubmod)
        .Where(m => !string.IsNullOrEmpty(m.PakPath)) // Exclude virtual mods (artifacts)
        .ToList();

    public async Task<PatchResult> PatchAsync(
        string ampPakPath,
        IReadOnlyList<ModInfo> mods,
        ModInfo? ampMod = null,
        IProgress<PatchProgress>? progress = null,
        CancellationToken ct = default)
    {
        var submodMods = mods
            .Where(m => m.IsAmpSubmod && !string.IsNullOrEmpty(m.PakPath))
            .ToList();

        var submodItems = submodMods
            .SelectMany(m => m.Items)
            .Where(i => i.Enabled)
            .ToList();

        var enabledModItems = mods
            .Where(m => !string.IsNullOrEmpty(m.PakPath)) // Exclude virtual mods (artifacts handled by ApplyArtifacts)
            .Where(m => !m.IsAmpSubmod)
            .SelectMany(m => m.Items)
            .Where(i => i.Enabled)
            .ToList();

        var modifiedAmpItems = ampMod?.Items
            .Where(i => i.IsModified && i.Enabled)
            .ToList() ?? new List<ItemEntry>();

        // For TT patching: pass all items (including unmodified AMP for removal logic)
        var allItemsForTt = mods.SelectMany(m => m.Items).ToList();
        if (ampMod != null)
            allItemsForTt.AddRange(ampMod.Items);

        var hasArtifacts = ArtifactStore.LoadAll().Any(a => a.PatchEnabled);
        if (enabledModItems.Count == 0 && modifiedAmpItems.Count == 0
            && submodItems.Count == 0 && !hasArtifacts)
        {
            var disabledAmpItems = ampMod?.Items.Where(i => !i.Enabled && i.IsModified).ToList()
                ?? new List<ItemEntry>();
            if (disabledAmpItems.Count == 0)
                return new PatchResult { Success = false, Error = "No items selected." };
        }

        var modsWithEnabledItems = SelectDependencyMods(mods);

        // Exactly one pak is written. A submod loads after AMP, so it can carry every edit and
        // wins wherever it and AMP define the same table or entry; with no submod, AMP itself.
        var target = SelectPatchTarget(submodMods);

        using var tempDir = new TempDirectoryManager();

        try
        {
            var warnings = new List<string>();
            var artifactCount = target == null
                ? await PatchAmpTargetAsync(ampPakPath, tempDir, allItemsForTt, modifiedAmpItems,
                    enabledModItems, modsWithEnabledItems, warnings, progress, ct)
                : await PatchSubmodTargetAsync(ampPakPath, target, submodMods, tempDir, allItemsForTt,
                    modifiedAmpItems, enabledModItems, submodItems, modsWithEnabledItems, warnings, progress, ct);

            // A pak an earlier run wrote to that is not the target now goes back to its pristine
            // copy — the patch has to live in exactly one pak.
            progress?.Report(new PatchProgress { Stage = "Restoring other paks...", Percent = 95 });
            var others = submodMods.Where(m => m != target).Select(m => m.PakPath!).ToList();
            if (target != null) others.Add(ampPakPath);
            foreach (var pak in others)
                await Task.Run(() => AmpBackupService.RestoreIfPatched(pak), ct);

            progress?.Report(new PatchProgress { Stage = "Done!", Percent = 100 });

            return new PatchResult
            {
                Success = true,
                ItemsPatched = enabledModItems.Count + modifiedAmpItems.Count
                    + submodItems.Count + artifactCount,
                TargetName = target?.Name,
                Warnings = warnings,
            };
        }
        catch (Exception ex)
        {
            return new PatchResult { Success = false, Error = ex.Message };
        }
    }

    /// <summary>
    /// The pak a patch is written into: the AMP submod BG3 loads last, or null for AMP itself
    /// when no submod is installed.
    /// </summary>
    public static ModInfo? SelectPatchTarget(IReadOnlyList<ModInfo> mods)
    {
        var submods = mods.Where(m => m.IsAmpSubmod && !string.IsNullOrEmpty(m.PakPath)).ToList();
        return submods.Count == 0 ? null : OrderSubmodsByLoad(submods)[^1];
    }

    /// <summary>
    /// Submods in load order as far as the paks tell: one that lists another as a dependency
    /// loads after it. Unrelated submods fall back to name order, so the pick stays stable.
    /// </summary>
    public static List<ModInfo> OrderSubmodsByLoad(IReadOnlyList<ModInfo> submods)
    {
        var byUuid = submods
            .GroupBy(s => s.UUID, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);
        var depth = new Dictionary<ModInfo, int>();

        int Depth(ModInfo mod, HashSet<ModInfo> visiting)
        {
            if (depth.TryGetValue(mod, out var known)) return known;
            if (!visiting.Add(mod)) return 0; // dependency cycle — BG3 refuses to load those anyway
            var d = 0;
            foreach (var dep in mod.DependencyUuids)
                if (byUuid.TryGetValue(dep, out var other) && other != mod)
                    d = Math.Max(d, Depth(other, visiting) + 1);
            visiting.Remove(mod);
            return depth[mod] = d;
        }

        return submods
            .OrderBy(s => Depth(s, new HashSet<ModInfo>()))
            .ThenBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static async Task<int> PatchAmpTargetAsync(
        string ampPakPath, TempDirectoryManager tempDir,
        List<ItemEntry> allItemsForTt, List<ItemEntry> modifiedAmpItems, List<ItemEntry> enabledModItems,
        List<ModInfo> dependencyMods, List<string> warnings,
        IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        var extractDir = tempDir.CreateSubDirectory("amp_extract");

        progress?.Report(new PatchProgress { Stage = "Creating backup...", Percent = 5 });
        await Task.Run(() => AmpBackupService.EnsureBackup(ampPakPath), ct);

        // Extract from the pristine backup, not the current pak: patching an already patched pak
        // would stack stale overrides on every run. EnsureBackup refreshes the backup when AMP
        // updates, so the user's edits apply to the new AMP on the next patch.
        progress?.Report(new PatchProgress { Stage = "Extracting AMP pak...", Percent = 10 });
        var extractSource = AmpBackupService.HasBackup(ampPakPath)
            ? AmpBackupService.GetBackupPath(ampPakPath)
            : ampPakPath;
        await Task.Run(() => PakReader.ExtractAll(extractSource, extractDir), ct);

        progress?.Report(new PatchProgress { Stage = "Patching loot lists...", Percent = 30 });
        var ttPath = FindFile(extractDir, "TreasureTable.txt")
            ?? throw new InvalidOperationException("TreasureTable.txt not found in AMP pak.");
        var ttText = await File.ReadAllTextAsync(ttPath, ct);
        OriginalTtStore.Store(ampPakPath, ttText);
        await File.WriteAllTextAsync(ttPath, TreasureTablePatcher.Patch(ttText, allItemsForTt), ct);

        progress?.Report(new PatchProgress { Stage = "Applying stat overrides...", Percent = 50 });
        var statsDir = FindDirectory(extractDir, Path.Combine("Stats", "Generated", "Data"));
        if (statsDir == null)
        {
            var publicDirs = Directory.GetDirectories(extractDir, "Public", SearchOption.TopDirectoryOnly);
            var subDirs = publicDirs.Length > 0 ? Directory.GetDirectories(publicDirs[0]) : [];
            if (subDirs.Length > 0)
            {
                statsDir = Path.Combine(subDirs[0], "Stats", "Generated", "Data");
                Directory.CreateDirectory(statsDir);
            }
        }

        if (statsDir != null)
        {
            DeleteOldOverrideFiles(statsDir);
            await Task.Run(() => ApplyStatOverrides(statsDir, modifiedAmpItems, enabledModItems), ct);
        }

        progress?.Report(new PatchProgress { Stage = "Applying artifacts...", Percent = 58 });
        var artifactCount = await Task.Run(
            () => ApplyArtifacts(extractDir, statsDir, ampPakPath, warnings, sourceDir: null).Count, ct);

        progress?.Report(new PatchProgress { Stage = "Updating dependencies...", Percent = 65 });
        await PatchMetaAsync(extractDir, dependencyMods, ct);

        progress?.Report(new PatchProgress { Stage = "Repacking AMP pak...", Percent = 80 });
        await RepackAsync(extractDir, ampPakPath, ct);
        return artifactCount;
    }

    private static async Task<int> PatchSubmodTargetAsync(
        string ampPakPath, ModInfo target, IReadOnlyList<ModInfo> submods, TempDirectoryManager tempDir,
        List<ItemEntry> allItemsForTt, List<ItemEntry> modifiedAmpItems, List<ItemEntry> enabledModItems,
        List<ItemEntry> submodItems, List<ModInfo> dependencyMods, List<string> warnings,
        IProgress<PatchProgress>? progress, CancellationToken ct)
    {
        var targetPak = target.PakPath;

        progress?.Report(new PatchProgress { Stage = "Creating backup...", Percent = 5 });
        await Task.Run(() => AmpBackupService.EnsureBackup(targetPak), ct);

        // AMP is only read: the tables, stats and templates the submod builds on.
        progress?.Report(new PatchProgress { Stage = "Reading AMP pak...", Percent = 10 });
        var sourceDir = tempDir.CreateSubDirectory("amp_source");
        await Task.Run(() => ExtractMatching(PakSource.Resolve(ampPakPath), sourceDir, IsBuildSourceFile), ct);

        progress?.Report(new PatchProgress { Stage = $"Extracting {target.Name}...", Percent = 20 });
        var extractDir = tempDir.CreateSubDirectory("target_extract");
        await Task.Run(() => PakReader.ExtractAll(PakSource.Resolve(targetPak), extractDir), ct);

        // Loot: a table the submod does not restate is taken from the last earlier pak that
        // defines it, and written into the submod only when the edits change it.
        progress?.Report(new PatchProgress { Stage = "Patching loot lists...", Percent = 30 });
        var ampTtPath = FindFile(sourceDir, "TreasureTable.txt")
            ?? throw new InvalidOperationException("TreasureTable.txt not found in AMP pak.");
        var ampTt = await File.ReadAllTextAsync(ampTtPath, ct);
        OriginalTtStore.Store(ampPakPath, ampTt);

        var earlierTables = new List<string> { ampTt };
        foreach (var sub in OrderSubmodsByLoad(submods).TakeWhile(s => s != target))
            if (ReadPakText(PakSource.Resolve(sub.PakPath), "TreasureTable.txt") is { } subTt)
                earlierTables.Add(subTt);

        var generatedDir = Path.Combine(extractDir, "Public", target.Folder, "Stats", "Generated");
        var ttPath = FindFile(extractDir, "TreasureTable.txt") ?? Path.Combine(generatedDir, "TreasureTable.txt");
        var targetTt = File.Exists(ttPath) ? await File.ReadAllTextAsync(ttPath, ct) : "";
        var patchedTt = TreasureTablePatcher.PatchIntoTarget(earlierTables, targetTt, allItemsForTt);
        if (patchedTt != targetTt)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(ttPath)!);
            await File.WriteAllTextAsync(ttPath, patchedTt, ct);
        }

        // Stats: AMP's entries are not in this pak, so every override goes in as a thin
        // re-declaration behind everything the submod declares itself.
        progress?.Report(new PatchProgress { Stage = "Applying stat overrides...", Percent = 50 });
        var statsDir = FindDirectory(extractDir, Path.Combine("Stats", "Generated", "Data"))
            ?? Path.Combine(generatedDir, "Data");
        Directory.CreateDirectory(statsDir);
        DeleteOldOverrideFiles(statsDir);
        var itemOverrides = BuildSubmodOverrideText(modifiedAmpItems, enabledModItems, submodItems, "");
        if (!string.IsNullOrWhiteSpace(itemOverrides))
            File.AppendAllText(LastStatFile(statsDir), "\n" + itemOverrides);

        progress?.Report(new PatchProgress { Stage = "Applying artifacts...", Percent = 58 });
        var artifactCount = await Task.Run(
            () => ApplyArtifacts(extractDir, statsDir, ampPakPath, warnings, sourceDir).Count, ct);

        progress?.Report(new PatchProgress { Stage = "Updating dependencies...", Percent = 65 });
        await PatchMetaAsync(extractDir, dependencyMods, ct);

        // Marker file: AmpBackupService and PakSource tell a patched pak from a fresh one by it.
        File.WriteAllText(Path.Combine(statsDir, "ZZZ_ParaTool_Overrides.txt"), "// Patched by ParaTool\n");

        progress?.Report(new PatchProgress { Stage = $"Repacking {target.Name}...", Percent = 80 });
        await RepackAsync(extractDir, targetPak, ct);
        return artifactCount;
    }

    private static void DeleteOldOverrideFiles(string statsDir)
    {
        foreach (var oldFile in new[] { "ParaTool_Overrides.txt", "ZZZ_ParaTool_Overrides.txt" })
        {
            var oldPath = Path.Combine(statsDir, oldFile);
            if (File.Exists(oldPath))
                File.Delete(oldPath);
        }
    }

    /// <summary>The stat file BG3 loads last for a mod; a new one when the mod has none.</summary>
    private static string LastStatFile(string statsDir) =>
        Directory.GetFiles(statsDir, "*.txt")
            .Where(f => !Path.GetFileName(f).EndsWith("ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .LastOrDefault()
        ?? Path.Combine(statsDir, "ZZZ_ParaTool_Stats.txt");

    private static async Task PatchMetaAsync(string extractDir, IReadOnlyList<ModInfo> dependencyMods, CancellationToken ct)
    {
        var metaPath = FindFile(extractDir, "meta.lsx");
        if (metaPath == null) return;
        var metaXml = await File.ReadAllTextAsync(metaPath, ct);
        await File.WriteAllTextAsync(metaPath, MetaLsxPatcher.Patch(metaXml, dependencyMods), ct);
    }

    private static async Task RepackAsync(string extractDir, string pakPath, CancellationToken ct)
    {
        var tempPakPath = pakPath + ".tmp";
        await Task.Run(() => PakWriter.CreatePak(extractDir, tempPakPath), ct);
        File.Delete(pakPath);
        File.Move(tempPakPath, pakPath);
    }

    private static bool IsBuildSourceFile(string path) =>
        path.Contains("/Stats/Generated/", StringComparison.OrdinalIgnoreCase)
        || path.Contains("/RootTemplates/", StringComparison.OrdinalIgnoreCase)
        || path.EndsWith("/meta.lsx", StringComparison.OrdinalIgnoreCase);

    private static void ExtractMatching(string pakPath, string outputDir, Func<string, bool> include)
    {
        using var fs = File.OpenRead(pakPath);
        var header = PakReader.ReadHeader(fs);
        foreach (var entry in PakReader.ReadFileList(fs, header).Where(e => include(e.Path)))
            PakReader.ExtractFile(fs, entry,
                Path.Combine(outputDir, entry.Path.Replace('/', Path.DirectorySeparatorChar)));
    }

    private static string? ReadPakText(string pakPath, string fileName)
    {
        using var fs = File.OpenRead(pakPath);
        var header = PakReader.ReadHeader(fs);
        var entry = PakReader.ReadFileList(fs, header)
            .FirstOrDefault(e => e.Path.EndsWith("/" + fileName, StringComparison.OrdinalIgnoreCase));
        return entry.Path == null ? null : Encoding.UTF8.GetString(PakReader.ExtractFileData(fs, entry));
    }

    /// <summary>
    /// Applies stat overrides:
    /// - Items AMP already defines: modify entries in-place within their source stat files
    /// - Everything else: append skeleton entries to the last stat file
    /// Creates a marker file so we know the pak was patched.
    /// </summary>
    private static void ApplyStatOverrides(
        string statsDir,
        IReadOnlyList<ItemEntry> ampItems,
        IReadOnlyList<ItemEntry> modItems)
    {
        // Build override fields for every item we might touch. Mod items are included in the
        // in-place pass because a mod StatId can collide with one AMP already defines (submod
        // rebalances, mods that redefine AMP gear) — appending a skeleton for those would put a
        // second definition of the same entry into AMP's own pak.
        var ampMods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ampItems.Concat(modItems))
        {
            var fields = StatsOverrideGenerator.ComputeFields(item);
            if (fields != null)
                ampMods.TryAdd(item.StatId, fields);
        }

        // Get all stat files (excluding old/new overrides)
        var statFiles = Directory.GetFiles(statsDir, "*.txt")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return !name.Equals("ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase) &&
                       !name.Equals("ZZZ_ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Step A: Modify already-defined items in-place across stat files
        var unresolved = new HashSet<string>(ampMods.Keys, StringComparer.OrdinalIgnoreCase);

        foreach (var filePath in statFiles)
        {
            if (unresolved.Count == 0) break;

            var text = File.ReadAllText(filePath);

            // Only pass entries that might be in this file
            var relevant = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var statId in unresolved)
            {
                if (text.Contains(statId, StringComparison.OrdinalIgnoreCase))
                    relevant[statId] = ampMods[statId];
            }
            if (relevant.Count == 0) continue;

            var (modified, foundEntries) = StatsFileEditor.ModifyEntries(text, relevant);
            if (foundEntries.Count > 0)
            {
                File.WriteAllText(filePath, modified);
                foreach (var entry in foundEntries)
                    unresolved.Remove(entry);
            }
        }

        // Step B: Generate skeleton entries for everything AMP didn't already define
        // (mod items proper, plus any AMP item whose entry wasn't found in any stat file).
        var skeletonItems = ampItems.Concat(modItems)
            .Where(i => unresolved.Contains(i.StatId))
            .GroupBy(i => i.StatId, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();
        var skeletonText = StatsOverrideGenerator.GenerateSkeletonEntries(skeletonItems);

        // Append skeleton entries to the last stat file (loaded last by BG3)
        if (!string.IsNullOrWhiteSpace(skeletonText) && statFiles.Length > 0)
        {
            var lastFile = statFiles[^1];
            var text = File.ReadAllText(lastFile);
            text = StatsFileEditor.AppendSkeletonEntries(text, skeletonText);
            File.WriteAllText(lastFile, text);
        }

        // Step C: Create marker file so we know the pak was patched by ParaTool
        var markerPath = Path.Combine(statsDir, "ZZZ_ParaTool_Overrides.txt");
        File.WriteAllText(markerPath, "// Patched by ParaTool\n");
    }

    /// <summary>
    /// Loads all saved artifacts, compiles them, and applies to extracted pak:
    /// - Overrides: modify existing Stats entries in-place
    /// - New items: append Stats + add to TreasureTable
    /// - Both: write Loca XML entries
    /// </summary>
    /// <param name="sourceDir">
    /// Extracted AMP when the patch is written into a submod: its entries, templates and tables
    /// are built on but never edited. Null when the pak being written is AMP itself.
    /// </param>
    private static ArtifactApplyResult ApplyArtifacts(string extractDir, string? statsDir, string ampPakPath,
        List<string>? warnings = null, string? sourceDir = null)
    {
        var logPath = Path.Combine(Path.GetTempPath(), "paratool_patch_debug.txt");
        var log = new System.Text.StringBuilder();

        var allArts = ArtifactStore.LoadAll();
        log.AppendLine($"LoadAll: {allArts.Count} artifacts from {ArtifactStore.GetArtifactsDir()}");
        foreach (var a in allArts)
            log.AppendLine($"  - {a.StatId} PatchEnabled={a.PatchEnabled} UsingBase={a.UsingBase}");

        var artifacts = allArts.Where(a => a.PatchEnabled).ToList();
        log.AppendLine($"After filter: {artifacts.Count}, statsDir={statsDir}");

        if (artifacts.Count == 0 || statsDir == null)
        {
            File.WriteAllText(logPath, log.ToString());
            return new ArtifactApplyResult(0);
        }

        var overrideStats = new StringBuilder();
        var newStats = new StringBuilder();
        var allLocaEntries = new Dictionary<string, List<(string handle, string xmlText)>>(StringComparer.OrdinalIgnoreCase);
        var customIconStatIds = new List<string>();
        var newArtifacts = new List<ArtifactDefinition>();
        var overrideArtifacts = new List<ArtifactDefinition>();
        int count = 0;

        // Build a resolver (AMP stats + vanilla bases) so the compiler can re-emit the
        // identity/behaviour fields an item would otherwise only inherit via `using`.
        // Vanilla is added LAST so the canonical base entries win on name conflicts —
        // AMP redefines `_Shield_Magic` etc. with a self-referential `using`, which would
        // otherwise truncate the chain before it reaches vanilla `_Shield` (where
        // `Shield "Yes"` lives). Without a resolver here, Compile() ran with null and the
        // explicit-Slot/identity safety net was dead at patch time.
        var resolver = new Parsing.StatsResolver();
        var sourceStatsDir = sourceDir == null
            ? null
            : FindDirectory(sourceDir, Path.Combine("Stats", "Generated", "Data"));
        var resolverFiles = (sourceStatsDir == null ? Array.Empty<string>() : Directory.GetFiles(sourceStatsDir, "*.txt"))
            .Concat(Directory.GetFiles(statsDir, "*.txt"));
        foreach (var sf in resolverFiles)
        {
            try { resolver.AddEntries(Parsing.StatsParser.Parse(File.ReadAllText(sf))); }
            catch (Exception ex) { log.AppendLine($"  resolver: skip {Path.GetFileName(sf)}: {ex.Message}"); }
        }
        try
        {
            var vdb = new Services.VanillaDatabase();
            vdb.Load();
            resolver.AddEntries(vdb.Resolver.AllEntries.Values);
        }
        catch (Exception ex) { log.AppendLine($"  resolver: vanilla load failed: {ex.Message}"); }

        foreach (var art in artifacts)
        {
            // Override = same StatId as UsingBase (modifying existing item)
            // New = different StatId (creating new item, even if leftover from previous patch exists in stats)
            bool isOverride = art.StatId.Equals(art.UsingBase, StringComparison.OrdinalIgnoreCase);
            log.AppendLine($"  {art.StatId}: isOverride={isOverride} (UsingBase={art.UsingBase})");
            var compiled = ArtifactCompiler.Compile(art, isOverride, resolver);
            warnings?.AddRange(compiled.Warnings);

            if (isOverride)
            {
                overrideStats.Append(compiled.StatsText);
                overrideArtifacts.Add(art);
            }
            else
            {
                newStats.Append(compiled.StatsText);
                newArtifacts.Add(art);
            }

            // Merge loca entries
            foreach (var (lang, entries) in compiled.LocalizationEntries)
            {
                if (!allLocaEntries.ContainsKey(lang))
                    allLocaEntries[lang] = [];
                allLocaEntries[lang].AddRange(entries);
            }

            // Icon files + track custom icons for metadata
            if (compiled.IconFiles != null)
                customIconStatIds.Add(art.StatId);
            if (compiled.IconFiles != null)
            {
                foreach (var (relativePath, data) in compiled.IconFiles)
                {
                    // Find the Mods/ModFolder/ directory
                    var modsDirs = Directory.GetDirectories(extractDir, "Mods", SearchOption.TopDirectoryOnly);
                    if (modsDirs.Length > 0)
                    {
                        var subDirs = Directory.GetDirectories(modsDirs[0]);
                        if (subDirs.Length > 0)
                        {
                            var iconPath = Path.Combine(subDirs[0], relativePath);
                            Directory.CreateDirectory(Path.GetDirectoryName(iconPath)!);
                            File.WriteAllBytes(iconPath, data);
                        }
                    }
                }
            }

            count++;
        }

        // Write custom icon atlases from AtlasStore
        var atlasFiles = Textures.AtlasStore.GetAllAtlasFiles();
        if (atlasFiles.Count > 0)
        {
            // Find Public/ModFolder/ directory
            var publicDirs = Directory.GetDirectories(extractDir, "Public", SearchOption.TopDirectoryOnly);
            if (publicDirs.Length > 0)
            {
                var modDirs = Directory.GetDirectories(publicDirs[0]);
                if (modDirs.Length > 0)
                {
                    var modFolder = modDirs[0];
                    foreach (var (relativePath, data) in atlasFiles)
                    {
                        var targetPath = Path.Combine(modFolder, relativePath);
                        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
                        File.WriteAllBytes(targetPath, data);
                    }
                }
            }
        }

        // Update GUI/metadata.lsf with custom icon entries
        if (customIconStatIds.Count > 0)
        {
            var metadataLsf = FindFile(extractDir, "metadata.lsf");
            if (metadataLsf != null)
            {
                PatchIconMetadataLsf(metadataLsf, customIconStatIds);
                // Remove .lsx duplicate — BG3 prefers .lsx over .lsf, so our .lsf changes would be ignored
                var metadataLsx = Path.ChangeExtension(metadataLsf, ".lsx");
                if (File.Exists(metadataLsx))
                    File.Delete(metadataLsx);
            }
        }

        var overrideParsed = overrideStats.Length > 0
            ? StatsParser.Parse(overrideStats.ToString())
            : [];

        // Item overrides re-serialized as thin self-referencing entries. A submod target does not
        // declare AMP's entries, so there they are appended instead of edited in place.
        var thinItemOverrides = new StringBuilder();
        foreach (var entry in overrideParsed)
        {
            if (entry.Type != "Armor" && entry.Type != "Weapon") continue;
            thinItemOverrides.AppendLine($"new entry \"{entry.Name}\"");
            thinItemOverrides.AppendLine($"type \"{entry.Type}\"");
            thinItemOverrides.AppendLine($"using \"{entry.Using ?? entry.Name}\"");
            foreach (var (key, value) in entry.Data)
                thinItemOverrides.AppendLine($"data \"{key}\" \"{value}\"");
            thinItemOverrides.AppendLine();
        }

        var statFiles = Directory.GetFiles(statsDir, "*.txt")
            .Where(f =>
            {
                var name = Path.GetFileName(f);
                return !name.Equals("ZZZ_ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase) &&
                       !name.Equals("ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase);
            })
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (statFiles.Length == 0 && sourceDir != null)
        {
            var created = LastStatFile(statsDir);
            File.AppendAllText(created, "");
            statFiles = [created];
        }

        // Apply override stats via in-place editing
        if (overrideStats.Length > 0 && statFiles.Length > 0)
        {
            var overrideMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in overrideParsed)
            {
                if (entry.Type != "Armor" && entry.Type != "Weapon") continue;
                overrideMap[entry.Name] = entry.Data;
            }

            var unresolved = new HashSet<string>(
                sourceDir == null ? overrideMap.Keys.ToList() : new List<string>(), StringComparer.OrdinalIgnoreCase);
            Services.AppLogger.Info($"Applying {overrideMap.Count} override(s): {string.Join(", ", overrideMap.Keys)}");
            foreach (var filePath in statFiles)
            {
                if (unresolved.Count == 0) break;
                var text = File.ReadAllText(filePath);
                var relevant = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
                foreach (var statId in unresolved)
                    if (text.Contains(statId, StringComparison.OrdinalIgnoreCase))
                        relevant[statId] = overrideMap[statId];
                if (relevant.Count == 0) continue;

                var (modified, foundEntries) = StatsFileEditor.ModifyEntries(text, relevant);
                if (foundEntries.Count > 0)
                {
                    File.WriteAllText(filePath, modified);
                    foreach (var entry in foundEntries) unresolved.Remove(entry);
                    Services.AppLogger.Info($"Override applied in {Path.GetFileName(filePath)}: {string.Join(", ", foundEntries)}");
                }
            }
            if (unresolved.Count > 0)
                Services.AppLogger.Warn($"Override entries NOT FOUND in any stat file: {string.Join(", ", unresolved)}");

            // Append passives/statuses/spells from overrides to last stat file
            var nonItemOverrides = new StringBuilder();
            var nonItemNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in overrideParsed)
            {
                if (entry.Type == "Armor" || entry.Type == "Weapon") continue;
                nonItemNames.Add(entry.Name);
                nonItemOverrides.AppendLine($"new entry \"{entry.Name}\"");
                nonItemOverrides.AppendLine($"type \"{entry.Type}\"");
                // Skip self-referencing using (already handled by compiler)
                if (entry.Using != null && !entry.Name.Equals(entry.Using, StringComparison.OrdinalIgnoreCase))
                    nonItemOverrides.AppendLine($"using \"{entry.Using}\"");
                foreach (var (k, v) in entry.Data) nonItemOverrides.AppendLine($"data \"{k}\" \"{v}\"");
                nonItemOverrides.AppendLine();
            }

            // Remove existing entries for these names first (cleanup duplicates + replace originals)
            if (nonItemNames.Count > 0)
            {
                foreach (var sf in statFiles)
                {
                    var text = File.ReadAllText(sf);
                    var cleaned = StatsFileEditor.RemoveEntries(text, nonItemNames);
                    if (cleaned != text) File.WriteAllText(sf, cleaned);
                }
            }

            if (nonItemOverrides.Length > 0)
            {
                var lastFile = statFiles[^1];
                File.AppendAllText(lastFile, "\n" + nonItemOverrides);
            }
        }

        // Remove old artifact stat entries from stat files (leftovers from previous patches)
        // ONLY remove NEW artifacts — NOT overrides (overrides modify existing entries in-place)
        var artifactStatIds = new HashSet<string>(
            artifacts.Where(a => !a.StatId.Equals(a.UsingBase, StringComparison.OrdinalIgnoreCase))
                     .Select(a => a.StatId), StringComparer.OrdinalIgnoreCase);
        if (artifactStatIds.Count > 0)
            Services.AppLogger.Info($"Cleanup: removing {artifactStatIds.Count} new artifact entries: {string.Join(", ", artifactStatIds)}");
        Services.AppLogger.Info($"Cleanup: skipping {artifacts.Count(a => a.StatId.Equals(a.UsingBase, StringComparison.OrdinalIgnoreCase))} override(s)");
        foreach (var sf in statFiles)
        {
            var text = File.ReadAllText(sf);
            var cleaned = StatsFileEditor.RemoveEntries(text, artifactStatIds);
            if (cleaned != text) File.WriteAllText(sf, cleaned);
        }

        // Append new artifact stats to the SAME file where UsingBase is defined
        // (BG3 may not resolve "using" across different stat files)
        if (newStats.Length > 0 && statFiles.Length > 0)
        {
            // Build index: StatId → which file it's in
            var statIdToFile = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var sf in statFiles)
            {
                var parsed = Parsing.StatsParser.Parse(File.ReadAllText(sf));
                foreach (var entry in parsed)
                    statIdToFile.TryAdd(entry.Name, sf);
            }

            // Group new artifacts by target file
            var byFile = new Dictionary<string, StringBuilder>();
            foreach (var art in newArtifacts)
            {
                var compiled = ArtifactCompiler.Compile(art, false, resolver);
                string targetFile = statIdToFile.TryGetValue(art.UsingBase, out var baseFile)
                    ? baseFile : statFiles[^1];

                if (!byFile.TryGetValue(targetFile, out var sb))
                {
                    sb = new StringBuilder();
                    byFile[targetFile] = sb;
                }
                sb.Append(compiled.StatsText);
            }

            foreach (var (file, content) in byFile)
                File.AppendAllText(file, "\n" + content);
        }

        // TreasureTable for new items is handled by the main TT patching step

        // Generate/update RootTemplates for artifacts
        var ownTemplates = newArtifacts.Count > 0 || overrideArtifacts.Count > 0
            ? PatchRootTemplates(extractDir, newArtifacts, overrideArtifacts, warnings, sourceDir)
            : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // An override that just got a template of its own only uses it once its stat entry points
        // there. Every declaration in the written pak is edited — AMP restates some entries in
        // more than one file.
        var thinOverrideText = thinItemOverrides.ToString();
        if (ownTemplates.Count > 0)
        {
            var rootTemplateEdits = ownTemplates.ToDictionary(
                kv => kv.Key,
                kv => new Dictionary<string, string> { ["RootTemplate"] = kv.Value },
                StringComparer.OrdinalIgnoreCase);
            foreach (var sf in statFiles)
            {
                var (modified, found) = StatsFileEditor.ModifyEntries(File.ReadAllText(sf), rootTemplateEdits);
                if (found.Count > 0) File.WriteAllText(sf, modified);
            }
            thinOverrideText = StatsFileEditor.ModifyEntries(thinOverrideText, rootTemplateEdits).text;
        }

        // A submod does not declare AMP's entries: its item overrides are re-declarations placed
        // behind everything the submod declares itself.
        if (sourceDir != null && statFiles.Length > 0 && !string.IsNullOrWhiteSpace(thinOverrideText))
            File.AppendAllText(statFiles[^1], "\n" + thinOverrideText);

        // Write loca XML entries
        if (allLocaEntries.Count > 0)
        {
            WriteLocaEntries(extractDir, allLocaEntries);
        }

        log.AppendLine($"Done: {count} artifacts, {newArtifacts.Count} new, {overrideArtifacts.Count} overrides");
        File.WriteAllText(logPath, log.ToString());
        return new ArtifactApplyResult(count);
    }

    private sealed record ArtifactApplyResult(int Count);

    /// <summary>
    /// Writes localization entries into existing .loca.xml files or creates new ones.
    /// </summary>
    private static void WriteLocaEntries(string extractDir,
        Dictionary<string, List<(string handle, string xmlText)>> entries)
    {
        // BG3 loca code → folder name mapping
        var codeToFolder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["en"] = "English", ["ru"] = "Russian", ["de"] = "German", ["fr"] = "French",
            ["es"] = "Spanish", ["it"] = "Italian", ["pl"] = "Polish", ["ja"] = "Japanese",
            ["ko"] = "Korean", ["tr"] = "Turkish", ["uk"] = "Ukrainian", ["zh"] = "Chinese",
            ["pt"] = "BrazilianPortuguese"
        };

        // Language loca lives in Mods/<Folder>/Localization. A Public/<Folder>/Localization can
        // exist as well (AMP Plus keeps generated books there), so the first match is not
        // necessarily the right one; a pak without loca of its own gets the folder created.
        var modsDir = Path.Combine(extractDir, "Mods");
        var modFolder = Directory.Exists(modsDir) ? Directory.GetDirectories(modsDir).FirstOrDefault() : null;
        if (modFolder == null) return;

        var locaBase = Path.Combine(modFolder, "Localization");
        Directory.CreateDirectory(locaBase);

        foreach (var (lang, locaEntries) in entries)
        {
            if (locaEntries.Count == 0) continue;

            var folderName = codeToFolder.GetValueOrDefault(lang, "English");
            var langDir = Path.Combine(locaBase, folderName);
            Directory.CreateDirectory(langDir);

            // Find existing XML loca file or create new one
            var existingXml = Directory.GetFiles(langDir, "*.xml").FirstOrDefault();
            if (existingXml != null)
            {
                // Append entries before </contentList>
                var text = File.ReadAllText(existingXml);
                var insertPoint = text.LastIndexOf("</contentList>", StringComparison.OrdinalIgnoreCase);
                if (insertPoint >= 0)
                {
                    var sb = new StringBuilder();
                    foreach (var (handle, xmlText) in locaEntries)
                        sb.AppendLine($"  <content contentuid=\"{handle}\" version=\"1\">{xmlText}</content>");
                    text = text.Insert(insertPoint, sb.ToString());
                    File.WriteAllText(existingXml, text);
                }
            }
            else
            {
                // Create new file
                var newPath = Path.Combine(langDir, "ParaTool_Artifacts.loca.xml");
                var content = ArtifactCompiler.GenerateLocaXml(locaEntries);
                File.WriteAllText(newPath, content);
            }
        }
    }

    /// <summary>
    /// Patches RootTemplates:
    /// - Override artifacts: finds GameObjects node in individual {uuid}.lsf or _merged.lsf,
    ///   updates DisplayName, Description, Icon
    /// - New artifacts: creates individual {uuid}.lsf files (safer than modifying _merged.lsf)
    /// </summary>
    /// <returns>
    /// StatId → template UUID for override artifacts that got a template of their own; their stat
    /// entries still have to point at it through RootTemplate.
    /// </returns>
    internal static Dictionary<string, string> PatchRootTemplates(string extractDir,
        IReadOnlyList<ArtifactDefinition> newArtifacts,
        IReadOnlyList<ArtifactDefinition> overrideArtifacts,
        List<string>? warnings = null,
        string? sourceDir = null)
    {
        var ownTemplates = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Find RootTemplates directory
        var rtDir = Directory.GetDirectories(extractDir, "RootTemplates", SearchOption.AllDirectories)
            .FirstOrDefault();

        // A submod with no templates of its own still has to carry the artifact templates.
        var publicDir = Path.Combine(extractDir, "Public");
        if (rtDir == null && sourceDir != null && Directory.Exists(publicDir)
            && Directory.GetDirectories(publicDir).FirstOrDefault() is { } modDir)
        {
            rtDir = Path.Combine(modDir, "RootTemplates");
            Directory.CreateDirectory(rtDir);
        }

        // AMP's templates when writing into a submod: read to clone parents, never modified.
        var sourceRtDir = sourceDir == null
            ? null
            : Directory.GetDirectories(sourceDir, "RootTemplates", SearchOption.AllDirectories).FirstOrDefault();

        var rtLog = Path.Combine(Path.GetTempPath(), "paratool_rt_debug.txt");
        File.WriteAllText(rtLog, $"rtDir={rtDir}\nsourceRtDir={sourceRtDir}\nnewArtifacts={newArtifacts.Count}\noverrideArtifacts={overrideArtifacts.Count}\n");

        if (rtDir == null) { File.AppendAllText(rtLog, "ABORT: rtDir is null\n"); return ownTemplates; }

        try
        {
            Dictionary<string, (object? equip, string? parent)>? chainIndex = null;
            object? ResolveEquipType(ArtifactDefinition art)
            {
                // For weapons, resolve the EquipmentTypeID the template would inherit and write
                // it explicitly (animation set). Inheritance via ParentTemplateId is fragile
                // across paks; an unresolved EquipmentTypeID means the wrong/default animation.
                if (!string.Equals(art.StatType, "Weapon", StringComparison.OrdinalIgnoreCase)) return null;
                chainIndex ??= BuildTemplateChainIndex(rtDir, sourceRtDir);
                var equipType = ResolveEquipmentTypeId(art.ParentTemplateUuid, chainIndex);
                File.AppendAllText(rtLog, $"  {art.StatId}: EquipmentTypeID={(equipType?.ToString() ?? "(none)")}\n");
                return equipType;
            }

            // ── Override artifacts: find and update existing templates ──
            if (overrideArtifacts.Count > 0)
            {
                var remaining = new Dictionary<string, ArtifactDefinition>(StringComparer.OrdinalIgnoreCase);
                foreach (var a in overrideArtifacts) remaining[a.StatId] = a;

                // 1. Check individual {uuid}.lsf files first (they override _merged.lsf)
                foreach (var lsfFile in Directory.GetFiles(rtDir, "*.lsf")
                    .Where(f => !Path.GetFileName(f).StartsWith("_")))
                {
                    if (remaining.Count == 0) break;
                    if (TryUpdateTemplateInLsf(lsfFile, remaining)) { }
                }

                // 2. Check _merged.lsf for any remaining
                if (remaining.Count > 0)
                {
                    var mergedPath = Path.Combine(rtDir, "_merged.lsf");
                    if (File.Exists(mergedPath))
                        TryUpdateTemplateInLsf(mergedPath, remaining);
                }

                // 3. No template carries this StatId: the item inherits its RootTemplate through
                //    `using` (AMP tiers like AMP_EnhanceShaman_Amulet_1 share the base's template),
                //    so there was nothing to update and a new name or lore silently never showed.
                //    Give it a template of its own, cloned from the one it inherits.
                foreach (var art in remaining.Values)
                {
                    if (string.IsNullOrEmpty(art.ParentTemplateUuid)) continue;
                    var lsfPath = Path.Combine(rtDir, $"{art.TemplateUuid}.lsf");
                    File.AppendAllText(rtLog, $"  Own template for override: {art.StatId} -> {lsfPath} (inherits {art.ParentTemplateUuid})\n");
                    if (CreateTemplateLsf(lsfPath, art, ResolveEquipType(art), sourceRtDir))
                        ownTemplates[art.StatId] = art.TemplateUuid;
                }
            }

            // ── New artifacts: create individual {uuid}.lsf files ──
            File.AppendAllText(rtLog, $"Creating {newArtifacts.Count} new RootTemplates in {rtDir}\n");
            foreach (var art in newArtifacts)
            {
                var equipType = ResolveEquipType(art);
                var lsfPath = Path.Combine(rtDir, $"{art.TemplateUuid}.lsf");
                File.AppendAllText(rtLog, $"  Creating: {lsfPath} (ParentTemplate={art.ParentTemplateUuid})\n");
                if (!CreateTemplateLsf(lsfPath, art, equipType, sourceRtDir))
                {
                    // The template went out with nothing to inherit from: no visual, no icon, no
                    // equipment data. BG3 cannot instantiate it, so the item exists in the pak but
                    // never drops and cannot even be spawned by UUID. Say so instead of reporting
                    // a clean patch and leaving the user to unpack the pak to find out.
                    File.AppendAllText(rtLog, $"  DEAD TEMPLATE: {art.StatId} (no parent)\n");
                    warnings?.Add(
                        $"{art.StatId}: the base item has no RootTemplate that ParaTool can resolve, " +
                        "so the artifact was written without a parent template. The game cannot " +
                        "spawn it. Build it from a different base item.");
                }
            }
        }
        catch (Exception ex)
        {
            Services.AppLogger.Warn($"RootTemplate patching failed: {ex}");
        }

        return ownTemplates;
    }

    /// <summary>
    /// Try to find and update GameObjects nodes matching override artifacts in an LSF file.
    /// Returns true if any were found and updated.
    /// </summary>
    private static bool TryUpdateTemplateInLsf(string lsfPath, Dictionary<string, ArtifactDefinition> remaining)
    {
        LSLib.Resource resource;
        using (var fs = File.OpenRead(lsfPath))
        {
            var reader = new LSLib.LSFReader(fs);
            resource = reader.Read();
        }

        if (!resource.Regions.TryGetValue("Templates", out var region)) return false;
        if (!region.Children.TryGetValue("GameObjects", out var goNodes)) return false;

        bool modified = false;
        foreach (var goNode in goNodes)
        {
            if (!goNode.Attributes.TryGetValue("Stats", out var statsAttr)) continue;
            var statsVal = statsAttr.Value?.ToString();
            if (statsVal == null || !remaining.TryGetValue(statsVal, out var art)) continue;

            UpdateTemplateNode(goNode, art);
            remaining.Remove(statsVal);
            modified = true;
        }

        if (modified)
        {
            using var outFs = File.Create(lsfPath);
            var writer = new LSLib.LSFWriter(outFs);
            writer.Write(resource);
        }

        return modified;
    }

    /// <summary>Update DisplayName, Description, Icon on an existing GameObjects node.</summary>
    private static void UpdateTemplateNode(LSLib.Node goNode, ArtifactDefinition art)
    {
        if (!string.IsNullOrEmpty(art.DisplayNameHandle))
        {
            goNode.Attributes["DisplayName"] = new LSLib.NodeAttribute(LSLib.AttributeType.TranslatedString)
            {
                Value = new LSLib.TranslatedString { Handle = art.DisplayNameHandle, Version = 1 }
            };
        }

        if (!string.IsNullOrEmpty(art.DescriptionHandle))
        {
            goNode.Attributes["Description"] = new LSLib.NodeAttribute(LSLib.AttributeType.TranslatedString)
            {
                Value = new LSLib.TranslatedString { Handle = art.DescriptionHandle, Version = 1 }
            };
        }

        if (!string.IsNullOrEmpty(art.AtlasIconMapKey))
        {
            goNode.Attributes["Icon"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString)
            {
                Value = art.AtlasIconMapKey
            };
        }
    }

    /// <summary>
    /// Create an individual {uuid}.lsf by cloning the parent template and replacing key fields.
    /// This preserves Equipment/Slot/Visuals from the parent.
    /// </summary>
    /// <summary>
    /// Writes the RootTemplate .lsf for a new artifact. Returns false when neither a parent
    /// template could be cloned nor a ParentTemplateId is known — the file is still written, but
    /// the game has nothing to build the item from and will not spawn it.
    /// </summary>
    private static bool CreateTemplateLsf(string lsfPath, ArtifactDefinition art,
        object? equipmentTypeId = null, string? sourceRtDir = null)
    {
        // Find parent template LSF to clone from — in the pak being written, then in AMP's
        var rtDir = Path.GetDirectoryName(lsfPath)!;
        var parentLsfPath = Path.Combine(rtDir, $"{art.ParentTemplateUuid}.lsf");
        if (!File.Exists(parentLsfPath) && sourceRtDir != null)
            parentLsfPath = Path.Combine(sourceRtDir, $"{art.ParentTemplateUuid}.lsf");

        LSLib.Resource resource;
        LSLib.Node? goNode = null;
        bool clonedParent = false;

        if (File.Exists(parentLsfPath))
        {
            // Clone parent template
            using (var fs = File.OpenRead(parentLsfPath))
            {
                var reader = new LSLib.LSFReader(fs);
                resource = reader.Read();
            }

            // Find the GameObjects node
            if (resource.Regions.TryGetValue("Templates", out var region) &&
                region.Children.TryGetValue("GameObjects", out var nodes) && nodes.Count > 0)
            {
                goNode = nodes[0];
            }
        }
        else
        {
            // Try _merged.lsf in current mod directory, then AMP's
            goNode = FindTemplateInMerged(Path.Combine(rtDir, "_merged.lsf"), art.ParentTemplateUuid);
            if (goNode == null && sourceRtDir != null)
                goNode = FindTemplateInMerged(Path.Combine(sourceRtDir, "_merged.lsf"), art.ParentTemplateUuid);

            // Try all _merged.lsf in the extracted pak (other Public/ folders)
            if (goNode == null)
            {
                var extractRoot = rtDir;
                // Walk up to extract root (parent of Public/)
                while (extractRoot != null && !Directory.Exists(Path.Combine(extractRoot, "Public")))
                    extractRoot = Path.GetDirectoryName(extractRoot);
                if (extractRoot != null)
                {
                    foreach (var merged in Directory.GetFiles(extractRoot, "_merged.lsf", SearchOption.AllDirectories))
                    {
                        if (!merged.Contains("RootTemplates")) continue;
                        goNode = FindTemplateInMerged(merged, art.ParentTemplateUuid);
                        if (goNode != null) break;
                    }
                }
            }

            // Create minimal resource with cloned node
            resource = new LSLib.Resource();
            resource.Metadata = new LSLib.LSMetadata
            {
                MajorVersion = 4, MinorVersion = 8, Revision = 0, BuildNumber = 500
            };
            resource.MetadataFormat = LSLib.LSFMetadataFormat.KeysAndAdjacency;

            var newRegion = new LSLib.Region { Name = "Templates", RegionName = "Templates" };
            resource.Regions["Templates"] = newRegion;

            if (goNode != null)
            {
                goNode.Parent = newRegion;
                newRegion.AppendChild(goNode);
            }
        }

        clonedParent = goNode != null;

        if (goNode == null)
        {
            // Fallback: create minimal node (no Equipment — slot may be wrong)
            resource = new LSLib.Resource();
            resource.Metadata = new LSLib.LSMetadata
            {
                MajorVersion = 4, MinorVersion = 8, Revision = 0, BuildNumber = 500
            };
            resource.MetadataFormat = LSLib.LSFMetadataFormat.KeysAndAdjacency;
            var fallbackRegion = new LSLib.Region { Name = "Templates", RegionName = "Templates" };
            resource.Regions["Templates"] = fallbackRegion;
            goNode = new LSLib.Node { Name = "GameObjects", Parent = fallbackRegion };
            goNode.Attributes["Type"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString) { Value = "item" };
            goNode.Attributes["LevelName"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString) { Value = "" };
            fallbackRegion.AppendChild(goNode);
        }

        // Override key fields on the cloned node
        goNode.Attributes["MapKey"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString)
            { Value = art.TemplateUuid };
        goNode.Attributes["Name"] = new LSLib.NodeAttribute(LSLib.AttributeType.LSString)
            { Value = art.StatId };
        goNode.Attributes["ParentTemplateId"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString)
            { Value = art.ParentTemplateUuid };
        goNode.Attributes["Stats"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString)
            { Value = art.StatId };
        goNode.Attributes["DisplayName"] = new LSLib.NodeAttribute(LSLib.AttributeType.TranslatedString)
        {
            Value = new LSLib.TranslatedString { Handle = art.DisplayNameHandle, Version = 1 }
        };
        goNode.Attributes["Description"] = new LSLib.NodeAttribute(LSLib.AttributeType.TranslatedString)
        {
            Value = new LSLib.TranslatedString { Handle = art.DescriptionHandle, Version = 1 }
        };
        if (!string.IsNullOrEmpty(art.AtlasIconMapKey))
        {
            goNode.Attributes["Icon"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString)
                { Value = art.AtlasIconMapKey };
        }

        // EquipmentTypeID is the weapon-class / equipment-class UUID (one per Greataxe /
        // Longsword / Shortbow / etc.) that drives the animation set — how the character holds
        // and swings the item. Vanilla leaf templates rarely declare it; BG3 inherits it from
        // the parent template chain via ParentTemplateId. That inheritance is fragile across
        // paks, and when it fails a custom weapon plays the wrong/default animation. So for
        // weapons we resolve the REAL value by walking the parent chain (mod _merged + vanilla
        // Shared.pak) and write it explicitly here. This is NOT a hardcoded guess — it is
        // exactly the value that would be inherited, so it cannot mismatch the weapon class.
        // For armor/shields equipmentTypeId is null and nothing is written (they don't use it).
        if (equipmentTypeId != null)
            goNode.Attributes["EquipmentTypeID"] = new LSLib.NodeAttribute(LSLib.AttributeType.UUID)
                { Value = equipmentTypeId };

        using (var outFs = File.Create(lsfPath))
        {
            var writer = new LSLib.LSFWriter(outFs);
            writer.Write(resource);
        }

        // Cloned a real parent, or at least point at one the game can resolve itself through
        // ParentTemplateId. With neither, the minimal fallback node is an empty shell.
        return clonedParent || !string.IsNullOrEmpty(art.ParentTemplateUuid);
    }

    private static LSLib.Node? FindTemplateInMerged(string mergedPath, string uuid)
    {
        if (!File.Exists(mergedPath)) return null;
        try
        {
            using var fs = File.OpenRead(mergedPath);
            var reader = new LSLib.LSFReader(fs);
            var res = reader.Read();
            if (res.Regions.TryGetValue("Templates", out var region) &&
                region.Children.TryGetValue("GameObjects", out var nodes))
            {
                return nodes.FirstOrDefault(n =>
                    n.Attributes.TryGetValue("MapKey", out var mk) &&
                    uuid.Equals(mk.Value?.ToString(), StringComparison.OrdinalIgnoreCase));
            }
        }
        catch { /* ignore corrupt files */ }
        return null;
    }

    /// <summary>
    /// Build a UUID → (EquipmentTypeID value, ParentTemplateId) index from the mod's
    /// RootTemplates/_merged.lsf, so the EquipmentTypeID a weapon would inherit can be resolved
    /// by walking the parent chain and written explicitly.
    ///
    /// Vanilla templates are deliberately not indexed. The old code looked for Shared.pak next to
    /// the AMP pak, which lives in the Mods folder — the file is in the game's Data folder, so
    /// that branch never ran and every weapon artifact ever built relied on the game resolving
    /// EquipmentTypeID through ParentTemplateId. That works; reading the game install at patch
    /// time would buy nothing and would need the install path, which ParaTool does not know.
    /// </summary>
    private static Dictionary<string, (object? equip, string? parent)> BuildTemplateChainIndex(
        string rtDir, string? sourceRtDir = null)
    {
        var index = new Dictionary<string, (object? equip, string? parent)>(StringComparer.OrdinalIgnoreCase);

        void Ingest(LSLib.Resource res)
        {
            if (!res.Regions.TryGetValue("Templates", out var region)) return;
            if (!region.Children.TryGetValue("GameObjects", out var nodes)) return;
            foreach (var n in nodes)
            {
                if (!n.Attributes.TryGetValue("MapKey", out var mk)) continue;
                var key = mk.Value?.ToString();
                if (string.IsNullOrEmpty(key) || index.ContainsKey(key)) continue;
                object? equip = n.Attributes.TryGetValue("EquipmentTypeID", out var et) ? et.Value : null;
                var parent = n.Attributes.TryGetValue("ParentTemplateId", out var pt) ? pt.Value?.ToString() : null;
                index[key] = (equip, string.IsNullOrEmpty(parent) ? null : parent);
            }
        }

        // Mod _merged.lsf — holds the AMP leaf/intermediate templates (e.g. the item's parent).
        // When writing into a submod, the submod's own come first, then AMP's.
        foreach (var dir in new[] { rtDir, sourceRtDir })
        {
            if (dir == null) continue;
            try
            {
                var mergedPath = Path.Combine(dir, "_merged.lsf");
                if (File.Exists(mergedPath))
                {
                    using var fs = File.OpenRead(mergedPath);
                    Ingest(new LSLib.LSFReader(fs).Read());
                }
            }
            catch (Exception ex) { Services.AppLogger.Warn($"Chain index (mod merged) failed: {ex.Message}"); }
        }

        return index;
    }

    /// <summary>
    /// Walk the ParentTemplateId chain from startUuid and return the first non-empty
    /// EquipmentTypeID value found, or null if none is defined anywhere in the chain.
    /// </summary>
    private static object? ResolveEquipmentTypeId(
        string? startUuid, Dictionary<string, (object? equip, string? parent)> index)
    {
        var current = startUuid;
        int depth = 0;
        while (!string.IsNullOrEmpty(current) && depth < 20)
        {
            if (!index.TryGetValue(current, out var node)) return null;
            if (node.equip != null)
            {
                var s = node.equip.ToString();
                if (!string.IsNullOrEmpty(s) && s != "00000000-0000-0000-0000-000000000000")
                    return node.equip;
            }
            current = node.parent;
            depth++;
        }
        return null;
    }

    /// <summary>
    /// Adds custom icon entries to GUI/metadata.lsf (binary) for each artifact with a custom PNG icon.
    /// Each icon gets two entries: 144×144 items_png + 380×380 Tooltips/ItemIcons.
    /// </summary>
    private static void PatchIconMetadataLsf(string metadataLsfPath, IReadOnlyList<string> statIds)
    {
        try
        {
            LSLib.Resource resource;
            using (var fs = File.OpenRead(metadataLsfPath))
            {
                var reader = new LSLib.LSFReader(fs);
                resource = reader.Read();
            }

            if (!resource.Regions.TryGetValue("config", out var configRegion)) return;

            // Collect existing MapKeys to avoid duplicates
            var existingKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (configRegion.Children.TryGetValue("entries", out var entriesNodes))
            {
                foreach (var entriesNode in entriesNodes)
                {
                    if (entriesNode.Children.TryGetValue("Object", out var objects))
                    {
                        foreach (var obj in objects)
                        {
                            if (obj.Attributes.TryGetValue("MapKey", out var mk))
                                existingKeys.Add(mk.Value?.ToString() ?? "");
                        }
                    }
                }
            }

            // Find the entries node to add children to
            var targetEntries = entriesNodes?.FirstOrDefault();
            if (targetEntries == null) return;

            bool modified = false;
            foreach (var statId in statIds)
            {
                // 144×144 console icon
                var consolePath = $"Assets/ControllerUIIcons/items_png/{statId}.png";
                if (!existingKeys.Contains(consolePath))
                {
                    AddMetadataObject(targetEntries, consolePath, 144, 8);
                    modified = true;
                }

                // 380×380 tooltip icon
                var tooltipPath = $"Assets/Tooltips/ItemIcons/{statId}.png";
                if (!existingKeys.Contains(tooltipPath))
                {
                    AddMetadataObject(targetEntries, tooltipPath, 380, 9);
                    modified = true;
                }
            }

            if (!modified) return;

            using (var outFs = File.Create(metadataLsfPath))
            {
                var writer = new LSLib.LSFWriter(outFs);
                writer.Write(resource);
            }
        }
        catch (Exception ex)
        {
            Services.AppLogger.Warn($"metadata.lsf patch failed: {ex}");
        }
    }

    private static void AddMetadataObject(LSLib.Node parentEntries, string mapKey, int size, int mipcount)
    {
        var objNode = new LSLib.Node { Name = "Object", Parent = parentEntries };
        objNode.Attributes["MapKey"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString)
            { Value = mapKey };

        var dataNode = new LSLib.Node { Name = "entries", Parent = objNode };
        dataNode.Attributes["h"] = new LSLib.NodeAttribute(LSLib.AttributeType.Short)
            { Value = (short)size };
        dataNode.Attributes["mipcount"] = new LSLib.NodeAttribute(LSLib.AttributeType.Int8)
            { Value = (sbyte)mipcount };
        dataNode.Attributes["w"] = new LSLib.NodeAttribute(LSLib.AttributeType.Short)
            { Value = (short)size };

        objNode.AppendChild(dataNode);
        parentEntries.AppendChild(objNode);
    }

    /// <summary>
    /// Builds the override block appended to the AMP submod a patch is written into: rarity/price skeletons
    /// for the selected items, followed by the Constructor's item overrides. Later entries win,
    /// so an artifact override lands after (and beats) the skeleton for the same StatId.
    /// </summary>
    public static string BuildSubmodOverrideText(
        IReadOnlyList<ItemEntry> ampItems,
        IReadOnlyList<ItemEntry> modItems,
        IReadOnlyList<ItemEntry> submodItems,
        string artifactOverrides)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<ItemEntry>();
        foreach (var item in ampItems.Concat(modItems).Concat(submodItems))
            if (seen.Add(item.StatId))
                items.Add(item);

        var sb = new StringBuilder();
        sb.Append(StatsOverrideGenerator.GenerateSkeletonEntries(items));
        sb.Append(artifactOverrides);
        return sb.ToString();
    }

    private static string? FindFile(string dir, string fileName)
    {
        return Directory.GetFiles(dir, fileName, SearchOption.AllDirectories).FirstOrDefault();
    }

    private static string? FindDirectory(string dir, string relativePath)
    {
        foreach (var d in Directory.GetDirectories(dir, "*", SearchOption.AllDirectories))
        {
            if (d.Replace('\\', '/').EndsWith(relativePath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
                return d;
        }
        return null;
    }
}
