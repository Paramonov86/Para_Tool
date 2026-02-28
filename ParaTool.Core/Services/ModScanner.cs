using ParaTool.Core.Models;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Services;

public sealed class ScanResult
{
    public List<ModInfo> Mods { get; init; } = new();
    public string? AmpPakPath { get; init; }
    public string? Error { get; init; }
}

public sealed class ScanProgress
{
    public int TotalPaks { get; init; }
    public int ScannedPaks { get; init; }
    public int ModsFound { get; init; }
}

public sealed class ModScanner
{
    private readonly VanillaDatabase _vanillaDb;

    public ModScanner(VanillaDatabase vanillaDb)
    {
        _vanillaDb = vanillaDb;
    }

    public async Task<ScanResult> ScanAsync(string modsFolder, IProgress<ScanProgress>? progress = null,
        CancellationToken ct = default)
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

        var mods = new List<ModInfo>();
        int scanned = 0;

        await Parallel.ForEachAsync(nonAmpPaks, new ParallelOptions
        {
            MaxDegreeOfParallelism = Environment.ProcessorCount,
            CancellationToken = ct
        }, async (pakPath, innerCt) =>
        {
            var mod = await Task.Run(() => ScanPak(pakPath), innerCt);
            var count = Interlocked.Increment(ref scanned);

            if (mod != null)
            {
                lock (mods) mods.Add(mod);
            }

            progress?.Report(new ScanProgress
            {
                TotalPaks = nonAmpPaks.Length,
                ScannedPaks = count,
                ModsFound = mods.Count
            });
        });

        mods.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        return new ScanResult
        {
            Mods = mods,
            AmpPakPath = ampPakPath
        };
    }

    private ModInfo? ScanPak(string pakPath)
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

            // Parse mod stats
            var modEntries = new List<StatsEntry>();
            foreach (var statFile in statFiles)
            {
                var data = PakReader.ExtractFileData(fs, statFile);
                var text = System.Text.Encoding.UTF8.GetString(data);
                var parsed = StatsParser.Parse(text);
                modEntries.AddRange(parsed);
            }

            resolver.AddEntries(modEntries);

            // Filter: keep only Armor/Weapon entries from this mod
            var items = new List<ItemEntry>();
            foreach (var entry in modEntries)
            {
                if (entry.Type != "Armor" && entry.Type != "Weapon") continue;

                var item = ResolveItem(entry, resolver);
                if (item == null) continue;

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
            // Skip these
            "MusicalInstrument" or "Underwear" => null,
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
            "Uncommon" => "Uncommon",
            "Rare" => "Rare",
            "VeryRare" => "VeryRare",
            "Legendary" => "Legendary",
            _ => "Uncommon" // Default
        };
    }
}
