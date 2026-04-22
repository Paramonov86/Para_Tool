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
    /// Non-fatal warnings collected from ArtifactCompiler while building each artifact's
    /// stats text (placeholder tokens, missing status references, auto-generated passive
    /// names). Patching still succeeds; the UI should surface these to the user.
    /// </summary>
    public List<string> Warnings { get; init; } = [];
}

public sealed class AmpPatcher
{
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

        var enabledModItems = mods
            .Where(m => !string.IsNullOrEmpty(m.PakPath)) // Exclude virtual mods (artifacts handled by ApplyArtifacts)
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
        if (enabledModItems.Count == 0 && modifiedAmpItems.Count == 0 && !hasArtifacts)
        {
            var disabledAmpItems = ampMod?.Items.Where(i => !i.Enabled && i.IsModified).ToList()
                ?? new List<ItemEntry>();
            if (disabledAmpItems.Count == 0)
                return new PatchResult { Success = false, Error = "No items selected." };
        }

        var modsWithEnabledItems = mods
            .Where(m => m.Items.Any(i => i.Enabled))
            .Where(m => !m.IsAmp)
            .Where(m => !string.IsNullOrEmpty(m.PakPath)) // Exclude virtual mods (artifacts)
            .ToList();

        using var tempDir = new TempDirectoryManager();
        var extractDir = tempDir.CreateSubDirectory("amp_extract");

        try
        {
            // Step 0: Ensure backup exists before modifying anything
            progress?.Report(new PatchProgress { Stage = "Creating backup...", Percent = 5 });
            await Task.Run(() => AmpBackupService.EnsureBackup(ampPakPath), ct);

            // Step 1: Extract AMP pak
            progress?.Report(new PatchProgress { Stage = "Extracting AMP pak...", Percent = 10 });
            await Task.Run(() => PakReader.ExtractAll(ampPakPath, extractDir), ct);

            // Step 2: Find and patch TreasureTable.txt (in-place insertion)
            progress?.Report(new PatchProgress { Stage = "Patching loot lists...", Percent = 30 });
            var ttPath = FindFile(extractDir, "TreasureTable.txt");
            if (ttPath == null)
                return new PatchResult { Success = false, Error = "TreasureTable.txt not found in AMP pak." };

            // Use stored original TT if available, otherwise store current as original
            string ttText;
            if (OriginalTtStore.HasValidOriginal(ampPakPath))
            {
                ttText = OriginalTtStore.Load()!;
            }
            else
            {
                ttText = await File.ReadAllTextAsync(ttPath, ct);
                OriginalTtStore.Store(ampPakPath, ttText);
            }

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
            var artifactCount = await Task.Run(() => ApplyArtifacts(extractDir, statsDir, ampPakPath, artifactWarnings), ct);

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

            progress?.Report(new PatchProgress { Stage = "Done!", Percent = 100 });

            return new PatchResult
            {
                Success = true,
                ItemsPatched = enabledModItems.Count + modifiedAmpItems.Count + artifactCount,
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
    /// - AMP items: modify entries in-place within their source stat files
    /// - Mod items: append skeleton entries to the last stat file
    /// Creates a marker file so we know the pak was patched.
    /// </summary>
    private static void ApplyStatOverrides(
        string statsDir,
        IReadOnlyList<ItemEntry> ampItems,
        IReadOnlyList<ItemEntry> modItems)
    {
        // Build override fields for AMP items
        var ampMods = new Dictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in ampItems)
        {
            var fields = StatsOverrideGenerator.ComputeFields(item);
            if (fields != null)
                ampMods[item.StatId] = fields;
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

        // Step A: Modify AMP items in-place across stat files
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

        // Step B: Generate skeleton entries for mod items
        var skeletonText = StatsOverrideGenerator.GenerateSkeletonEntries(modItems);

        // Also generate skeletons for any unresolved AMP items (not found in any file)
        if (unresolved.Count > 0)
        {
            var unresolvedItems = ampItems.Where(i => unresolved.Contains(i.StatId)).ToList();
            skeletonText += StatsOverrideGenerator.GenerateSkeletonEntries(unresolvedItems);
        }

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
    private static int ApplyArtifacts(string extractDir, string? statsDir, string ampPakPath, List<string>? warnings = null)
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
            return 0;
        }

        var overrideStats = new StringBuilder();
        var newStats = new StringBuilder();
        var allLocaEntries = new Dictionary<string, List<(string handle, string xmlText)>>(StringComparer.OrdinalIgnoreCase);
        var customIconStatIds = new List<string>();
        var newArtifacts = new List<ArtifactDefinition>();
        var overrideArtifacts = new List<ArtifactDefinition>();
        int count = 0;

        foreach (var art in artifacts)
        {
            // Override = same StatId as UsingBase (modifying existing item)
            // New = different StatId (creating new item, even if leftover from previous patch exists in stats)
            bool isOverride = art.StatId.Equals(art.UsingBase, StringComparison.OrdinalIgnoreCase);
            log.AppendLine($"  {art.StatId}: isOverride={isOverride} (UsingBase={art.UsingBase})");
            var compiled = ArtifactCompiler.Compile(art, isOverride);
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
            var overrideParsed = StatsParser.Parse(overrideStats.ToString());
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
                var compiled = ArtifactCompiler.Compile(art, false);
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
            PatchRootTemplates(extractDir, newArtifacts, overrideArtifacts, ampPakPath);

        // Write loca XML entries
        if (allLocaEntries.Count > 0)
        {
            WriteLocaEntries(extractDir, allLocaEntries);
        }

        log.AppendLine($"Done: {count} artifacts, {newArtifacts.Count} new, {overrideArtifacts.Count} overrides");
        File.WriteAllText(logPath, log.ToString());
        return count;
    }

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
        string ampPakPath)
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
            foreach (var art in newArtifacts)
            {
                var lsfPath = Path.Combine(rtDir, $"{art.TemplateUuid}.lsf");
                File.AppendAllText(rtLog, $"  Creating: {lsfPath} (ParentTemplate={art.ParentTemplateUuid})\n");
                CreateTemplateLsf(lsfPath, art, ampPakPath);
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
    private static void CreateTemplateLsf(string lsfPath, ArtifactDefinition art, string ampPakPath)
    {
        // Find parent template LSF to clone from
        var rtDir = Path.GetDirectoryName(lsfPath)!;
        var parentLsfPath = Path.Combine(rtDir, $"{art.ParentTemplateUuid}.lsf");

        LSLib.Resource resource;
        LSLib.Node? goNode = null;

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

            // Try vanilla Shared.pak as last resort
            if (goNode == null)
            {
                goNode = FindTemplateInSharedPak(art.ParentTemplateUuid, ampPakPath);
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

        // Ensure EquipmentTypeID is set — prevents items ending up in wrong slot
        if (!goNode.Attributes.ContainsKey("EquipmentTypeID"))
        {
            // Map stat type to BG3 EquipmentTypeID UUID
            var eqTypeId = GetEquipmentTypeId(art.StatType, art.UsingBase);
            if (eqTypeId != null)
            {
                goNode.Attributes["EquipmentTypeID"] = new LSLib.NodeAttribute(LSLib.AttributeType.FixedString)
                    { Value = eqTypeId };
            }
        }

        using var outFs = File.Create(lsfPath);
        var writer = new LSLib.LSFWriter(outFs);
        writer.Write(resource);
    }

    /// <summary>
    /// Get BG3 EquipmentTypeID UUID based on stat type.
    /// These are vanilla BG3 equipment slot UUIDs from Shared.pak.
    /// </summary>
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

    private static LSLib.Node? FindTemplateInSharedPak(string uuid, string ampPakPath)
    {
        try
        {
            // Shared.pak is in the same Data directory as AMP pak
            var dataDir = Path.GetDirectoryName(ampPakPath);
            if (dataDir == null) return null;
            var sharedPak = Path.Combine(dataDir, "Shared.pak");
            if (!File.Exists(sharedPak)) return null;

            // Find the RootTemplates/_merged.lsf entry in Shared.pak
            using var pakStream = File.OpenRead(sharedPak);
            var header = PakReader.ReadHeader(pakStream);
            var entries = PakReader.ReadFileList(pakStream, header);
            var rtEntry = entries.FirstOrDefault(e =>
                e.Path.Contains("RootTemplates/_merged.lsf", StringComparison.OrdinalIgnoreCase));
            if (rtEntry.Path == null) return null;

            var lsfData = PakReader.ExtractFileData(pakStream, rtEntry);
            using var ms = new System.IO.MemoryStream(lsfData);
            var reader = new LSLib.LSFReader(ms);
            var res = reader.Read();

            if (res.Regions.TryGetValue("Templates", out var region) &&
                region.Children.TryGetValue("GameObjects", out var nodes))
            {
                return nodes.FirstOrDefault(n =>
                    n.Attributes.TryGetValue("MapKey", out var mk) &&
                    uuid.Equals(mk.Value?.ToString(), StringComparison.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex)
        {
            Services.AppLogger.Warn($"FindTemplateInSharedPak failed: {ex.Message}");
        }
        return null;
    }

    private static string? GetEquipmentTypeId(string statType, string? usingBase)
    {
        // Weapons always go in melee/ranged weapon slot — BG3 determines from template
        if (statType == "Weapon")
            return "2ef8e830-1759-4335-b4db-e498e41b1afe"; // Melee Main Weapon

        // Armor — determine slot from base name patterns
        if (statType == "Armor" && usingBase != null)
        {
            var b = usingBase.ToUpperInvariant();
            if (b.Contains("AMULET") || b.Contains("NECK"))
                return "e4133b20-a10f-4a97-9e2e-4e07e1c7a9e0"; // Amulet
            if (b.Contains("RING"))
                return "af06a192-1a52-41fb-a46f-ae7a1010cd15"; // Ring
            if (b.Contains("CLOAK") || b.Contains("MANTLE"))
                return "3e2d74e3-3631-4a22-b71b-95565dbc13e9"; // Cloak
            if (b.Contains("HELMET") || b.Contains("HAT") || b.Contains("CIRCLET") || b.Contains("CROWN"))
                return "aedd3574-39a3-4b47-a20a-34e4f90c02fd"; // Helmet
            if (b.Contains("GLOVES") || b.Contains("GAUNTLET"))
                return "6d37abad-5eb8-4e98-9963-e4e514d2959a"; // Gloves
            if (b.Contains("BOOTS") || b.Contains("SHOES"))
                return "29a5e2b2-0e67-4355-8e40-469aabd16498"; // Boots
            if (b.Contains("SHIELD"))
                return "a3ce6f42-1fd3-4880-a330-5765f7a35c24"; // Shield
            // Chest armor (default for ARM_ prefix)
            return "6a084c55-76e8-4528-9510-3b63ec290cd0"; // Chest
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
