using System.Text;
using ParaTool.Core.Models;

namespace ParaTool.Core.Patching;

public static class TreasureTablePatcher
{
    // Rarity mapping: VeryRare → "Epic" in table names
    private static string RarityToTableName(string rarity) => rarity switch
    {
        "VeryRare" => "Epic",
        _ => rarity
    };

    // Pool → AMP_Para number (by type)
    private static readonly Dictionary<string, int> PoolToParaType = new()
    {
        ["Clothes"] = 1,
        ["Armor"] = 2,
        ["Shields"] = 3,
        ["Hats"] = 4,
        ["Cloaks"] = 5,
        ["Gloves"] = 6,
        ["Boots"] = 7,
        ["Amulets"] = 8,
        ["Rings"] = 9,
        ["Weapons"] = 10,
        ["Weapons_1H"] = 11,
        ["Weapons_2H"] = 12,
    };

    // Theme → AMP_Para number (by theme)
    private static readonly Dictionary<string, int> ThemeToParaNum = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Swamp"] = 13,
        ["Aquatic"] = 14,
        ["Shadowfell"] = 15,
        ["Arcane"] = 16,
        ["Celestial"] = 17,
        ["Nature"] = 18,
        ["Destructive"] = 19,
        ["War"] = 20,
        ["Psionic"] = 21,
        ["Primal"] = 22,
    };

    public static string Patch(string originalText, IReadOnlyList<ItemEntry> items)
    {
        // Collect all additions grouped by table name
        var poolAdditions = new Dictionary<string, List<string>>();   // table → list of object category lines
        var paragonAdditions = new Dictionary<string, List<string>>(); // table → list of items for subtable "1,1"

        foreach (var item in items)
        {
            if (!item.Enabled) continue;

            var pool = item.EffectivePool;
            var rarity = item.EffectiveRarity;
            var rarityTable = RarityToTableName(rarity);
            var objLine = $"object category \"I_{item.StatId}\",1,0,0,0,0,0,0,0";

            // Layer 1: REL_[Rarity]_[Type]
            AddToPool(poolAdditions, $"REL_{rarityTable}_{pool}", objLine);

            // Layer 2: REL_All_[Rarity]
            AddToPool(poolAdditions, $"REL_All_{rarityTable}", objLine);

            // For weapons: also add to Weapons main pool + 1H/2H
            if (pool == "Weapons_1H")
            {
                AddToPool(poolAdditions, $"REL_{rarityTable}_Weapons", objLine);
                // Paragon Weapons too
                AddParagon(paragonAdditions, "AMP_Para_10", objLine);
            }
            else if (pool == "Weapons_2H")
            {
                AddToPool(poolAdditions, $"REL_{rarityTable}_Weapons", objLine);
                AddParagon(paragonAdditions, "AMP_Para_10", objLine);
            }

            // Layer 3: REL_[Rarity]_[Theme] for each theme
            foreach (var theme in item.UserThemes)
            {
                AddToPool(poolAdditions, $"REL_{rarityTable}_{theme}", objLine);

                // Layer 4 (theme): AMP_Para_[N] by theme
                if (ThemeToParaNum.TryGetValue(theme, out var themeParaNum))
                    AddParagon(paragonAdditions, $"AMP_Para_{themeParaNum}", objLine);
            }

            // Layer 4 (type): AMP_Para_[N] by pool type
            if (PoolToParaType.TryGetValue(pool, out var paraNum))
                AddParagon(paragonAdditions, $"AMP_Para_{paraNum}", objLine);
        }

        // Build patch text to append
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("// === ParaTool Integration Start ===");

        // Pool additions (subtable "-1" appended to existing tables)
        foreach (var (tableName, lines) in poolAdditions.OrderBy(x => x.Key))
        {
            sb.AppendLine();
            sb.AppendLine($"new treasuretable \"{tableName}\"");
            sb.AppendLine("new subtable \"-1\"");
            foreach (var line in lines)
                sb.AppendLine(line);
        }

        // Paragon additions (each item in its own subtable "1,1")
        foreach (var (tableName, lines) in paragonAdditions.OrderBy(x => x.Key))
        {
            sb.AppendLine();
            sb.AppendLine($"new treasuretable \"{tableName}\"");
            foreach (var line in lines)
            {
                sb.AppendLine("new subtable \"1,1\"");
                sb.AppendLine(line);
            }
        }

        sb.AppendLine();
        sb.AppendLine("// === ParaTool Integration End ===");

        return originalText + sb.ToString();
    }

    private static void AddToPool(Dictionary<string, List<string>> dict, string table, string line)
    {
        if (!dict.TryGetValue(table, out var list))
        {
            list = new List<string>();
            dict[table] = list;
        }
        if (!list.Contains(line))
            list.Add(line);
    }

    private static void AddParagon(Dictionary<string, List<string>> dict, string table, string line)
    {
        if (!dict.TryGetValue(table, out var list))
        {
            list = new List<string>();
            dict[table] = list;
        }
        if (!list.Contains(line))
            list.Add(line);
    }
}
