using ParaTool.Core.Models;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Services;

public sealed class ScanResult
{
    public List<ModInfo> Mods { get; init; } = new();
    public ModInfo? AmpMod { get; init; }
    public string? AmpPakPath { get; init; }
    public string? Error { get; init; }
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
        if (ampPaks.Length > 1)
            return new ScanResult { Error = "Multiple AMP paks found. Keep only one version." };

        var ampPakPath = ampPaks[0];
        var nonAmpPaks = pakFiles.Where(p => p != ampPakPath).ToArray();

        // Extract AMP integration info — items already in AMP TT are marked as integrated in mods
        progress?.Report(new ScanProgress { Stage = "ScanAMP", Percent = 0 });
        var (ampWhitelist, ampRarities, ampThemes) = ExtractAmpIntegrationInfo(ampPakPath);

        var mods = new List<ModInfo>();
        int scanned = 0;

        progress?.Report(new ScanProgress { Stage = "ScanMods", Percent = 5 });

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

        // Scan AMP pak itself for editable items
        progress?.Report(new ScanProgress { Stage = "ScanAMP", Percent = 42 });
        var ampMod = ScanAmpPak(ampPakPath);

        // Resolve display names from PAK localization files
        progress?.Report(new ScanProgress { Stage = "ResolveNames", Percent = 55 });
        await Task.Run(() => ResolveDisplayNames(ampPakPath, ampMod, mods, nonAmpPaks, langCode, _vanillaDb.Resolver, progress), ct);

        progress?.Report(new ScanProgress { Stage = "Done", Percent = 100 });

        return new ScanResult
        {
            Mods = mods,
            AmpMod = ampMod,
            AmpPakPath = ampPakPath
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
        catch { return (new(), new(), new()); }
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
            // If the pak was patched by us (contains ParaTool_Overrides.txt), use the
            // stored original TT so mod items don't appear as AMP items.
            // If the pak is clean (no overrides file), read TT from pak directly —
            // this handles AMP updates where the stored original would be stale.
            bool isPatchedByUs = entries.Any(e =>
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
                        var mergedData = new Dictionary<string, string>(prev.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in entry.Data)
                            mergedData[kvp.Key] = kvp.Value;

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

                    Dictionary<string, string> finalData;
                    if (sameChain)
                    {
                        finalData = new Dictionary<string, string>(vanilla.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in modEntry.Data)
                            finalData[kvp.Key] = kvp.Value;
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

                        var mergedData = new Dictionary<string, string>(prev.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in entry.Data)
                            mergedData[kvp.Key] = kvp.Value;

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

                    Dictionary<string, string> finalData;
                    if (sameChain)
                    {
                        finalData = new Dictionary<string, string>(vanilla.Data, StringComparer.OrdinalIgnoreCase);
                        foreach (var kvp in modEntry.Data)
                            finalData[kvp.Key] = kvp.Value;
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

            // Filter: keep only Armor/Weapon entries from this mod that are NOT vanilla rebals.
            // Items already in AMP treasure tables are kept but marked as integrated.
            var items = new List<ItemEntry>();
            foreach (var entry in mergedModEntries.Values)
            {
                if (entry.Type != "Armor" && entry.Type != "Weapon") continue;
                if (_vanillaDb.Resolver.AllEntries.ContainsKey(entry.Name)) continue;

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
                "Melee Main Weapon" => IsOneHanded(weaponProps) ? "Weapons_1H" : "Weapons",
                "Melee Offhand Weapon" => "Weapons_1H",
                "Ranged Main Weapon" => "Weapons_2H",
                "Ranged Offhand Weapon" => "Weapons_1H",
                _ => "Weapons"
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
        // If it has Versatile or Two-Handed, it's not strictly 1H for the main pool
        // But for the sub-pool: Versatile weapons go in Weapons (main) + Weapons_1H
        // Light, Finesse = 1H indicators
        return !weaponProps.Contains("Two-Handed", StringComparison.OrdinalIgnoreCase);
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
            resolver.AddEntries(new[] { entry });
        }
    }

    private static void ResolveDisplayNames(string ampPakPath, ModInfo? ampMod,
        List<ModInfo> mods, string[] modPakPaths, string langCode, Parsing.StatsResolver baseResolver,
        IProgress<ScanProgress>? progress = null)
    {
        var allItems = new List<ItemEntry>();
        if (ampMod != null) allItems.AddRange(ampMod.Items);
        foreach (var mod in mods) allItems.AddRange(mod.Items);
        if (allItems.Count == 0) return;

        progress?.Report(new ScanProgress { Stage = "BuildResolver", Percent = 58 });

        // Build combined resolver for using-chain walking
        var resolver = new Parsing.StatsResolver();
        foreach (var kvp in baseResolver.AllEntries)
            resolver.AddEntries(new[] { kvp.Value });

        // Add AMP stats entries (they have RootTemplate UUIDs and using chains)
        try
        {
            using var ampFs = File.OpenRead(ampPakPath);
            var ampHeader = PakReader.ReadHeader(ampFs);
            var ampEntries = PakReader.ReadFileList(ampFs, ampHeader);
            foreach (var sf in ampEntries.Where(e =>
                e.Path.Contains("/Stats/Generated/Data/", StringComparison.OrdinalIgnoreCase) &&
                e.Path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase)))
            {
                var data = PakReader.ExtractFileData(ampFs, sf);
                var text = System.Text.Encoding.UTF8.GetString(data);
                AddEntriesSafe(resolver, Parsing.StatsParser.Parse(text));
            }
        }
        catch { }

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
            catch { }
        }

        progress?.Report(new ScanProgress { Stage = "ResolveTemplates", Percent = 65 });

        // For each item, walk the using chain to find the first ancestor with a RootTemplate UUID
        // Build: UUID → list of StatIds that resolve to it
        var uuidToStatIds = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var item in allItems)
        {
            var current = item.StatId;
            int depth = 0;
            string? foundUuid = null;

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

        if (uuidToStatIds.Count == 0) return;

        progress?.Report(new ScanProgress { Stage = "ScanTemplates", Percent = 70 });

        // Resolve UUID → display name from AMP pak
        var resolved = ItemNameResolver.ResolveFromPak(ampPakPath, uuidToStatIds, langCode);

        progress?.Report(new ScanProgress { Stage = "ResolveLoca", Percent = 90 });

        // Resolve remaining from mod paks
        var remainingUuids = uuidToStatIds.Keys
            .Where(u => !resolved.ContainsKey(u))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        if (remainingUuids.Count > 0)
        {
            var remainingMap = uuidToStatIds
                .Where(kvp => remainingUuids.Contains(kvp.Key))
                .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);

            foreach (var pakPath in modPakPaths)
            {
                if (remainingMap.Count == 0) break;
                try
                {
                    var modResolved = ItemNameResolver.ResolveFromPak(pakPath, remainingMap, langCode);
                    foreach (var (uuid, name) in modResolved)
                    {
                        resolved[uuid] = name;
                        remainingMap.Remove(uuid);
                    }
                }
                catch { }
            }
        }

        // Apply: UUID → name → all items that share this UUID
        foreach (var (uuid, statIds) in uuidToStatIds)
        {
            if (!resolved.TryGetValue(uuid, out var displayName)) continue;
            foreach (var statId in statIds)
            {
                var item = allItems.Find(i => i.StatId.Equals(statId, StringComparison.OrdinalIgnoreCase));
                if (item != null)
                    item.DisplayName = displayName;
            }
        }
    }
}
