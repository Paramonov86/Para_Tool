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
            .Where(m => !m.IsAmp) // Exclude AMP from dependencies
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
                ItemsPatched = enabledModItems.Count + modifiedAmpItems.Count
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
