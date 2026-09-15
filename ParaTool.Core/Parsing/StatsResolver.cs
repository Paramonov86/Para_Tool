namespace ParaTool.Core.Parsing;

public sealed class StatsResolver
{
    private const int MaxInheritanceDepth = 20;

    private readonly Dictionary<string, StatsEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Definitions replaced by a later one of the same name, oldest first. The game reads a later
    /// <c>new entry "X"</c> that <c>using "X"</c> as an extension of the earlier X (later layers of the
    /// vanilla dump, AMP's spell rebalances), so resolution continues into the definition it replaced.
    /// </summary>
    private readonly Dictionary<string, List<StatsEntry>> _replaced = new(StringComparer.OrdinalIgnoreCase);

    public void AddEntries(IEnumerable<StatsEntry> entries)
    {
        foreach (var entry in entries)
        {
            // Don't let non-Armor/Weapon overwrite Armor/Weapon entries
            if (_entries.TryGetValue(entry.Name, out var existing))
            {
                if ((existing.Type is "Armor" or "Weapon") &&
                    entry.Type is not "Armor" and not "Weapon")
                    continue; // Skip — preserve item entry
                if (!ReferenceEquals(existing, entry))
                {
                    if (!_replaced.TryGetValue(entry.Name, out var older))
                        _replaced[entry.Name] = older = [];
                    older.Add(existing);
                }
            }
            _entries[entry.Name] = entry;
        }
    }

    /// <summary>
    /// The entry an entry inherits from. A self-<c>using</c> entry inherits from the definition of the
    /// same name it replaced; <paramref name="layer"/> counts how far back that is.
    /// </summary>
    private (StatsEntry? entry, int layer) Parent(StatsEntry entry, int layer)
    {
        if (entry.Using == null) return (null, 0);
        if (!entry.Using.Equals(entry.Name, StringComparison.OrdinalIgnoreCase))
            return (Get(entry.Using), 0);
        return _replaced.TryGetValue(entry.Name, out var older) && layer < older.Count
            ? (older[older.Count - 1 - layer], layer + 1)
            : (null, 0);
    }

    public StatsEntry? Get(string name)
    {
        _entries.TryGetValue(name, out var entry);
        return entry;
    }

    public string? Resolve(string entryName, string property)
    {
        var (entry, layer) = (Get(entryName), 0);
        for (int depth = 0; entry != null && depth < MaxInheritanceDepth; depth++)
        {
            if (entry.Data.TryGetValue(property, out var value))
                return value;
            (entry, layer) = Parent(entry, layer);
        }

        return null;
    }

    public Dictionary<string, string> ResolveAll(string entryName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CollectInherited(Get(entryName), 0, result, 0);
        return result;
    }

    private void CollectInherited(StatsEntry? entry, int layer, Dictionary<string, string> result, int depth)
    {
        if (entry == null || depth >= MaxInheritanceDepth) return;

        // Resolve parent first so child values override
        var (parent, parentLayer) = Parent(entry, layer);
        CollectInherited(parent, parentLayer, result, depth + 1);

        foreach (var kvp in entry.Data)
            result[kvp.Key] = kvp.Value;
    }

    public IReadOnlyDictionary<string, StatsEntry> AllEntries => _entries;

    /// <summary>
    /// Every definition in load order per name — replaced ones first, then the current one. Adding
    /// these to another resolver keeps what self-<c>using</c> entries extend; <see cref="AllEntries"/>
    /// holds only the last definition of each name.
    /// </summary>
    public IEnumerable<StatsEntry> Definitions =>
        _entries.Values.SelectMany(e => _replaced.TryGetValue(e.Name, out var older) ? older.Append(e) : [e]);
}
