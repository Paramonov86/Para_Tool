using System.Collections;

namespace ParaTool.Core.Parsing;

/// <summary>
/// Stats entries by name, with <c>using</c> inheritance. Names are the game's: case-sensitive and
/// shared by every type — AMP has the weapon <c>WPN_Longsword_l</c> and the status
/// <c>WPN_LONGSWORD_L</c>, the game the spells <c>Projectile_Jump</c> and <c>Projectile_JUMP</c>,
/// and no name repeats exactly across types. A name asked for in another case finds its entry
/// when only one matches ignoring case.
/// </summary>
public sealed class StatsResolver
{
    private const int MaxInheritanceDepth = 20;

    private readonly Dictionary<string, StatsEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>Exact name by its case-insensitive form; null where two entries share it.</summary>
    private readonly Dictionary<string, string?> _byFolded = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Definitions replaced by a later one of the same name, oldest first. The game reads a later
    /// <c>new entry "X"</c> that <c>using "X"</c> as an extension of the earlier X (later layers of the
    /// vanilla dump, AMP's spell rebalances), so resolution continues into the definition it replaced.
    /// </summary>
    private readonly Dictionary<string, List<StatsEntry>> _replaced = new(StringComparer.Ordinal);

    public StatsResolver() => AllEntries = new EntryMap(this);

    public void AddEntries(IEnumerable<StatsEntry> entries)
    {
        foreach (var entry in entries)
        {
            if (_entries.TryGetValue(entry.Name, out var existing))
            {
                // Don't let non-Armor/Weapon overwrite Armor/Weapon entries
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
            else
                _byFolded[entry.Name] = _byFolded.ContainsKey(entry.Name) ? null : entry.Name;
            _entries[entry.Name] = entry;
        }
    }

    /// <summary>The exact name an entry is stored under: the name itself, or the only one matching it ignoring case.</summary>
    private string? ExactName(string name)
    {
        if (_entries.ContainsKey(name)) return name;
        return _byFolded.GetValueOrDefault(name);
    }

    /// <summary>
    /// The entry an entry inherits from. A self-<c>using</c> entry inherits from the definition of the
    /// same name it replaced; <paramref name="layer"/> counts how far back that is.
    /// </summary>
    private (StatsEntry? entry, int layer) Parent(StatsEntry entry, int layer)
    {
        if (entry.Using == null) return (null, 0);
        var parent = Get(entry.Using);
        if (parent != null && !ReferenceEquals(parent, entry) && parent.Name != entry.Name) return (parent, 0);
        // `using` its own name: the earlier definition of that exact name.
        if (!entry.Using.Equals(entry.Name, StringComparison.OrdinalIgnoreCase)) return (null, 0);
        return _replaced.TryGetValue(entry.Name, out var older) && layer < older.Count
            ? (older[older.Count - 1 - layer], layer + 1)
            : (null, 0);
    }

    public StatsEntry? Get(string name)
    {
        var exact = ExactName(name);
        return exact == null ? null : _entries[exact];
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

    /// <summary>
    /// The current definition of every name. Looking a name up here finds it in another case too
    /// when only one entry matches; enumerating gives every entry, case variants included.
    /// </summary>
    public IReadOnlyDictionary<string, StatsEntry> AllEntries { get; }

    /// <summary>
    /// Every definition in load order per name — replaced ones first, then the current one. Adding
    /// these to another resolver keeps what self-<c>using</c> entries extend; <see cref="AllEntries"/>
    /// holds only the last definition of each name.
    /// </summary>
    public IEnumerable<StatsEntry> Definitions =>
        _entries.Values.SelectMany(e => _replaced.TryGetValue(e.Name, out var older) ? older.Append(e) : [e]);

    private sealed class EntryMap(StatsResolver owner) : IReadOnlyDictionary<string, StatsEntry>
    {
        public StatsEntry this[string key] => owner.Get(key) ?? throw new KeyNotFoundException(key);
        public IEnumerable<string> Keys => owner._entries.Keys;
        public IEnumerable<StatsEntry> Values => owner._entries.Values;
        public int Count => owner._entries.Count;
        public bool ContainsKey(string key) => owner.ExactName(key) != null;

        public bool TryGetValue(string key, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out StatsEntry value)
        {
            value = owner.Get(key);
            return value != null;
        }

        public IEnumerator<KeyValuePair<string, StatsEntry>> GetEnumerator() => owner._entries.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
