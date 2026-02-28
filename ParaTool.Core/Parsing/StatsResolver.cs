namespace ParaTool.Core.Parsing;

public sealed class StatsResolver
{
    private const int MaxInheritanceDepth = 20;

    private readonly Dictionary<string, StatsEntry> _entries = new(StringComparer.OrdinalIgnoreCase);

    public void AddEntries(IEnumerable<StatsEntry> entries)
    {
        foreach (var entry in entries)
            _entries[entry.Name] = entry;
    }

    public StatsEntry? Get(string name)
    {
        _entries.TryGetValue(name, out var entry);
        return entry;
    }

    public string? Resolve(string entryName, string property)
    {
        int depth = 0;
        var current = entryName;

        while (current != null && depth < MaxInheritanceDepth)
        {
            if (!_entries.TryGetValue(current, out var entry))
                return null;

            if (entry.Data.TryGetValue(property, out var value))
                return value;

            current = entry.Using;
            depth++;
        }

        return null;
    }

    public Dictionary<string, string> ResolveAll(string entryName)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        CollectInherited(entryName, result, 0);
        return result;
    }

    private void CollectInherited(string? name, Dictionary<string, string> result, int depth)
    {
        if (name == null || depth >= MaxInheritanceDepth) return;
        if (!_entries.TryGetValue(name, out var entry)) return;

        // Resolve parent first so child values override
        CollectInherited(entry.Using, result, depth + 1);

        foreach (var kvp in entry.Data)
            result[kvp.Key] = kvp.Value;
    }

    public IReadOnlyDictionary<string, StatsEntry> AllEntries => _entries;
}
