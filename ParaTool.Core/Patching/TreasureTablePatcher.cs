using ParaTool.Core.Models;

namespace ParaTool.Core.Patching;

public static class TreasureTablePatcher
{
    private static string RarityToTableName(string rarity) => rarity switch
    {
        "VeryRare" => "Epic",
        _ => rarity
    };

    private static readonly Dictionary<string, int> PoolToParaType = new()
    {
        ["Clothes"] = 1, ["Armor"] = 2, ["Shields"] = 3,
        ["Hats"] = 4, ["Cloaks"] = 5, ["Gloves"] = 6,
        ["Boots"] = 7, ["Amulets"] = 8, ["Rings"] = 9,
        ["Weapons"] = 10, ["Weapons_1H"] = 11, ["Weapons_2H"] = 12,
    };

    private static readonly Dictionary<string, int> ThemeToParaNum = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Swamp"] = 13, ["Aquatic"] = 14, ["Shadowfell"] = 15,
        ["Arcane"] = 16, ["Celestial"] = 17, ["Nature"] = 18,
        ["Destructive"] = 19, ["War"] = 20, ["Psionic"] = 21, ["Primal"] = 22,
    };

    /// <summary>
    /// Patches TreasureTable.txt by inserting items INTO existing tables at their positions.
    /// Pool tables (subtable "-1"): appends object category lines after existing items.
    /// Paragon tables (subtable "1,1"): appends new subtable blocks at end of table.
    /// </summary>
    public static string Patch(string originalText, IReadOnlyList<ItemEntry> items)
    {
        // Step 1: Collect additions by table name
        var poolAdditions = new Dictionary<string, List<string>>();
        var paragonAdditions = new Dictionary<string, List<string>>();

        foreach (var item in items)
        {
            if (!item.Enabled) continue;

            var pool = item.EffectivePool;
            var rarity = item.EffectiveRarity;
            var rt = RarityToTableName(rarity);
            var objLine = $"object category \"I_{item.StatId}\",1,0,0,0,0,0,0,0";

            // Layer 1: Type pool
            AddUnique(poolAdditions, $"REL_{rt}_{pool}", objLine);

            // Layer 2: All rarity
            AddUnique(poolAdditions, $"REL_All_{rt}", objLine);

            // Weapons also go into main Weapons pool
            if (pool is "Weapons_1H" or "Weapons_2H")
            {
                AddUnique(poolAdditions, $"REL_{rt}_Weapons", objLine);
                AddUnique(paragonAdditions, "AMP_Para_10", objLine);
            }

            // Layer 3: Theme pools
            foreach (var theme in item.UserThemes)
            {
                AddUnique(poolAdditions, $"REL_{rt}_{theme}", objLine);
                if (ThemeToParaNum.TryGetValue(theme, out var themeNum))
                    AddUnique(paragonAdditions, $"AMP_Para_{themeNum}", objLine);
            }

            // Layer 4: Paragon by type
            if (PoolToParaType.TryGetValue(pool, out var paraNum))
                AddUnique(paragonAdditions, $"AMP_Para_{paraNum}", objLine);
        }

        // Step 2: Parse file into lines
        var lines = originalText.Split('\n').ToList();

        // Step 3: Build table position index
        var tableRanges = BuildTableIndex(lines);

        // Step 4: Collect all insertions (line index → lines to insert AFTER that index)
        var insertions = new List<(int afterLine, List<string> newLines)>();

        // Pool tables: insert into existing subtable "-1"
        foreach (var (tableName, newObjLines) in poolAdditions)
        {
            if (!tableRanges.TryGetValue(tableName, out var range)) continue;

            // Find last object category line inside subtable "-1"
            int insertAfter = FindLastObjectInSubtable(lines, range.start, range.end, "-1");
            if (insertAfter < 0) continue;

            // Filter out items already in the table (avoid duplicates on re-patch)
            var existingItems = CollectExistingItems(lines, range.start, range.end);
            var filtered = newObjLines.Where(l => !existingItems.Contains(l)).ToList();
            if (filtered.Count > 0)
                insertions.Add((insertAfter, filtered));
        }

        // Paragon tables: append new subtable "1,1" blocks at end of table
        foreach (var (tableName, newObjLines) in paragonAdditions)
        {
            if (!tableRanges.TryGetValue(tableName, out var range)) continue;

            var existingItems = CollectExistingItems(lines, range.start, range.end);
            var filtered = newObjLines.Where(l => !existingItems.Contains(l)).ToList();
            if (filtered.Count == 0) continue;

            // Find last non-empty line in the table
            int insertAfter = range.end;
            while (insertAfter > range.start && string.IsNullOrWhiteSpace(lines[insertAfter]))
                insertAfter--;

            var paragonLines = new List<string>();
            foreach (var objLine in filtered)
            {
                paragonLines.Add("new subtable \"1,1\"");
                paragonLines.Add(objLine);
            }
            insertions.Add((insertAfter, paragonLines));
        }

        // Step 5: Apply insertions in REVERSE line order to preserve indices
        foreach (var (afterLine, newLines) in insertions.OrderByDescending(x => x.afterLine))
        {
            lines.InsertRange(afterLine + 1, newLines);
        }

        return string.Join('\n', lines);
    }

    /// <summary>
    /// Builds a map of table name → (startLine, endLine).
    /// </summary>
    private static Dictionary<string, (int start, int end)> BuildTableIndex(List<string> lines)
    {
        var result = new Dictionary<string, (int start, int end)>();
        var tableStarts = new List<(string name, int line)>();

        for (int i = 0; i < lines.Count; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("new treasuretable \""))
            {
                var name = ExtractQuoted(trimmed);
                if (!string.IsNullOrEmpty(name))
                    tableStarts.Add((name, i));
            }
        }

        for (int t = 0; t < tableStarts.Count; t++)
        {
            var (name, start) = tableStarts[t];
            int end = t + 1 < tableStarts.Count ? tableStarts[t + 1].line - 1 : lines.Count - 1;
            result[name] = (start, end);
        }

        return result;
    }

    /// <summary>
    /// Finds the last "object category" line inside a specific subtable spec (e.g. "-1") within a table range.
    /// </summary>
    private static int FindLastObjectInSubtable(List<string> lines, int start, int end, string subtableSpec)
    {
        int lastObj = -1;
        bool inTargetSubtable = false;

        for (int i = start; i <= end; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("new subtable "))
            {
                var spec = ExtractQuoted(trimmed);
                inTargetSubtable = spec == subtableSpec;
            }

            if (inTargetSubtable && trimmed.StartsWith("object category "))
                lastObj = i;
        }

        return lastObj;
    }

    /// <summary>
    /// Collects all existing "object category" lines in a table range for deduplication.
    /// </summary>
    private static HashSet<string> CollectExistingItems(List<string> lines, int start, int end)
    {
        var set = new HashSet<string>();
        for (int i = start; i <= end; i++)
        {
            var trimmed = lines[i].TrimStart();
            if (trimmed.StartsWith("object category "))
                set.Add(trimmed);
        }
        return set;
    }

    private static string ExtractQuoted(string line)
    {
        int first = line.IndexOf('"');
        if (first < 0) return "";
        int second = line.IndexOf('"', first + 1);
        return second < 0 ? "" : line[(first + 1)..second];
    }

    private static void AddUnique(Dictionary<string, List<string>> dict, string table, string line)
    {
        if (!dict.TryGetValue(table, out var list))
        {
            list = [];
            dict[table] = list;
        }
        if (!list.Contains(line))
            list.Add(line);
    }
}
