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

        if (enabledModItems.Count == 0 && modifiedAmpItems.Count == 0)
        {
            // Check if any AMP items were disabled (need re-patch to remove them)
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
            var artifactCount = await Task.Run(() => ApplyArtifacts(extractDir, statsDir, ampPakPath), ct);

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
                ItemsPatched = enabledModItems.Count + modifiedAmpItems.Count + artifactCount
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
    private static int ApplyArtifacts(string extractDir, string? statsDir, string ampPakPath)
    {
        var artifacts = ArtifactStore.LoadAll();
        if (artifacts.Count == 0 || statsDir == null) return 0;

        // Load existing stat IDs to distinguish overrides vs new items
        var existingStatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var statFile in Directory.GetFiles(statsDir, "*.txt"))
        {
            var text = File.ReadAllText(statFile);
            var parsed = StatsParser.Parse(text);
            foreach (var entry in parsed)
                existingStatIds.Add(entry.Name);
        }

        var overrideStats = new StringBuilder();
        var newStats = new StringBuilder();
        var allLocaEntries = new Dictionary<string, List<(string handle, string xmlText)>>(StringComparer.OrdinalIgnoreCase);
        int count = 0;

        foreach (var art in artifacts)
        {
            var compiled = ArtifactCompiler.Compile(art);
            bool isOverride = existingStatIds.Contains(art.StatId);

            if (isOverride)
            {
                // Override: apply compiled stats as in-place modification
                overrideStats.Append(compiled.StatsText);
            }
            else
            {
                // New item: append to stats + schedule for TT insertion
                newStats.Append(compiled.StatsText);
                // TT integration handled via virtual mod in patcher UI
            }

            // Merge loca entries
            foreach (var (lang, entries) in compiled.LocalizationEntries)
            {
                if (!allLocaEntries.ContainsKey(lang))
                    allLocaEntries[lang] = [];
                allLocaEntries[lang].AddRange(entries);
            }

            // Icon files
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
                }
            }

            // Append passives/statuses/spells from overrides to last stat file
            var nonItemOverrides = new StringBuilder();
            foreach (var entry in overrideParsed)
            {
                if (entry.Type == "Armor" || entry.Type == "Weapon") continue;
                nonItemOverrides.AppendLine($"new entry \"{entry.Name}\"");
                nonItemOverrides.AppendLine($"type \"{entry.Type}\"");
                if (entry.Using != null) nonItemOverrides.AppendLine($"using \"{entry.Using}\"");
                foreach (var (k, v) in entry.Data) nonItemOverrides.AppendLine($"data \"{k}\" \"{v}\"");
                nonItemOverrides.AppendLine();
            }

            if (nonItemOverrides.Length > 0)
            {
                var lastFile = statFiles[^1];
                File.AppendAllText(lastFile, "\n" + nonItemOverrides);
            }
        }

        // Append new item stats to last stat file
        if (newStats.Length > 0 && statFiles.Length > 0)
        {
            var lastFile = statFiles[^1];
            File.AppendAllText(lastFile, "\n" + newStats);
        }

        // TreasureTable for new items is handled by the main TT patching step
        // (artifacts appear as items in the virtual "My Artifacts" mod)

        // Write loca XML entries
        if (allLocaEntries.Count > 0)
        {
            WriteLocaEntries(extractDir, allLocaEntries);
        }

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
