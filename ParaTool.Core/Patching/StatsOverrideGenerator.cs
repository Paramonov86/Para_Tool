using System.Text;
using ParaTool.Core.Models;

namespace ParaTool.Core.Patching;

public static class StatsOverrideGenerator
{
    /// <summary>
    /// Computes the override fields for a single item (Rarity, ValueOverride, Unique).
    /// Returns null if the item should be skipped (disabled or Common).
    /// </summary>
    public static Dictionary<string, string>? ComputeFields(ItemEntry item)
    {
        if (!item.Enabled) return null;
        if (item.EffectiveRarity == "Common") return null;

        var pool = item.EffectivePool;
        var rarity = item.EffectiveRarity;
        var category = PricingGrid.GetSlotCategory(pool);
        var price = PricingGrid.GetPrice(category, rarity);

        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["Rarity"] = rarity,
            ["ValueOverride"] = price.ToString(),
            ["Unique"] = ""
        };
    }

    /// <summary>
    /// Generates skeleton override entries (self-referencing using) for mod items.
    /// Used for items from external mods that don't have definitions in AMP stat files.
    /// </summary>
    public static string GenerateSkeletonEntries(IReadOnlyList<ItemEntry> items)
    {
        var sb = new StringBuilder();

        foreach (var item in items)
        {
            var fields = ComputeFields(item);
            if (fields == null) continue;

            sb.AppendLine($"new entry \"{item.StatId}\"");
            sb.AppendLine($"type \"{item.StatType}\"");
            sb.AppendLine($"using \"{item.StatId}\"");
            foreach (var (key, value) in fields)
                sb.AppendLine($"data \"{key}\" \"{value}\"");
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
