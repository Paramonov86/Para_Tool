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
    /// How many AMP submod paks got a copy of the stat overrides appended. Submods load after
    /// AMP, so without that copy every entry they re-declare keeps the submod's own values.
    /// </summary>
    public int SubmodsPatched { get; init; }

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
        // Combine mod items + AMP items for TT patching
        var allItems = mods.SelectMany(m => m.Items).ToList();
        if (ampMod != null)
            allItems.AddRange(ampMod.Items);

        // Stat overrides written into AMP's own pak only reach mods that load BEFORE AMP (they
        // get written into AMP's dependencies). AMP submods load AFTER it and restate fields of
        // their own, so every entry they re-declare wins over AMP's copy — an edited ability cap
        // on an item AMP Plus also touches was silently lost. Their items stay out of AMP's stat
        // files (a skeleton there would reference a base that doesn't exist yet); the overrides
        // are mirrored into the submod paks themselves instead, after the AMP pak is written.
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

        using var tempDir = new TempDirectoryManager();
        var extractDir = tempDir.CreateSubDirectory("amp_extract");

        try
        {
            // Step 0: Ensure backup exists before modifying anything
            progress?.Report(new PatchProgress { Stage = "Creating backup...", Percent = 5 });
            await Task.Run(() => AmpBackupService.EnsureBackup(ampPakPath), ct);

            // Step 1: Extract from BACKUP (clean original), not the current AMP pak.
            // The current pak may already contain previous patches — extracting from
            // it would cause each round of patching to accumulate cruft (duplicate
            // stats entries, stale overrides, orphan loca). With backup-sourced extract
            // every patch starts from the pristine baseline and applies the full set
            // of user artifacts fresh.
            //
            // When AMP updates, AmpBackupService.EnsureBackup auto-recreates the backup
            // from the new pak, so the user's artifacts/overrides/pool settings apply
            // to the new AMP automatically on the next patch click.
            progress?.Report(new PatchProgress { Stage = "Extracting AMP pak...", Percent = 10 });
            var extractSource = AmpBackupService.HasBackup(ampPakPath)
                ? AmpBackupService.GetBackupPath(ampPakPath)
                : ampPakPath;
            await Task.Run(() => PakReader.ExtractAll(extractSource, extractDir), ct);

            // Step 2: Find and patch TreasureTable.txt (in-place insertion)
            progress?.Report(new PatchProgress { Stage = "Patching loot lists...", Percent = 30 });
            var ttPath = FindFile(extractDir, "TreasureTable.txt");
            if (ttPath == null)
                return new PatchResult { Success = false, Error = "TreasureTable.txt not found in AMP pak." };

            // The extract now comes from the pristine backup, so the TT we read is
            // already the original. Keep OriginalTtStore in sync for other scanners
            // that still rely on it (e.g. ModScanner's AmpMod loader).
            var ttText = await File.ReadAllTextAsync(ttPath, ct);
            OriginalTtStore.Store(ampPakPath, ttText);

            var patchedTt = TreasureTablePatcher.Patch(ttText, allItemsForTt);
            await File.WriteAllTextAsync(ttPath, patchedTt, ct);

            // Step 3: Apply stat overrides
            progress?.Report(new PatchProgress { Stage = "Applying stat overrides...", Percent = 50 });

            var statsDir = FindDirectory(extractDir, Path.Combine("Stats", "Generated", "Data"));
            if (statsDir == null)
            {
                var publicDirs = Directory.GetDirectories(extractDir, "Public", SearchOption.TopDirectoryOnly);
                if (publicDirs.Length > 0)
                {
                    var subDirs = Directory.GetDirectories(publicDirs[0]);
                    if (subDirs.Length > 0)
                    {
                        statsDir = Path.Combine(subDirs[0], "Stats", "Generated", "Data");
                        Directory.CreateDirectory(statsDir);
                    }
                }
            }

            if (statsDir != null)
            {
                // Clean up old overrides files from previous ParaTool versions
                foreach (var oldFile in new[] { "ParaTool_Overrides.txt", "ZZZ_ParaTool_Overrides.txt" })
                {
                    var oldPath = Path.Combine(statsDir, oldFile);
                    if (File.Exists(oldPath))
                        File.Delete(oldPath);
                }

                await Task.Run(() => ApplyStatOverrides(statsDir, modifiedAmpItems, enabledModItems), ct);
            }

            // Step 3.5: Apply artifact overrides from Constructor
            progress?.Report(new PatchProgress { Stage = "Applying artifacts...", Percent = 58 });
            var artifactWarnings = new List<string>();
            var artifacts = await Task.Run(() => ApplyArtifacts(extractDir, statsDir, ampPakPath, artifactWarnings), ct);
            var artifactCount = artifacts.Count;

            // Step 4: Patch meta.lsx with mod dependencies
            progress?.Report(new PatchProgress { Stage = "Updating dependencies...", Percent = 65 });
            var metaPath = FindFile(extractDir, "meta.lsx");
            if (metaPath != null)
            {
                var metaXml = await File.ReadAllTextAsync(metaPath, ct);
                var patchedMeta = MetaLsxPatcher.Patch(metaXml, modsWithEnabledItems);
                await File.WriteAllTextAsync(metaPath, patchedMeta, ct);
            }

            // Step 5: Repack
            progress?.Report(new PatchProgress { Stage = "Repacking AMP pak...", Percent = 80 });
            var tempPakPath = ampPakPath + ".tmp";
            await Task.Run(() => PakWriter.CreatePak(extractDir, tempPakPath), ct);

            // Replace original with patched
            File.Delete(ampPakPath);
            File.Move(tempPakPath, ampPakPath);

            // Step 6: Mirror the same overrides into every AMP submod. A submod loads after AMP
            // and re-declares entries of its own (AMP Plus restates `Boosts` for its capped
            // items), which beats whatever we just wrote into AMP for exactly those StatIds.
            // Appending the overrides to the end of a submod's last stat file puts them after
            // every declaration that submod makes. The payload is identical in each submod, so
            // the load order between them does not matter — whichever wins carries our values.
            var submodOverrides = BuildSubmodOverrideText(
                modifiedAmpItems, enabledModItems, submodItems, artifacts.OverrideItemStats);

            int submodsPatched = 0;
            for (int i = 0; i < submodMods.Count; i++)
            {
                var submod = submodMods[i];
                progress?.Report(new PatchProgress
                {
                    Stage = $"Patching {submod.Name}...",
                    Percent = 85 + 10 * i / submodMods.Count
                });

                // Nothing left to write: a submod an earlier run patched has to go back to its
                // pristine copy, the same way AMP is rebuilt from its backup on every patch.
                if (string.IsNullOrWhiteSpace(submodOverrides))
                {
                    await Task.Run(() => AmpBackupService.RestorePak(submod.PakPath!), ct);
                    continue;
                }

                if (await Task.Run(() => PatchSubmodPak(submod.PakPath!, submodOverrides), ct))
                    submodsPatched++;
                else
                    artifactWarnings.Add(
                        $"{submod.Name} has no stat files to write overrides into. Items it " +
                        "re-declares keep the submod's own values.");
            }

            progress?.Report(new PatchProgress { Stage = "Done!", Percent = 100 });

            return new PatchResult
            {
                Success = true,
                ItemsPatched = enabledModItems.Count + modifiedAmpItems.Count
                    + submodItems.Count + artifactCount,
                SubmodsPatched = submodsPatched,
                Warnings = artifactWarnings,
            };
        }
        catch (Exception ex)
        {
            return new PatchResult { Success = false, Error = ex.Message };
        }
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
    private static ArtifactApplyResult ApplyArtifacts(string extractDir, string? statsDir, string ampPakPath, List<string>? warnings = null)
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
            return new ArtifactApplyResult(0, "");
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
        foreach (var sf in Directory.GetFiles(statsDir, "*.txt"))
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

        // The item overrides also have to reach every AMP submod — a submod loads after AMP and
        // re-declares entries of its own, so the copy edited into AMP's files alone loses there.
        // Only Armor/Weapon entries travel: passives, statuses and spells live in AMP and no
        // submod restates them, so a second declaration would only risk drifting out of sync.
        var submodOverrideStats = new StringBuilder();
        foreach (var entry in overrideParsed)
        {
            if (entry.Type != "Armor" && entry.Type != "Weapon") continue;
            submodOverrideStats.AppendLine($"new entry \"{entry.Name}\"");
            submodOverrideStats.AppendLine($"type \"{entry.Type}\"");
            submodOverrideStats.AppendLine($"using \"{entry.Using ?? entry.Name}\"");
            foreach (var (key, value) in entry.Data)
                submodOverrideStats.AppendLine($"data \"{key}\" \"{value}\"");
            submodOverrideStats.AppendLine();
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

        // Apply override stats via in-place editing
        if (overrideStats.Length > 0 && statFiles.Length > 0)
        {
            var overrideMap = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in overrideParsed)
            {
                if (entry.Type != "Armor" && entry.Type != "Weapon") continue;
                overrideMap[entry.Name] = entry.Data;
            }

            var unresolved = new HashSet<string>(overrideMap.Keys, StringComparer.OrdinalIgnoreCase);
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
        if (newArtifacts.Count > 0 || overrideArtifacts.Count > 0)
            PatchRootTemplates(extractDir, newArtifacts, overrideArtifacts, warnings);

        // Write loca XML entries
        if (allLocaEntries.Count > 0)
        {
            WriteLocaEntries(extractDir, allLocaEntries);
        }

        log.AppendLine($"Done: {count} artifacts, {newArtifacts.Count} new, {overrideArtifacts.Count} overrides");
        File.WriteAllText(logPath, log.ToString());
        return new ArtifactApplyResult(count, submodOverrideStats.ToString());
    }

    /// <summary>
    /// What ApplyArtifacts produced: how many artifacts were applied, plus the item overrides
    /// re-serialized as thin self-referencing entries for the AMP submod pass.
    /// </summary>
    private sealed record ArtifactApplyResult(int Count, string OverrideItemStats);

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

        // Find existing Localization directory structure
        var locaDirs = Directory.GetDirectories(extractDir, "Localization", SearchOption.AllDirectories);
        if (locaDirs.Length == 0) return;

        var locaBase = locaDirs[0];

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
    private static void PatchRootTemplates(string extractDir,
        IReadOnlyList<ArtifactDefinition> newArtifacts,
        IReadOnlyList<ArtifactDefinition> overrideArtifacts,
        List<string>? warnings = null)
    {
        // Find RootTemplates directory
        var rtDir = Directory.GetDirectories(extractDir, "RootTemplates", SearchOption.AllDirectories)
            .FirstOrDefault();

        var rtLog = Path.Combine(Path.GetTempPath(), "paratool_rt_debug.txt");
        File.WriteAllText(rtLog, $"rtDir={rtDir}\nnewArtifacts={newArtifacts.Count}\noverrideArtifacts={overrideArtifacts.Count}\n");

        if (rtDir == null) { File.AppendAllText(rtLog, "ABORT: rtDir is null\n"); return; }

        try
        {
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
            }

            // ── New artifacts: create individual {uuid}.lsf files ──
            File.AppendAllText(rtLog, $"Creating {newArtifacts.Count} new RootTemplates in {rtDir}\n");
            Dictionary<string, (object? equip, string? parent)>? chainIndex = null;
            foreach (var art in newArtifacts)
            {
                // For weapons, resolve the EquipmentTypeID the template would inherit and write
                // it explicitly (animation set). Inheritance via ParentTemplateId is fragile
                // across paks; an unresolved EquipmentTypeID means the wrong/default animation.
                object? equipType = null;
                if (string.Equals(art.StatType, "Weapon", StringComparison.OrdinalIgnoreCase))
                {
                    chainIndex ??= BuildTemplateChainIndex(rtDir);
                    equipType = ResolveEquipmentTypeId(art.ParentTemplateUuid, chainIndex);
                    File.AppendAllText(rtLog, $"  {art.StatId}: EquipmentTypeID={(equipType?.ToString() ?? "(none)")}\n");
                }
                var lsfPath = Path.Combine(rtDir, $"{art.TemplateUuid}.lsf");
                File.AppendAllText(rtLog, $"  Creating: {lsfPath} (ParentTemplate={art.ParentTemplateUuid})\n");
                if (!CreateTemplateLsf(lsfPath, art, equipType))
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
        object? equipmentTypeId = null)
    {
        // Find parent template LSF to clone from
        var rtDir = Path.GetDirectoryName(lsfPath)!;
        var parentLsfPath = Path.Combine(rtDir, $"{art.ParentTemplateUuid}.lsf");

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
            // Try _merged.lsf in current mod directory
            goNode = FindTemplateInMerged(Path.Combine(rtDir, "_merged.lsf"), art.ParentTemplateUuid);

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
        string rtDir)
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
        try
        {
            var mergedPath = Path.Combine(rtDir, "_merged.lsf");
            if (File.Exists(mergedPath))
            {
                using var fs = File.OpenRead(mergedPath);
                Ingest(new LSLib.LSFReader(fs).Read());
            }
        }
        catch (Exception ex) { Services.AppLogger.Warn($"Chain index (mod merged) failed: {ex.Message}"); }

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
    /// Builds the override block that gets appended to every AMP submod: rarity/price skeletons
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

    /// <summary>
    /// Appends the stat overrides to a single AMP submod pak. Returns false when the pak carries
    /// no stat files, in which case it re-declares nothing and needs no patch.
    /// </summary>
    private static bool PatchSubmodPak(string pakPath, string overrideText)
    {
        AmpBackupService.EnsureBackup(pakPath);

        using var tempDir = new TempDirectoryManager();
        var extractDir = tempDir.CreateSubDirectory("submod_extract");

        // Extract from the pristine backup, same as AMP: patching the already-patched pak would
        // stack a fresh copy of the overrides on top of the previous one on every run.
        var extractSource = AmpBackupService.HasBackup(pakPath)
            ? AmpBackupService.GetBackupPath(pakPath)
            : pakPath;
        PakReader.ExtractAll(extractSource, extractDir);

        var statsDir = FindDirectory(extractDir, Path.Combine("Stats", "Generated", "Data"));
        if (statsDir == null) return false;

        var statFiles = Directory.GetFiles(statsDir, "*.txt")
            .Where(f => !Path.GetFileName(f)
                .Equals("ZZZ_ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (statFiles.Length == 0) return false;

        // Last file alphabetically is the last one BG3 loads for this mod, and appending puts our
        // entries behind everything in it — including the submod's own re-declarations.
        File.AppendAllText(statFiles[^1], "\n" + overrideText);

        // Marker file: AmpBackupService reads it to tell a patched pak from a freshly updated one.
        File.WriteAllText(Path.Combine(statsDir, "ZZZ_ParaTool_Overrides.txt"), "// Patched by ParaTool\n");

        var tempPakPath = pakPath + ".tmp";
        PakWriter.CreatePak(extractDir, tempPakPath);
        File.Delete(pakPath);
        File.Move(tempPakPath, pakPath);
        return true;
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
