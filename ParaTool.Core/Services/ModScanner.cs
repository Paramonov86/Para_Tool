using ParaTool.Core.Models;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Services;

public sealed class ScanResult
{
    public List<ModInfo> Mods { get; init; } = new();
    public ModInfo? AmpMod { get; init; }
    public string? AmpPakPath { get; init; }
    public string? Error { get; init; }
    public int CleanedOldPaks { get; init; }

    /// <summary>
    /// Combined stats resolver (vanilla + AMP + mods) for full field resolution.
    /// </summary>
    public StatsResolver? Resolver { get; init; }

    /// <summary>
    /// All localization handle → text mappings from scanned paks (scan language + English).
    /// </summary>
    public Dictionary<string, string> LocaMap { get; init; } = new();

    /// <summary>
    /// Paths to all scanned pak files (AMP + mods) for on-demand loca loading.
    /// </summary>
    public string[] PakPaths { get; init; } = [];

    /// <summary>
    /// Handle → owning pak path. Ensures mod-specific loca text wins over other mods.
    /// </summary>
    public Dictionary<string, string> HandleOwnership { get; init; } = new();
}

public sealed class ScanProgress
{
    public int TotalPaks { get; init; }
    public int ScannedPaks { get; init; }
    public int ModsFound { get; init; }
    public string? Stage { get; init; }
    public int Percent { get; init; }
}

public sealed class ModScanner
{
    private readonly VanillaDatabase _vanillaDb;

    public ModScanner(VanillaDatabase vanillaDb)
    {
        _vanillaDb = vanillaDb;
    }

    public async Task<ScanResult> ScanAsync(string modsFolder, string langCode = "en",
        IProgress<ScanProgress>? progress = null, CancellationToken ct = default)
    {
        var pakFiles = Directory.GetFiles(modsFolder, "*.pak");
        if (pakFiles.Length == 0)
            return new ScanResult { Error = "No .pak files found in Mods folder." };

        // Find AMP pak
        var ampPaks = pakFiles.Where(p =>
            Path.GetFileName(p).StartsWith("REL_Full_Ancient_", StringComparison.OrdinalIgnoreCase)).ToArray();

        if (ampPaks.Length == 0)
            return new ScanResult { Error = "AMP pak not found. Install Ancient Mega Pack first." };

        // Multiple AMP paks: keep the largest (latest version), clean the rest
        string ampPakPath;
        int cleanedOldPaks = 0;
        if (ampPaks.Length > 1)
        {
            ampPakPath = ampPaks.OrderByDescending(p => new FileInfo(p).Length).First();
            cleanedOldPaks = AmpBackupService.CleanOldAmpPaks(modsFolder, ampPakPath);
            // Re-read after cleanup
            pakFiles = Directory.GetFiles(modsFolder, "*.pak");
        }
        else
        {
            ampPakPath = ampPaks[0];
        }
        var nonAmpPaks = pakFiles.Where(p => p != ampPakPath).ToArray();

        // Extract AMP integration info — items already in AMP TT are marked as integrated in mods
        progress?.Report(new ScanProgress { Stage = "ScanAMP", Percent = 0 });
        var (ampWhitelist, ampRarities, ampThemes) = ExtractAmpIntegrationInfo(ampPakPath);

        var mods = new List<ModInfo>();
        int scanned = 0;

        progress?.Report(new ScanProgress
        {
            Stage = "ScanMods", Percent = 5,
            TotalPaks = nonAmpPaks.Length, ScannedPaks = 0, ModsFound = 0
        });

        await Parallel.ForEachAsync(nonAmpPaks, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        }, async (pakPath, innerCt) =>
        {
            var mod = await Task.Run(() => ScanPak(pakPath, ampWhitelist, ampRarities, ampThemes), innerCt);
            var count = Interlocked.Increment(ref scanned);

            if (mod != null)
            {
                lock (mods) mods.Add(mod);
            }

            progress?.Report(new ScanProgress
            {
                TotalPaks = nonAmpPaks.Length,
                ScannedPaks = count,
                ModsFound = mods.Count,
                Stage = "ScanMods",
                Percent = nonAmpPaks.Length > 0 ? 5 + count * 35 / nonAmpPaks.Length : 40
            });
        });

        mods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // Carry forward final scan counts for all subsequent progress reports
        int finalScanned = nonAmpPaks.Length;
        int finalFound = mods.Count;

        // Scan AMP pak itself for editable items
        progress?.Report(new ScanProgress { Stage = "ScanAMP", Percent = 42,
            TotalPaks = finalScanned, ScannedPaks = finalScanned, ModsFound = finalFound });
        var ampMod = ScanAmpPak(ampPakPath);

        // Resolve display names from PAK localization files
        progress?.Report(new ScanProgress { Stage = "ResolveNames", Percent = 55,
            TotalPaks = finalScanned, ScannedPaks = finalScanned, ModsFound = finalFound });
        Parsing.StatsResolver? combinedResolver = null;
        Dictionary<string, string>? locaMap = null;
        Dictionary<string, string>? handleOwnershipMap = null;
        await Task.Run(() =>
        {
            var result = ResolveDisplayNames(ampPakPath, ampMod, mods, nonAmpPaks, langCode, _vanillaDb.Resolver, progress);
            combinedResolver = result.resolver;
            locaMap = result.locaMap;
            handleOwnershipMap = result.handleOwnership;
        }, ct);

        // Apply vanilla overrides: mark AMP items as modified by other mods
        if (ampMod != null)
        {
            foreach (var mod in mods)
            {
                if (mod.VanillaOverrides == null) continue;
                foreach (var statId in mod.VanillaOverrides)
                {
                    var ampItem = ampMod.Items.Find(i => i.StatId.Equals(statId, StringComparison.OrdinalIgnoreCase));
                    if (ampItem != null)
                        ampItem.ModifiedBy.Add(mod.Name);
                }
            }
        }

        progress?.Report(new ScanProgress { Stage = "Done", Percent = 100,
            TotalPaks = finalScanned, ScannedPaks = finalScanned, ModsFound = finalFound });

        return new ScanResult
        {
            Mods = mods,
            AmpMod = ampMod,
            AmpPakPath = ampPakPath,
            CleanedOldPaks = cleanedOldPaks,
            Resolver = combinedResolver,
            LocaMap = locaMap ?? new(),
            PakPaths = nonAmpPaks.Concat(new[] { ampPakPath }).ToArray(), // AMP last = highest priority
            HandleOwnership = handleOwnershipMap ?? new()
        };
    }

    private static readonly HashSet<string> KnownThemes = new(StringComparer.OrdinalIgnoreCase)
    {
        "Swamp", "Aquatic", "Shadowfell", "Arcane", "Celestial",
        "Nature", "Destructive", "War", "Psionic", "Primal"
    };

    private static readonly Dictionary<string, string> TableRarityMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Uncommon"] = "Uncommon", ["Rare"] = "Rare",
        ["Epic"] = "VeryRare", ["Legendary"] = "Legendary"
    };

    /// <summary>
    /// Extracts AMP integration info from the AMP pak's TreasureTable:
    /// whitelist (StatIds in REL_All tables), per-item rarity, per-item themes.
    /// </summary>
    private static (HashSet<string> whitelist, Dictionary<string, string> rarities, Dictionary<string, List<string>> themes)
        ExtractAmpIntegrationInfo(string ampPakPath)
    {
        try
        {
            using var fs = File.OpenRead(ampPakPath);
            var header = PakReader.ReadHeader(fs);
            var entries = PakReader.ReadFileList(fs, header);
            var ttEntry = entries.FirstOrDefault(e =>
                e.Path.EndsWith("TreasureTable.txt", StringComparison.OrdinalIgnoreCase));
            if (ttEntry.Path == null) return (new(), new(), new());
            var ttData = PakReader.ExtractFileData(fs, ttEntry);
            var (whitelist, themes, rarities) = ParseTreasureTableInfo(ttData);
            return (whitelist, rarities, themes);
        }
        catch (Exception ex) { AppLogger.Error("ScanTreasureTable failed", ex); return (new(), new(), new()); }
    }

    private ModInfo? ScanAmpPak(string ampPakPath)
    {
        try
        {
            using var fs = File.OpenRead(ampPakPath);
            var header = PakReader.ReadHeader(fs);
            var entries = PakReader.ReadFileList(fs, header);

            var metaEntry = entries.FirstOrDefault(e =>
                e.Path.EndsWith("meta.lsx", StringComparison.OrdinalIgnoreCase));
            if (metaEntry.Path == null) return null;

            var metaData = PakReader.ExtractFileData(fs, metaEntry);
            var modInfo = MetaLsxParser.Parse(metaData, ampPakPath);
            if (modInfo == null) return null;

            // Parse TreasureTable → whitelist (REL_All items) + themes per item.
            // If the pak was patched by us (contains ZZZ_ParaTool_Overrides.txt), use the
            // stored original TT so mod items don't appear as AMP items.
            // If the pak is clean (no overrides file), read TT from pak directly —
            // this handles AMP updates where the stored original would be stale.
            bool isPatchedByUs = entries.Any(e =>
                e.Path.EndsWith("ZZZ_ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase) ||
                e.Path.EndsWith("ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase));

            HashSet<string> whitelist;
            Dictionary<string, List<string>> themeMap;

            if (isPatchedByUs && OriginalTtStore.HasValidOriginal(ampPakPath))
            {
                var originalText = OriginalTtStore.Load();
                if (originalText != null)
                {
                    var origBytes = System.Text.Encoding.UTF8.GetBytes(originalText);
                    (whitelist, themeMap, _) = ParseTreasureTableInfo(origBytes);
                }
                else
                {
                    var ttEntry = entries.FirstOrDefault(e =>
                        e.Path.EndsWith("TreasureTable.txt", StringComparison.OrdinalIgnoreCase));
                    if (ttEntry.Path == null) return null;
                    var ttData = PakReader.ExtractFileData(fs, ttEntry);
                    (whitelist, themeMap, _) = ParseTreasureTableInfo(ttData);
                }
            }
            else
            {
                var ttEntry = entries.FirstOrDefault(e =>
                    e.Path.EndsWith("TreasureTable.txt", StringComparison.OrdinalIgnoreCase));
                if (ttEntry.Path == null) return null;
                var ttData = PakReader.ExtractFileData(fs, ttEntry);
                (whitelist, themeMap, _) = ParseTreasureTableInfo(ttData);
            }
            if (whitelist.Count == 0) return null;

            // Build merged resolver: vanilla + AMP stats
            var resolver = new StatsResolver();
            foreach (var kvp in _vanillaDb.Resolver.AllEntries)
                resolver.AddEntries(new[] { kvp.Value });

            var statFiles = entries.Where(e =>
                e.Path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase) &&
                e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                !e.Path.EndsWith("ZZZ_ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase) &&
                !e.Path.EndsWith("ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase)).ToList();

            // Pass 1: Merge all entries with the same name across stat files.
            // BG3 stats work as a cascade: later definitions supplement earlier ones.
            // After PakWriter repacks the pak, file order changes (alphabetical vs original),
            // so we must merge across files instead of relying on load order.
            var mergedEntries = new Dictionary<string, StatsEntry>(StringComparer.OrdinalIgnoreCase);

            foreach (var statFile in statFiles)
            {
                var data = PakReader.ExtractFileData(fs, statFile);
                var text = System.Text.Encoding.UTF8.GetString(data);
                var parsed = StatsParser.Parse(text);

                foreach (var entry in parsed)
                {
                    if (mergedEntries.TryGetValue(entry.Name, out var prev))
                    {
                        // Don't let StatusData overwrite Armor/Weapon entries
                        if (entry.Type != "Armor" && entry.Type != "Weapon"
                            && (prev.Type == "Armor" || prev.Type == "Weapon"))
                            continue;

                        // Merge data: previous base + new overrides
                        var isSkel = entry.Data.Count > 0 && entry.Data.Values.All(string.IsNullOrEmpty);
                        var mergedData = new Dictionary<string, string>(prev.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in entry.Data)
                        {
                            if (isSkel && string.IsNullOrEmpty(kvp.Value)
                                && mergedData.TryGetValue(kvp.Key, out var existing2)
                                && !string.IsNullOrEmpty(existing2))
                                continue;
                            mergedData[kvp.Key] = kvp.Value;
                        }

                        // Self-referencing Using is a BG3 skeleton pattern — keep the
                        // correct Using from the earlier definition instead
                        var mergedUsing = entry.Using;
                        if (mergedUsing != null && mergedUsing.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                            mergedUsing = prev.Using;

                        mergedEntries[entry.Name] = new StatsEntry
                        {
                            Name = entry.Name,
                            Type = entry.Type,
                            Using = mergedUsing ?? prev.Using,
                            Data = mergedData
                        };
                    }
                    else
                    {
                        mergedEntries[entry.Name] = entry;
                    }
                }
            }

            // Pass 2: Add merged entries to resolver with vanilla merge
            foreach (var modEntry in mergedEntries.Values)
            {
                // Don't let non-Armor/Weapon overwrite Armor/Weapon from vanilla
                var existing = resolver.Get(modEntry.Name);
                if (existing != null && modEntry.Type != "Armor" && modEntry.Type != "Weapon"
                    && (existing.Type == "Armor" || existing.Type == "Weapon"))
                    continue;

                // Fix self-referencing Using: replace with the Using from vanilla DB
                // or from the resolver (which has vanilla entries loaded first)
                var fixedUsing = modEntry.Using;
                if (fixedUsing != null && fixedUsing.Equals(modEntry.Name, StringComparison.OrdinalIgnoreCase))
                {
                    fixedUsing = existing?.Using; // Try resolver's existing entry (vanilla)
                }

                var vanilla = _vanillaDb.Resolver.Get(modEntry.Name);
                if (vanilla != null)
                {
                    var mergedUsing = fixedUsing;
                    if (mergedUsing != null && mergedUsing.Equals(modEntry.Name, StringComparison.OrdinalIgnoreCase))
                        mergedUsing = vanilla.Using;

                    var effectiveUsing = mergedUsing ?? vanilla.Using;
                    bool sameChain = string.Equals(effectiveUsing, vanilla.Using, StringComparison.OrdinalIgnoreCase);

                    var isSkel2 = modEntry.Data.Count > 0 && modEntry.Data.Values.All(string.IsNullOrEmpty);
                    Dictionary<string, string> finalData;
                    if (sameChain)
                    {
                        finalData = new Dictionary<string, string>(vanilla.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in modEntry.Data)
                        {
                            if (isSkel2 && string.IsNullOrEmpty(kvp.Value)
                                && finalData.TryGetValue(kvp.Key, out var existing2)
                                && !string.IsNullOrEmpty(existing2))
                                continue;
                            finalData[kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        finalData = modEntry.Data;
                    }

                    resolver.AddEntries(new[] { new StatsEntry
                    {
                        Name = modEntry.Name,
                        Type = modEntry.Type,
                        Using = effectiveUsing,
                        Data = finalData
                    }});
                }
                else
                {
                    resolver.AddEntries(new[] { new StatsEntry
                    {
                        Name = modEntry.Name,
                        Type = modEntry.Type,
                        Using = fixedUsing,
                        Data = modEntry.Data
                    }});
                }
            }

            // Resolve only items that exist in REL_All tables
            var items = new List<ItemEntry>();
            foreach (var statId in whitelist)
            {
                var entry = resolver.Get(statId);
                if (entry == null) continue;
                if (entry.Type != "Armor" && entry.Type != "Weapon") continue;

                var item = ResolveItem(entry, resolver);
                if (item == null) continue;

                item.IsAmpItem = true;
                if (themeMap.TryGetValue(statId, out var themes))
                    item.DetectedThemes = themes;

                items.Add(item);
            }

            if (items.Count == 0) return null;

            return new ModInfo
            {
                Name = modInfo.Name,
                UUID = modInfo.UUID,
                Folder = modInfo.Folder,
                PakPath = modInfo.PakPath,
                Version64 = modInfo.Version64,
                IsAmp = true,
                Items = items
            };
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses TreasureTable.txt → whitelist of StatIds in REL_All_* tables + theme map + rarity map.
    /// </summary>
    private static (HashSet<string> whitelist, Dictionary<string, List<string>> themes, Dictionary<string, string> rarities)
        ParseTreasureTableInfo(byte[] ttData)
    {
        var text = System.Text.Encoding.UTF8.GetString(ttData);
        var whitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var themes = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var rarities = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        bool inRelTable = false;
        bool isAllTable = false;
        string? currentTheme = null;
        string? currentAllRarity = null;

        foreach (var rawLine in text.Split('\n'))
        {
            var line = rawLine.TrimStart();

            if (line.StartsWith("new treasuretable \""))
            {
                inRelTable = false;
                isAllTable = false;
                currentTheme = null;
                currentAllRarity = null;

                var name = ExtractQuoted(line);
                if (!name.StartsWith("REL_", StringComparison.OrdinalIgnoreCase))
                    continue;

                inRelTable = true;
                var rest = name[4..];

                if (rest.StartsWith("All_", StringComparison.OrdinalIgnoreCase))
                {
                    isAllTable = true;
                    // Extract rarity from table name: REL_All_Uncommon → Uncommon
                    var tableRarity = rest[4..];
                    if (TableRarityMap.TryGetValue(tableRarity, out var mapped))
                        currentAllRarity = mapped;
                    else
                        currentAllRarity = tableRarity;
                }
                else
                {
                    var idx = rest.IndexOf('_');
                    if (idx > 0)
                    {
                        var suffix = rest[(idx + 1)..];
                        if (KnownThemes.Contains(suffix))
                            currentTheme = suffix;
                    }
                }
                continue;
            }

            if (!inRelTable) continue;
            if (!line.StartsWith("object category \"I_")) continue;

            int start = line.IndexOf("\"I_") + 3;
            int end = line.IndexOf('"', start);
            if (end <= start) continue;
            var statId = line[start..end];

            if (isAllTable)
            {
                whitelist.Add(statId);
                if (currentAllRarity != null)
                    rarities[statId] = currentAllRarity;
            }

            if (currentTheme != null)
            {
                if (!themes.TryGetValue(statId, out var list))
                {
                    list = new List<string>();
                    themes[statId] = list;
                }
                if (!list.Contains(currentTheme))
                    list.Add(currentTheme);
            }
        }

        return (whitelist, themes, rarities);
    }

    private static string ExtractQuoted(string line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return "";
        int second = line.IndexOf('"', first + 1);
        return second < 0 ? "" : line[(first + 1)..second];
    }

    private ModInfo? ScanPak(string pakPath,
        HashSet<string>? ampWhitelist = null,
        Dictionary<string, string>? ampRarities = null,
        Dictionary<string, List<string>>? ampThemes = null)
    {
        try
        {
            using var fs = File.OpenRead(pakPath);
            var header = PakReader.ReadHeader(fs);
            var entries = PakReader.ReadFileList(fs, header);

            // Find meta.lsx
            var metaEntry = entries.FirstOrDefault(e =>
                e.Path.EndsWith("meta.lsx", StringComparison.OrdinalIgnoreCase));
            if (metaEntry.Path == null) return null;

            var metaData = PakReader.ExtractFileData(fs, metaEntry);
            var modInfo = MetaLsxParser.Parse(metaData, pakPath);
            if (modInfo == null) return null;

            // Find Stats .txt files
            var statFiles = entries.Where(e =>
                e.Path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase) &&
                e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)).ToList();

            if (statFiles.Count == 0) return null;

            var resolver = new StatsResolver();

            // Add vanilla entries for resolution
            foreach (var kvp in _vanillaDb.Resolver.AllEntries)
                resolver.AddEntries(new[] { kvp.Value });

            // Pass 1: Parse mod stats and merge same-name entries across files
            var mergedModEntries = new Dictionary<string, StatsEntry>(StringComparer.OrdinalIgnoreCase);
            foreach (var statFile in statFiles)
            {
                var data = PakReader.ExtractFileData(fs, statFile);
                var text = System.Text.Encoding.UTF8.GetString(data);
                var parsed = StatsParser.Parse(text);

                foreach (var entry in parsed)
                {
                    if (mergedModEntries.TryGetValue(entry.Name, out var prev))
                    {
                        if (entry.Type != "Armor" && entry.Type != "Weapon"
                            && (prev.Type == "Armor" || prev.Type == "Weapon"))
                            continue;

                        var isSkel3 = entry.Data.Count > 0 && entry.Data.Values.All(string.IsNullOrEmpty);
                        var mergedData = new Dictionary<string, string>(prev.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in entry.Data)
                        {
                            if (isSkel3 && string.IsNullOrEmpty(kvp.Value)
                                && mergedData.TryGetValue(kvp.Key, out var existing2)
                                && !string.IsNullOrEmpty(existing2))
                                continue;
                            mergedData[kvp.Key] = kvp.Value;
                        }

                        // Self-referencing Using is a BG3 rebalance pattern — keep prev Using
                        var mergedUsing = entry.Using;
                        if (mergedUsing != null && mergedUsing.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                            mergedUsing = prev.Using;

                        mergedModEntries[entry.Name] = new StatsEntry
                        {
                            Name = entry.Name,
                            Type = entry.Type,
                            Using = mergedUsing ?? prev.Using,
                            Data = mergedData
                        };
                    }
                    else
                    {
                        mergedModEntries[entry.Name] = entry;
                    }
                }
            }

            // Pass 2: Merge with vanilla and add to resolver.
            // Mod data overrides vanilla, but vanilla data fills gaps.
            // This preserves vanilla Slot/ArmorType in skeleton entries that mods redefine.
            foreach (var modEntry in mergedModEntries.Values)
            {
                var vanilla = _vanillaDb.Resolver.Get(modEntry.Name);
                if (vanilla != null)
                {
                    // Fix self-referencing Using (broken skeleton pattern)
                    var mergedUsing = modEntry.Using;
                    if (mergedUsing != null && mergedUsing.Equals(modEntry.Name, StringComparison.OrdinalIgnoreCase))
                        mergedUsing = vanilla.Using;

                    var effectiveUsing = mergedUsing ?? vanilla.Using;

                    // Only merge vanilla data when inheritance chain is unchanged.
                    // If the mod changes Using, vanilla data (Slot, ArmorType, etc.) would
                    // shadow values from the new chain, causing misclassification.
                    bool sameChain = string.Equals(effectiveUsing, vanilla.Using, StringComparison.OrdinalIgnoreCase);

                    var isSkel2 = modEntry.Data.Count > 0 && modEntry.Data.Values.All(string.IsNullOrEmpty);
                    Dictionary<string, string> finalData;
                    if (sameChain)
                    {
                        finalData = new Dictionary<string, string>(vanilla.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in modEntry.Data)
                        {
                            if (isSkel2 && string.IsNullOrEmpty(kvp.Value)
                                && finalData.TryGetValue(kvp.Key, out var existing2)
                                && !string.IsNullOrEmpty(existing2))
                                continue;
                            finalData[kvp.Key] = kvp.Value;
                        }
                    }
                    else
                    {
                        finalData = modEntry.Data;
                    }

                    resolver.AddEntries(new[] { new StatsEntry
                    {
                        Name = modEntry.Name,
                        Type = modEntry.Type,
                        Using = effectiveUsing,
                        Data = finalData
                    }});
                }
                else
                {
                    resolver.AddEntries(new[] { modEntry });
                }
            }

            // Filter: keep only Armor/Weapon entries from this mod that are NOT vanilla.
            // Vanilla overrides from mods are tracked as "modified by" on AMP items instead.
            var items = new List<ItemEntry>();
            var vanillaOverrides = new List<string>(); // StatIds of vanilla items this mod overrides
            foreach (var entry in mergedModEntries.Values)
            {
                if (entry.Type != "Armor" && entry.Type != "Weapon") continue;
                if (_vanillaDb.Resolver.AllEntries.ContainsKey(entry.Name))
                {
                    // Track vanilla override for later marking on AMP items
                    vanillaOverrides.Add(entry.Name);
                    continue;
                }

                var item = ResolveItem(entry, resolver);
                if (item == null) continue;

                // If item is already in AMP loot tables, apply current AMP integration state
                if (ampWhitelist != null && ampWhitelist.Contains(entry.Name))
                {
                    item.IsIntegrated = true;
                    if (ampRarities != null && ampRarities.TryGetValue(entry.Name, out var ampRarity))
                        item.DetectedRarity = ampRarity;
                    if (ampThemes != null && ampThemes.TryGetValue(entry.Name, out var themes))
                        item.DetectedThemes = themes;
                }

                items.Add(item);
            }

            // Mark AMP items as modified by this mod
            if (vanillaOverrides.Count > 0 && modInfo.Name != null)
                modInfo.VanillaOverrides = vanillaOverrides;

            if (items.Count == 0) return null;

            modInfo.Items = items;
            return modInfo;
        }
        catch
        {
            return null; // Skip broken paks silently
        }
    }

    private static ItemEntry? ResolveItem(StatsEntry entry, StatsResolver resolver)
    {
        var slot = resolver.Resolve(entry.Name, "Slot");
        var armorType = resolver.Resolve(entry.Name, "ArmorType");
        var rarity = resolver.Resolve(entry.Name, "Rarity");
        var shield = resolver.Resolve(entry.Name, "Shield");
        var weaponProps = resolver.Resolve(entry.Name, "Weapon Properties");
        var valueOverride = resolver.Resolve(entry.Name, "ValueOverride");
        var unique = resolver.Resolve(entry.Name, "Unique");

        // Detect pool from slot + type
        var pool = DetectPool(entry.Type, slot, armorType, shield, weaponProps);
        if (pool == null) return null; // Skip items with unrecognized slots

        // Map rarity
        var detectedRarity = MapRarity(rarity);

        return new ItemEntry
        {
            StatId = entry.Name,
            StatType = entry.Type,
            ResolvedSlot = slot,
            ResolvedArmorType = armorType,
            ResolvedRarity = rarity,
            ResolvedShield = shield,
            ResolvedWeaponProperties = weaponProps,
            ResolvedValueOverride = valueOverride,
            ResolvedUnique = unique,
            DetectedPool = pool,
            DetectedRarity = detectedRarity
        };
    }

    public static string? DetectPool(string statType, string? slot, string? armorType, string? shield, string? weaponProps)
    {
        // Shield check
        if (string.Equals(shield, "Yes", StringComparison.OrdinalIgnoreCase))
            return "Shields";

        if (statType == "Weapon")
        {
            return slot switch
            {
                "Melee Main Weapon" => IsOneHanded(weaponProps) ? "Weapons_1H" : "Weapons_2H",
                "Melee Offhand Weapon" => "Weapons_1H",
                "Ranged Main Weapon" => IsOneHanded(weaponProps) ? "Weapons_1H" : "Weapons_2H",
                "Ranged Offhand Weapon" => "Weapons_1H",
                _ => IsOneHanded(weaponProps) ? "Weapons_1H" : "Weapons_2H"
            };
        }

        // Armor type
        return slot switch
        {
            "Breast" => IsClothArmor(armorType) ? "Clothes" : "Armor",
            "Helmet" => "Hats",
            "Cloak" => "Cloaks",
            "Gloves" => "Gloves",
            "Boots" => "Boots",
            "Amulet" => "Amulets",
            "Ring" => "Rings",
            "MusicalInstrument" => "Rings",
            // Skip these
            "Underwear" => null,
            _ when slot?.StartsWith("Vanity") == true => null,
            _ => null
        };
    }

    private static bool IsOneHanded(string? weaponProps)
    {
        if (string.IsNullOrEmpty(weaponProps)) return false;
        // Twohanded (no hyphen) is the BG3 property name
        return !weaponProps.Contains("Twohanded", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsClothArmor(string? armorType)
    {
        return string.IsNullOrEmpty(armorType) ||
               armorType.Equals("None", StringComparison.OrdinalIgnoreCase) ||
               armorType.Equals("Cloth", StringComparison.OrdinalIgnoreCase);
    }

    private static string MapRarity(string? rarity)
    {
        return rarity switch
        {
            "Common" => "Common",
            "Uncommon" => "Uncommon",
            "Rare" => "Rare",
            "VeryRare" => "VeryRare",
            "Legendary" => "Legendary",
            _ => "Uncommon" // Default
        };
    }

    /// <summary>
    /// Adds entries to resolver without letting StatusData/PassiveData overwrite Armor/Weapon entries.
    /// Same-name StatusData entries (e.g. WPN_SPEAR_U) would erase RootTemplate from Weapon entries.
    /// </summary>
    private static void AddEntriesSafe(Parsing.StatsResolver resolver, IEnumerable<Parsing.StatsEntry> entries)
    {
        foreach (var entry in entries)
        {
            var existing = resolver.Get(entry.Name);
            if (existing != null
                && (existing.Type == "Armor" || existing.Type == "Weapon")
                && entry.Type != "Armor" && entry.Type != "Weapon")
            {
                continue; // Don't let StatusData/PassiveData overwrite Armor/Weapon
            }

            // Fix self-referencing using: BG3 skeleton pattern where entry.Using == entry.Name.
            // Preserve the using from the existing (vanilla) entry so the inheritance chain stays intact.
            var fixedUsing = entry.Using;
            if (fixedUsing != null && fixedUsing.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
                fixedUsing = existing?.Using;

            // Merge data: existing fields as base, new entry overrides
            // Skeleton detection: entry where ALL data values are empty (only declares fields
            // without setting them). Real overrides have at least one non-empty value.
            var isSkeleton = entry.Data.Count > 0 && entry.Data.Values.All(string.IsNullOrEmpty);
            Dictionary<string, string> mergedData;
            if (existing != null)
            {
                mergedData = new Dictionary<string, string>(existing.Data, StringComparer.OrdinalIgnoreCase);
                foreach (var kvp in entry.Data)
                {
                    if (isSkeleton && string.IsNullOrEmpty(kvp.Value)
                        && mergedData.TryGetValue(kvp.Key, out var prev)
                        && !string.IsNullOrEmpty(prev))
                        continue; // Don't let skeleton erase existing value
                    mergedData[kvp.Key] = kvp.Value;
                }
            }
            else
            {
                mergedData = entry.Data;
            }

            resolver.AddEntries(new[] { new Parsing.StatsEntry
            {
                Name = entry.Name,
                Type = entry.Type,
                Using = fixedUsing ?? existing?.Using,
                Data = mergedData
            }});
        }
    }

    private static (Parsing.StatsResolver resolver, Dictionary<string, string> locaMap, Dictionary<string, string> handleOwnership) ResolveDisplayNames(string ampPakPath, ModInfo? ampMod,
        List<ModInfo> mods, string[] modPakPaths, string langCode, Parsing.StatsResolver baseResolver,
        IProgress<ScanProgress>? progress = null)
    {
        var allItems = new List<ItemEntry>();
        if (ampMod != null) allItems.AddRange(ampMod.Items);
        foreach (var mod in mods) allItems.AddRange(mod.Items);
        var resolver = new Parsing.StatsResolver();
        foreach (var kvp in baseResolver.AllEntries)
            resolver.AddEntries(new[] { kvp.Value });

        var masterLocaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var handleOwnership = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (allItems.Count == 0) return (resolver, masterLocaMap, handleOwnership);

        progress?.Report(new ScanProgress { Stage = "BuildResolver", Percent = 58 });

        // Add AMP stats entries (they have RootTemplate UUIDs and using chains)
        try
        {
            using var ampFs = File.OpenRead(ampPakPath);
            var ampHeader = PakReader.ReadHeader(ampFs);
            var ampEntries = PakReader.ReadFileList(ampFs, ampHeader);
            foreach (var sf in ampEntries.Where(e =>
                e.Path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase) &&
                e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                !e.Path.EndsWith("ZZZ_ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase) &&
                !e.Path.EndsWith("ParaTool_Overrides.txt", StringComparison.OrdinalIgnoreCase)))
            {
                var data = PakReader.ExtractFileData(ampFs, sf);
                var text = System.Text.Encoding.UTF8.GetString(data);
                AddEntriesSafe(resolver, Parsing.StatsParser.Parse(text));
            }
        }
        catch (Exception ex) { AppLogger.Error("Failed to load AMP stats entries", ex); }

        // Add mod stats entries
        foreach (var mod in mods)
        {
            try
            {
                using var fs = File.OpenRead(mod.PakPath);
                var header = PakReader.ReadHeader(fs);
                var entries = PakReader.ReadFileList(fs, header);
                foreach (var sf in entries.Where(e =>
                    e.Path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase) &&
                    e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
                {
                    var data = PakReader.ExtractFileData(fs, sf);
                    var text = System.Text.Encoding.UTF8.GetString(data);
                    AddEntriesSafe(resolver, Parsing.StatsParser.Parse(text));
                }
            }
            catch (Exception ex) { AppLogger.Warn($"Failed to load mod stats from {mod.PakPath}: {ex.Message}"); }
        }

        progress?.Report(new ScanProgress { Stage = "ResolveTemplates", Percent = 65 });

        // === Build LSF-structure-aware template metadata map across ALL paks ===
        // Why: the legacy uuid→handle lookup uses byte-proximity heuristics in merged
        // .lsf files (FindHandlesForUuidsEx). In merged files packing hundreds of
        // templates contiguously, the "first handle after UUID" regularly belongs to
        // a neighboring template, which is why distinct cloaks all collapse to
        // "Master's Cloak of Fortitude". This pass parses the LSF tree properly so
        // each attribute is tied to the node it was defined in.
        //
        // Two maps come out of this:
        //  * uuidToTemplateMeta: UUID → (nameHandle, descHandle, icon, stats, parent, pakPath)
        //  * statsToOwnUuid:    StatId → UUID (for items whose template declares Stats=<StatId>)
        //    Needed because tiered AMP items (MAG_Cloak24_1/2/3) have no RT field in
        //    their stats entry; without this, they walk up to MAG_Cloak24's RT and
        //    collapse to the parent's name.
        var allStatIdsSet = new HashSet<string>(
            allItems.Select(i => i.StatId), StringComparer.OrdinalIgnoreCase);
        var uuidToTemplateMeta = new Dictionary<string, (string? nameHandle, string? descHandle, string? icon, string? stats, string? parent, string pakPath)>(
            StringComparer.OrdinalIgnoreCase);
        var statsToOwnUuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void ScanPakTemplates(string pakPath)
        {
            try
            {
                using var fs = File.OpenRead(pakPath);
                var header = PakReader.ReadHeader(fs);
                var entries = PakReader.ReadFileList(fs, header);
                var rtFiles = entries.Where(e =>
                    (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
                    (e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase) ||
                     e.Path.Contains("Globals", StringComparison.OrdinalIgnoreCase))).ToList();
                foreach (var rtFile in rtFiles)
                {
                    byte[] data;
                    try { data = PakReader.ExtractFileData(fs, rtFile); }
                    catch { continue; }
                    var meta = RootTemplateIconExtractor.ExtractFullMetadata(data);
                    foreach (var (uuid, val) in meta)
                    {
                        // First pak to declare a UUID wins (AMP scanned first)
                        if (!uuidToTemplateMeta.ContainsKey(uuid))
                            uuidToTemplateMeta[uuid] = (val.nameHandle, val.descHandle, val.icon, val.stats, val.parent, pakPath);

                        if (val.stats != null && allStatIdsSet.Contains(val.stats))
                            statsToOwnUuid.TryAdd(val.stats, uuid);
                    }
                }
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"Template scan failed for {pakPath}: {ex.Message}");
            }
        }

        // AMP first: AMP items should prefer AMP's own templates over any mod-side duplicates.
        ScanPakTemplates(ampPakPath);
        foreach (var mod in mods)
            if (!string.IsNullOrEmpty(mod.PakPath))
                ScanPakTemplates(mod.PakPath);

        AppLogger.Info($"Template metadata: {uuidToTemplateMeta.Count} templates, {statsToOwnUuid.Count}/{allItems.Count} items with own templates");

        // For each item, find its RootTemplate UUID:
        //  1. Prefer own template (Stats=<StatId> reverse lookup) — authoritative per-item
        //  2. Fallback: walk stats using-chain to find first ancestor with RootTemplate
        var uuidToStatIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var statIdToOwnUuid = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in allItems)
        {
            string? foundUuid = null;

            if (statsToOwnUuid.TryGetValue(item.StatId, out var ownUuid))
            {
                foundUuid = ownUuid;
                statIdToOwnUuid[item.StatId] = ownUuid;
            }
            else
            {
                var current = item.StatId;
                int depth = 0;
                while (current != null && depth < 20)
                {
                    var entry = resolver.Get(current);
                    if (entry != null && entry.Data.TryGetValue("RootTemplate", out var uuid)
                        && !string.IsNullOrEmpty(uuid))
                    {
                        foundUuid = uuid;
                        break;
                    }
                    current = entry?.Using;
                    depth++;
                }
            }

            if (foundUuid != null)
            {
                if (!uuidToStatIds.TryGetValue(foundUuid, out var list))
                {
                    list = new List<string>();
                    uuidToStatIds[foundUuid] = list;
                }
                list.Add(item.StatId);
            }
        }

        if (uuidToStatIds.Count == 0) return (resolver, masterLocaMap, handleOwnership);

        progress?.Report(new ScanProgress { Stage = "ScanTemplates", Percent = 70 });

        // === Name resolution: mod-pak first → AMP fallback → vanilla fallback ===

        // Per-item resolved names/descriptions/handles (keyed by StatId, not UUID)
        var itemNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var itemDescs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var itemNameHandles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var itemDescHandles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, string> iconNamesMap = new(StringComparer.OrdinalIgnoreCase);

        // Step 1: Resolve from each mod pak FIRST (for that mod's items only)
        foreach (var mod in mods)
        {
            if (string.IsNullOrEmpty(mod.PakPath)) continue;
            var modItemSet = new HashSet<string>(
                mod.Items.Select(i => i.StatId), StringComparer.OrdinalIgnoreCase);

            var modUuidMap = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            foreach (var (uuid, statIds) in uuidToStatIds)
            {
                var modStatIds = statIds.Where(s => modItemSet.Contains(s)).ToList();
                if (modStatIds.Count > 0)
                    modUuidMap[uuid] = modStatIds;
            }
            if (modUuidMap.Count == 0) continue;

            try
            {
                var (modNames, modDescs, modNh, modDh) =
                    ItemNameResolver.ResolveFromPakFull(mod.PakPath, modUuidMap, langCode);

                foreach (var (uuid, modStatIds) in modUuidMap)
                {
                    foreach (var statId in modStatIds)
                    {
                        if (modNames.TryGetValue(uuid, out var n)) itemNames.TryAdd(statId, n);
                        if (modDescs.TryGetValue(uuid, out var d)) itemDescs.TryAdd(statId, d);
                        if (modNh.TryGetValue(uuid, out var nh)) itemNameHandles.TryAdd(statId, nh);
                        if (modDh.TryGetValue(uuid, out var dh)) itemDescHandles.TryAdd(statId, dh);
                    }
                }

                // Collect mod icons
                try
                {
                    using var mfs = File.OpenRead(mod.PakPath);
                    var mHeader = PakReader.ReadHeader(mfs);
                    var mEntries = PakReader.ReadFileList(mfs, mHeader);
                    var mUuids = new HashSet<string>(modUuidMap.Keys, StringComparer.OrdinalIgnoreCase);
                    var mRtFiles = mEntries.Where(e2 =>
                        (e2.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) || e2.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
                        (e2.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) || e2.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase)));
                    foreach (var rtf in mRtFiles)
                    {
                        var rtData = PakReader.ExtractFileData(mfs, rtf);
                        var mIcons = RootTemplateIconExtractor.ExtractFromLsf(rtData);
                        foreach (var (k2, v2) in mIcons)
                            if (mUuids.Contains(k2)) iconNamesMap.TryAdd(k2, v2);
                    }
                }
                catch { /* skip */ }

                // Collect mod loca
                var modLoca = ItemNameResolver.ReadAllLocalization(mod.PakPath, langCode);
                foreach (var (k, v) in modLoca)
                    masterLocaMap.TryAdd(k, v);

                // Force-override masterLocaMap for handles belonging to THIS mod's items.
                // Prevents other mods from shadowing via TryAdd order.
                var ownedHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var h in modNh.Values) ownedHandles.Add(h);
                foreach (var h in modDh.Values) ownedHandles.Add(h);
                foreach (var (k, v) in modLoca)
                    if (ownedHandles.Contains(k))
                        masterLocaMap[k] = v;

                // Track handle ownership for on-demand loca loading (other languages)
                foreach (var h in ownedHandles)
                    handleOwnership[h] = mod.PakPath;
            }
            catch (Exception ex) { AppLogger.Warn($"Failed to resolve mod pak {mod.PakPath}: {ex.Message}"); }
        }

        // Step 2: Resolve from AMP pak (fallback for items not resolved from mod pak)
        var (ampNames, ampDescs, ampNh, ampDh) =
            ItemNameResolver.ResolveFromPakFull(ampPakPath, uuidToStatIds, langCode);

        try
        {
            using var hfs = File.OpenRead(ampPakPath);
            var hHeader = PakReader.ReadHeader(hfs);
            var hEntries = PakReader.ReadFileList(hfs, hHeader);
            var uuidsSet = new HashSet<string>(uuidToStatIds.Keys, StringComparer.OrdinalIgnoreCase);
            var rtFiles = hEntries.Where(e =>
                (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) || e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
                (e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) ||
                 e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase))).ToList();
            foreach (var rtFile in rtFiles)
            {
                var data = PakReader.ExtractFileData(hfs, rtFile);
                var icons = RootTemplateIconExtractor.ExtractFromLsf(data);
                foreach (var (k, v) in icons)
                    if (uuidsSet.Contains(k)) iconNamesMap.TryAdd(k, v);
            }
        }
        catch (Exception ex) { AppLogger.Error("Failed to scan AMP templates/icons", ex); }

        var ampLoca = ItemNameResolver.ReadAllLocalization(ampPakPath, langCode);
        foreach (var (k, v) in ampLoca)
            masterLocaMap.TryAdd(k, v);

        // AMP-owned handles: override with AMP's text (authoritative for its templates)
        var ampOwnedHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var h in ampNh.Values) ampOwnedHandles.Add(h);
        foreach (var h in ampDh.Values) ampOwnedHandles.Add(h);
        foreach (var (k, v) in ampLoca)
            if (ampOwnedHandles.Contains(k))
            {
                masterLocaMap[k] = v;
                handleOwnership[k] = ampPakPath;
            }

        progress?.Report(new ScanProgress { Stage = "ResolveLoca", Percent = 90 });

        // Step 2.4: Build cross-pak template parent graph (uuid -> parentUuid)
        // so we can walk template inheritance when the item's own template has
        // no DisplayName handle (thin template pattern, e.g. DnD2024's Artificer
        // Flail points at a parent "Wrecking Ball" template).
        var templateParents = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var pakPath in new[] { ampPakPath }.Concat(mods.Select(m => m.PakPath).Where(p => !string.IsNullOrEmpty(p))))
        {
            try
            {
                var parents = ItemNameResolver.GatherTemplateParents(pakPath!);
                foreach (var (k, v) in parents) templateParents.TryAdd(k, v);
            }
            catch { }
        }

        // Step 2.45: For UUIDs still unresolved, walk template parent chain and
        // add ancestor UUIDs that might have handles. Expand uuidToStatIds with
        // parent UUIDs so subsequent cross-mod scan can find them.
        // CRITICAL: deep-copy the lists — a Dictionary copy-ctor is shallow, so
        // mutating expandedUuidToStatIds[k] directly mutates uuidToStatIds[k] too,
        // which poisons Pass 0 downstream (AMP cloaks all inheriting one handle).
        var expandedUuidToStatIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var (k, v) in uuidToStatIds)
            expandedUuidToStatIds[k] = [.. v];
        foreach (var (uuid, statIds) in uuidToStatIds.ToList())
        {
            // Walk chain regardless of whether this uuid already resolved —
            // a mod-specific name on the child template should win, but if it
            // doesn't exist, the ancestor provides the fallback.
            var cur = uuid;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { cur };
            int depth = 0;
            while (depth < 10 && templateParents.TryGetValue(cur, out var parentUuid))
            {
                if (!seen.Add(parentUuid)) break;
                if (!expandedUuidToStatIds.ContainsKey(parentUuid))
                    expandedUuidToStatIds[parentUuid] = [.. statIds];
                else
                    foreach (var s in statIds)
                        if (!expandedUuidToStatIds[parentUuid].Contains(s))
                            expandedUuidToStatIds[parentUuid].Add(s);
                cur = parentUuid;
                depth++;
            }
        }

        // Step 2.5: Cross-mod fallback — if a StatId still has no resolved name,
        // its template UUID might live in a DIFFERENT mod's pak (common when mod A
        // registers a stats entry that points at a RootTemplate shipped by mod B,
        // e.g. a class mod registers Artificer flail stats, DnD2024 mod ships the
        // flail template). Scan every mod pak for any unresolved UUID.
        // AMP items must stay authoritative — skip them in cross-mod resolution
        // (their names come from AMP pak via Step 2 / Step 3's AMP branch).
        var ampStatIds = ampMod != null
            ? new HashSet<string>(ampMod.Items.Select(i => i.StatId), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Scan every mod pak for every UUID in the expanded graph (including
        // ancestor UUIDs from template-parent chains). Propagate the name /
        // handle to ALL statIds under each UUID, even if some of those statIds
        // already have a resolved name — so _1/_2/_3 tiers all get the same
        // mod-template name when only _1 has a direct template. Skip AMP items
        // because AMP is authoritative for its own StatIds.
        foreach (var mod in mods)
        {
            if (string.IsNullOrEmpty(mod.PakPath)) continue;
            try
            {
                var (xNames, xDescs, xNh, xDh) =
                    ItemNameResolver.ResolveFromPakFull(mod.PakPath, expandedUuidToStatIds, langCode);
                if (xNames.Count == 0 && xNh.Count == 0) continue;

                // Record handle ownership so other-language loca loads can find
                // this mod's text for these handles.
                foreach (var h in xNh.Values) handleOwnership.TryAdd(h, mod.PakPath);
                foreach (var h in xDh.Values) handleOwnership.TryAdd(h, mod.PakPath);

                foreach (var (uuid, statIds) in expandedUuidToStatIds)
                {
                    foreach (var statId in statIds)
                    {
                        if (ampStatIds.Contains(statId)) continue;  // AMP authoritative
                        if (xNames.TryGetValue(uuid, out var n)) itemNames.TryAdd(statId, n);
                        if (xDescs.TryGetValue(uuid, out var d)) itemDescs.TryAdd(statId, d);
                        if (xNh.TryGetValue(uuid, out var nh)) itemNameHandles.TryAdd(statId, nh);
                        if (xDh.TryGetValue(uuid, out var dh)) itemDescHandles.TryAdd(statId, dh);
                    }
                }

                // Merge this mod's loca into master for later handle resolution
                var modLoca = ItemNameResolver.ReadAllLocalization(mod.PakPath, langCode);
                foreach (var (k, v) in modLoca)
                    masterLocaMap.TryAdd(k, v);
            }
            catch (Exception ex) { AppLogger.Warn($"Cross-mod resolve failed for {mod.PakPath}: {ex.Message}"); }
        }

        // Step 3: Apply names per item
        // AMP items: mod-pak → AMP (AMP is authoritative for its own items)
        // Non-AMP mod items: mod-pak only (skip AMP, vanilla fallback later)
        var ampItemSet = ampMod != null
            ? new HashSet<string>(ampMod.Items.Select(i => i.StatId), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Track statIds that already got a name set — prevents a later iteration
        // (e.g. when the same StatId is registered in two mods with different
        // RootTemplate UUIDs, hence appears twice in uuidToStatIds) from
        // overwriting an authoritative AMP value.
        var namedStatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Pass 0: Authoritative LSF-structure-aware assignment, keyed by item (not
        // by uuidToStatIds). Each item has a single authoritative UUID — own template
        // via statIdToOwnUuid when present, else the stats-chain RT. Walk ParentTemplateId
        // when the own template has no DisplayName/Description (thin-template pattern).
        // Handles come from proper LSF tree parsing (ExtractFullMetadata), not from
        // byte-proximity heuristics — that's the fix for "100 cloaks share one name".
        string? ResolveHandleViaMeta(string startUuid, Func<(string? nameHandle, string? descHandle, string? icon, string? stats, string? parent, string pakPath), string?> pick)
        {
            var cur = startUuid;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int depth = 0;
            while (depth < 15 && cur != null && seen.Add(cur))
            {
                if (!uuidToTemplateMeta.TryGetValue(cur, out var meta)) return null;
                var picked = pick(meta);
                if (!string.IsNullOrEmpty(picked)) return picked;
                cur = meta.parent;
                depth++;
            }
            return null;
        }

        foreach (var item in allItems)
        {
            if (namedStatIds.Contains(item.StatId)) continue;

            // Authoritative UUID: prefer own template, else fall back to stats-chain RT.
            string? itemUuid = null;
            if (statIdToOwnUuid.TryGetValue(item.StatId, out var ownU)) itemUuid = ownU;
            else
            {
                var cur = item.StatId;
                int depth = 0;
                while (cur != null && depth < 20)
                {
                    var entry = resolver.Get(cur);
                    if (entry != null && entry.Data.TryGetValue("RootTemplate", out var uuid) && !string.IsNullOrEmpty(uuid))
                    { itemUuid = uuid; break; }
                    cur = entry?.Using;
                    depth++;
                }
            }
            if (itemUuid == null) continue;

            var nh = ResolveHandleViaMeta(itemUuid, m => m.nameHandle);
            var dh = ResolveHandleViaMeta(itemUuid, m => m.descHandle);
            var ic = ResolveHandleViaMeta(itemUuid, m => m.icon);

            if (nh != null && masterLocaMap.TryGetValue(nh, out var nameText))
            {
                item.DisplayName = Core.Localization.BbCode.FromBg3Xml(nameText);
                item.DisplayNameHandle = nh;
                namedStatIds.Add(item.StatId);
            }
            if (dh != null && masterLocaMap.TryGetValue(dh, out var descText))
            {
                item.Description = Core.Localization.BbCode.FromBg3Xml(descText);
                item.DescriptionHandle = dh;
            }
            if (ic != null) item.IconName = ic;
        }

        AppLogger.Info($"Pass 0 (LSF-aware): named {namedStatIds.Count}/{allItems.Count} items");

        // Pass A: AMP items (authoritative) — set first so any later pass can't override.
        foreach (var (uuid, statIds) in uuidToStatIds)
        {
            foreach (var statId in statIds)
            {
                if (!ampItemSet.Contains(statId)) continue;
                if (namedStatIds.Contains(statId)) continue;
                if (!ampNames.TryGetValue(uuid, out var ampN)) continue;
                var item = allItems.Find(i => i.StatId.Equals(statId, StringComparison.OrdinalIgnoreCase));
                if (item == null) continue;
                item.DisplayName = ampN;
                item.DisplayNameHandle = ampNh.GetValueOrDefault(uuid);
                if (ampDescs.TryGetValue(uuid, out var ampD))
                {
                    item.Description = ampD;
                    item.DescriptionHandle = ampDh.GetValueOrDefault(uuid);
                }
                if (iconNamesMap.TryGetValue(uuid, out var ampIcon))
                    item.IconName = ampIcon;
                namedStatIds.Add(statId);
            }
        }

        // Pass B: non-AMP items + AMP items that AMP didn't resolve, via mod paks.
        foreach (var (uuid, statIds) in uuidToStatIds)
        {
            foreach (var statId in statIds)
            {
                if (namedStatIds.Contains(statId)) continue;
                var item = allItems.Find(i => i.StatId.Equals(statId, StringComparison.OrdinalIgnoreCase));
                if (item == null) continue;

                if (itemNames.TryGetValue(statId, out var modN))
                {
                    item.DisplayName = modN;
                    item.DisplayNameHandle = itemNameHandles.GetValueOrDefault(statId);
                    namedStatIds.Add(statId);
                }
                if (itemDescs.TryGetValue(statId, out var modD) && item.Description == null)
                {
                    item.Description = modD;
                    item.DescriptionHandle = itemDescHandles.GetValueOrDefault(statId);
                }
                if (iconNamesMap.TryGetValue(uuid, out var iconName) && item.IconName == null)
                    item.IconName = iconName;
            }
        }

        // Fallback: fill missing DisplayNames from embedded VanillaLocaService
        // Also walk using-chain for tiered items (_1, _2, _3)
        foreach (var item in allItems)
        {
            if (item.DisplayName == null)
            {
                // Try direct StatId
                var name = VanillaLocaService.GetDisplayName(item.StatId, langCode);

                // Walk using-chain to find parent with loca
                if (name == null)
                {
                    var current = item.StatId;
                    int depth = 0;
                    while (name == null && current != null && depth < 20)
                    {
                        var entry = resolver.Get(current);
                        if (entry == null) break;
                        name = VanillaLocaService.GetDisplayName(entry.Name, langCode);
                        if (name != null) item.LocaAncestorId = entry.Name;
                        current = entry.Using;
                        depth++;
                    }
                }

                if (name != null) item.DisplayName = name;
            }
            if (item.Description == null)
            {
                var desc = VanillaLocaService.GetDescription(item.StatId, langCode);
                if (desc == null)
                {
                    var current = item.StatId;
                    int depth = 0;
                    while (desc == null && current != null && depth < 20)
                    {
                        var entry = resolver.Get(current);
                        if (entry == null) break;
                        desc = VanillaLocaService.GetDescription(entry.Name, langCode);
                        current = entry.Using;
                        depth++;
                    }
                }
                if (desc != null) item.Description = desc;
            }
            if (item.IconName == null)
            {
                var icon = VanillaLocaService.GetIconName(item.StatId);
                if (icon == null)
                {
                    var current = item.StatId;
                    int depth = 0;
                    while (icon == null && current != null && depth < 20)
                    {
                        var entry = resolver.Get(current);
                        if (entry == null) break;
                        icon = VanillaLocaService.GetIconName(entry.Name);
                        current = entry.Using;
                        depth++;
                    }
                }
                if (icon != null) item.IconName = icon;
            }
        }

        // Reverse template lookup: for items still without names, scan mod RootTemplates
        // for templates that reference StatIds via Stats attribute (template → stats, not stats → template)
        var unnamed = allItems.Where(i => i.DisplayName == null).Select(i => i.StatId).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (unnamed.Count > 0)
        {
            foreach (var mod in mods)
            {
                if (string.IsNullOrEmpty(mod.PakPath) || unnamed.Count == 0) continue;
                try
                {
                    using var mfs = File.OpenRead(mod.PakPath);
                    var mHeader = PakReader.ReadHeader(mfs);
                    var mEntries = PakReader.ReadFileList(mfs, mHeader);
                    var rtFiles = mEntries.Where(e =>
                        e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) &&
                        e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase)).ToList();

                    // Find templates with Stats attribute matching unnamed items
                    var reverseMap = new Dictionary<string, (string uuid, string? nameHandle, string? descHandle, string? icon)>(StringComparer.OrdinalIgnoreCase);
                    foreach (var rtf in rtFiles)
                    {
                        var rtData = PakReader.ExtractFileData(mfs, rtf);
                        var found = RootTemplateIconExtractor.ExtractByStats(rtData, unnamed);
                        foreach (var (statId, val) in found)
                            reverseMap.TryAdd(statId, val);
                    }

                    if (reverseMap.Count > 0)
                    {
                        // Collect handles to resolve
                        var handles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var (_, val) in reverseMap)
                        {
                            if (val.nameHandle != null) handles.Add(val.nameHandle.Split(';')[0]);
                            if (val.descHandle != null) handles.Add(val.descHandle.Split(';')[0]);
                        }

                        // Resolve from mod loca
                        var modLoca2 = ItemNameResolver.ReadAllLocalization(mod.PakPath, langCode);

                        foreach (var (statId, (uuid, nh, dh, rIcon)) in reverseMap)
                        {
                            var item = allItems.Find(i => i.StatId.Equals(statId, StringComparison.OrdinalIgnoreCase));
                            if (item == null) continue;

                            if (nh != null)
                            {
                                var handleKey = nh.Split(';')[0];
                                if (modLoca2.TryGetValue(handleKey, out var name))
                                {
                                    item.DisplayName = Core.Localization.BbCode.FromBg3Xml(name);
                                    item.DisplayNameHandle = handleKey;
                                }
                            }
                            if (dh != null && item.Description == null)
                            {
                                var handleKey = dh.Split(';')[0];
                                if (modLoca2.TryGetValue(handleKey, out var desc))
                                {
                                    item.Description = Core.Localization.BbCode.FromBg3Xml(desc);
                                    item.DescriptionHandle = handleKey;
                                }
                            }

                            if (rIcon != null && item.IconName == null)
                                item.IconName = rIcon;
                            unnamed.Remove(statId);
                        }
                    }
                }
                catch (Exception ex) { AppLogger.Warn($"Reverse template scan failed for {mod.PakPath}: {ex.Message}"); }
            }
        }

        // Reverse-lookup: for items with DisplayName but no handle, find handle in masterLocaMap
        // This enables multi-language resolution later
        if (masterLocaMap.Count > 0)
        {
            // Build reverse maps: raw text → handle AND stripped text → handle
            // Loca files contain <LSTag> markup that VanillaLocaService TSV doesn't have
            var reverseMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var reverseMapStripped = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (handle, text) in masterLocaMap)
            {
                if (!string.IsNullOrEmpty(text))
                {
                    reverseMap.TryAdd(text, handle);
                    var stripped = StripLsTags(text);
                    if (stripped != text)
                        reverseMapStripped.TryAdd(stripped, handle);
                }
            }

            foreach (var item in allItems)
            {
                if (item.DisplayNameHandle == null && item.DisplayName != null)
                {
                    if (reverseMap.TryGetValue(item.DisplayName, out var foundHandle)
                        || reverseMapStripped.TryGetValue(item.DisplayName, out foundHandle))
                        item.DisplayNameHandle = foundHandle;
                }
                if (item.DescriptionHandle == null && item.Description != null)
                {
                    if (reverseMap.TryGetValue(item.Description, out var foundHandle)
                        || reverseMapStripped.TryGetValue(item.Description, out foundHandle))
                        item.DescriptionHandle = foundHandle;
                }
            }
        }

        // Diagnostic: handle coverage
        int withHandle = allItems.Count(i => i.DisplayNameHandle != null);
        int withName = allItems.Count(i => i.DisplayName != null);
        int noHandleNoName = allItems.Count(i => i.DisplayNameHandle == null && i.DisplayName == null);
        int noHandleWithName = allItems.Count(i => i.DisplayNameHandle == null && i.DisplayName != null);
        AppLogger.Info($"Loca stats: {allItems.Count} items, {withHandle} with handle, {withName} with name, " +
            $"{noHandleWithName} name-only (no handle), {noHandleNoName} neither");
        // Log first few items without handles + check why reverse failed
        foreach (var item in allItems.Where(i => i.DisplayNameHandle == null && i.DisplayName != null).Take(5))
        {
            var inMap = masterLocaMap.Values.Any(v => v != null &&
                v.Equals(item.DisplayName, StringComparison.OrdinalIgnoreCase));
            var partial = !inMap ? masterLocaMap.Values
                .FirstOrDefault(v => v != null && v.Contains(item.DisplayName, StringComparison.OrdinalIgnoreCase)) : null;
            AppLogger.Debug($"  No handle: {item.StatId} name=\"{item.DisplayName}\" inLocaMap={inMap} partial=\"{partial?[..Math.Min(partial?.Length ?? 0, 60)]}\"");
        }

        // Build SearchableText for deep search in patcher
        var searchFields = new[] { "PassivesOnEquip", "Boosts", "DefaultBoosts",
            "StatusOnEquip", "BoostsOnEquipMainHand", "BoostsOnEquipOffHand",
            "StatusOnEquipOffHand", "StatusOnEquipMainHand", "SpellsOnEquip" };
        foreach (var item in allItems)
        {
            var sb = new System.Text.StringBuilder();
            sb.Append(item.StatId).Append(' ');
            if (item.DisplayName != null) sb.Append(item.DisplayName).Append(' ');
            if (item.Description != null) sb.Append(item.Description).Append(' ');

            var fields = resolver.ResolveAll(item.StatId);
            foreach (var fieldName in searchFields)
            {
                if (fields.TryGetValue(fieldName, out var val) && !string.IsNullOrEmpty(val))
                    sb.Append(val).Append(' ');
            }

            // Resolve passive names for search
            if (fields.TryGetValue("PassivesOnEquip", out var passives) && !string.IsNullOrEmpty(passives))
            {
                foreach (var pName in passives.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                {
                    var pFields = resolver.ResolveAll(pName);
                    foreach (var pf in new[] { "Boosts", "StatsFunctors", "Conditions", "BoostConditions" })
                        if (pFields.TryGetValue(pf, out var pv)) sb.Append(pv).Append(' ');
                    // Passive display name from vanilla loca
                    var pDispName = VanillaLocaService.GetDisplayName(pName, langCode);
                    if (pDispName != null) sb.Append(pDispName).Append(' ');
                }
            }

            item.SearchableText = sb.ToString();
        }

        return (resolver, masterLocaMap, handleOwnership);
    }

    /// <summary>Strip BG3 LSTag markup from loca text, e.g. "&lt;LSTag Tooltip="..."&gt;text&lt;/LSTag&gt;" → "text"</summary>
    private static string StripLsTags(string text)
    {
        if (!text.Contains('<')) return text;
        return System.Text.RegularExpressions.Regex.Replace(text, @"<[^>]+>", "");
    }
}
