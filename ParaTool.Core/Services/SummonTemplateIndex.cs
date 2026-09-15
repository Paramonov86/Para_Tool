using System.Reflection;

namespace ParaTool.Core.Services;

/// <summary>
/// Maps the character RootTemplate a <c>Summon(&lt;uuid&gt;, …)</c> functor spawns to the
/// Character stats entry it uses. Vanilla templates live in the game paks, which ParaTool never
/// reads at runtime, so the vanilla part is an embedded dump (tools/vanilladump) of every
/// template a vanilla Summon() call references. Character templates found in AMP and mod paks
/// are added at scan time through <see cref="Register"/>.
/// </summary>
public static class SummonTemplateIndex
{
    public sealed record Entry(string TemplateUuid, string Stats, string? NameHandle,
        string? NameEn, string? NameRu, bool IsVanilla);

    private static readonly object Gate = new();
    private static Dictionary<string, Entry>? _entries;

    private static Dictionary<string, Entry> Entries
    {
        get
        {
            if (_entries != null) return _entries;
            lock (Gate)
            {
                _entries ??= LoadVanilla();
                return _entries;
            }
        }
    }

    /// <summary>All known summonable character templates, vanilla first.</summary>
    public static IReadOnlyList<Entry> All
    {
        get
        {
            lock (Gate)
                return Entries.Values
                    .OrderBy(e => e.IsVanilla ? 0 : 1)
                    .ThenBy(e => e.NameEn ?? e.Stats, StringComparer.OrdinalIgnoreCase)
                    .ToList();
        }
    }

    public static Entry? Find(string? templateUuid)
    {
        if (string.IsNullOrWhiteSpace(templateUuid)) return null;
        lock (Gate)
            return Entries.GetValueOrDefault(templateUuid.Trim());
    }

    /// <summary>
    /// Adds a character template from a scanned pak. A vanilla entry for the same UUID is
    /// replaced only when the mod actually changes its stats.
    /// </summary>
    public static void Register(string templateUuid, string stats, string? nameHandle)
    {
        if (string.IsNullOrWhiteSpace(templateUuid) || string.IsNullOrWhiteSpace(stats)) return;
        lock (Gate)
        {
            if (Entries.TryGetValue(templateUuid, out var existing)
                && existing.Stats.Equals(stats, StringComparison.OrdinalIgnoreCase))
                return;
            Entries[templateUuid] = new Entry(templateUuid, stats, nameHandle, null, null, false);
        }
    }

    /// <summary>Display name in the given language, English as fallback, then the stats name.</summary>
    public static string DisplayName(Entry entry, string langCode, LocaService? loca = null)
    {
        if (langCode == "ru" && !string.IsNullOrEmpty(entry.NameRu)) return entry.NameRu;
        if (loca != null && !string.IsNullOrEmpty(entry.NameHandle))
        {
            var text = loca.ResolveHandle(entry.NameHandle, langCode);
            if (!string.IsNullOrEmpty(text)) return Localization.BbCode.FromBg3Xml(text);
        }
        return !string.IsNullOrEmpty(entry.NameEn) ? entry.NameEn : entry.Stats;
    }

    private static Dictionary<string, Entry> LoadVanilla()
    {
        var result = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        using var stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream("ParaTool.Core.Resources.Vanilla.Vanilla_SummonTemplates.tsv");
        if (stream == null) return result;
        using var reader = new StreamReader(stream);
        reader.ReadLine(); // header
        while (reader.ReadLine() is { } line)
        {
            var cols = line.Split('\t');
            if (cols.Length < 2 || string.IsNullOrEmpty(cols[0])) continue;
            string? Col(int i) => cols.Length > i && cols[i].Length > 0 ? cols[i] : null;
            result[cols[0]] = new Entry(cols[0], cols[1], Col(2), Col(3), Col(4), true);
        }
        return result;
    }
}
