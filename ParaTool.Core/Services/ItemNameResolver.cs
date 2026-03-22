using ParaTool.Core.Models;
using ParaTool.Core.Parsing;

namespace ParaTool.Core.Services;

/// <summary>
/// Resolves item StatIds to their in-game display names by:
/// 1. Using RootTemplate UUIDs from Stats data to find templates in .lsf files
/// 2. Scanning templates for DisplayName handles near the UUID
/// 3. Reading localization files for handle → translated name
/// </summary>
public sealed class ItemNameResolver
{
    private static readonly Dictionary<string, string[]> LangMapping = new(StringComparer.OrdinalIgnoreCase)
    {
        ["English"] = new[] { "en" },
        ["Russian"] = new[] { "ru" },
        ["German"] = new[] { "de" },
        ["French"] = new[] { "fr" },
        ["Spanish"] = new[] { "es" },
        ["LatinSpanish"] = new[] { "es" },
        ["Italian"] = new[] { "it" },
        ["Polish"] = new[] { "pl" },
        ["Japanese"] = new[] { "ja" },
        ["Korean"] = new[] { "ko" },
        ["Turkish"] = new[] { "tr" },
        ["Ukrainian"] = new[] { "uk" },
        ["Chinese"] = new[] { "zh" },
        ["ChineseTraditional"] = new[] { "zh" },
        ["BrazilianPortuguese"] = new[] { "pt" },
    };

    /// <summary>
    /// Resolves display names for items using RootTemplate UUIDs.
    /// </summary>
    /// <param name="pakPath">Path to the .pak file</param>
    /// <param name="uuidToStatIds">RootTemplate UUID → list of StatIds that use it</param>
    /// <param name="langCode">ParaTool language code (e.g. "ru", "en")</param>
    /// <returns>UUID → display name mapping</returns>
    public static Dictionary<string, string> ResolveFromPak(
        string pakPath, Dictionary<string, List<string>> uuidToStatIds, string langCode)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (uuidToStatIds.Count == 0) return result;

        try
        {
            using var fs = File.OpenRead(pakPath);
            var header = PakReader.ReadHeader(fs);
            var entries = PakReader.ReadFileList(fs, header);

            // Step 1: Find DisplayName handles by scanning template files for UUIDs
            var uuidsToFind = new HashSet<string>(uuidToStatIds.Keys, StringComparer.OrdinalIgnoreCase);
            var uuidToHandle = ScanTemplatesForUuids(fs, entries, uuidsToFind);
            if (uuidToHandle.Count == 0) return result;

            // Step 2: Read localization
            var handles = new HashSet<string>(uuidToHandle.Values, StringComparer.OrdinalIgnoreCase);
            var locaMap = ReadLocalization(fs, entries, handles, langCode);

            // Step 3: UUID → handle → name
            foreach (var (uuid, handle) in uuidToHandle)
            {
                if (locaMap.TryGetValue(handle, out var name) && !string.IsNullOrWhiteSpace(name))
                    result[uuid] = name;
            }
        }
        catch { }

        return result;
    }

    /// <summary>
    /// Scans ALL .lsf and .lsx template files in a PAK for RootTemplate UUIDs.
    /// For each UUID found, extracts the nearest TranslatedString handle (DisplayName).
    /// </summary>
    private static Dictionary<string, string> ScanTemplatesForUuids(
        FileStream fs, IReadOnlyList<FileEntry> entries, IReadOnlySet<string> uuids)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var remaining = new HashSet<string>(uuids, StringComparer.OrdinalIgnoreCase);

        // Scan RootTemplates — individual files first (filename = UUID), then merged
        var rtFiles = entries.Where(e =>
            e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) &&
            (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
             e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase))).ToList();

        // Pass 1: individual files (fast — match by filename)
        foreach (var entry in rtFiles)
        {
            if (remaining.Count == 0) break;

            var fileName = Path.GetFileNameWithoutExtension(entry.Path);
            if (!remaining.Contains(fileName)) continue;

            var data = PakReader.ExtractFileData(fs, entry);
            var handle = LsfScanner.FindHandleNearUuid(data, fileName);
            if (handle != null)
            {
                result[fileName] = handle;
                remaining.Remove(fileName);
            }
        }

        if (remaining.Count == 0) return result;

        // Pass 2: merged/large files (RootTemplates/_merged, Content/*, Globals/*)
        var mergedFiles = entries.Where(e =>
            (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
             e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) &&
            (e.Path.Contains("_merged", StringComparison.OrdinalIgnoreCase) ||
             e.Path.Contains("Content", StringComparison.OrdinalIgnoreCase) ||
             e.Path.Contains("Globals", StringComparison.OrdinalIgnoreCase))).ToList();

        foreach (var entry in mergedFiles)
        {
            if (remaining.Count == 0) break;

            var data = PakReader.ExtractFileData(fs, entry);

            // Decompress once, search all remaining UUIDs
            var found = LsfScanner.FindHandlesForUuids(data, remaining);
            foreach (var (uuid, handle) in found)
            {
                result[uuid] = handle;
                remaining.Remove(uuid);
            }
        }

        return result;
    }

    public static Dictionary<string, string> ReadLocalization(
        FileStream fs, IReadOnlyList<FileEntry> entries, IReadOnlySet<string> handles, string langCode)
    {
        var locaMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (handles.Count == 0) return locaMap;

        var targetLangs = new List<string>();
        foreach (var (bg3Lang, codes) in LangMapping)
        {
            if (codes.Contains(langCode))
                targetLangs.Add(bg3Lang);
        }

        if (!targetLangs.Contains("English"))
            targetLangs.Add("English");

        var locaFiles = entries.Where(e =>
            e.Path.Contains("Localization", StringComparison.OrdinalIgnoreCase) &&
            e.Path.EndsWith(".xml", StringComparison.OrdinalIgnoreCase) &&
            targetLangs.Any(lang => e.Path.Contains(lang, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(e => e.Path.Contains("English", StringComparison.OrdinalIgnoreCase) ? 1 : 0)
            .ToList();

        foreach (var entry in locaFiles)
        {
            var data = PakReader.ExtractFileData(fs, entry);
            if (!LocaReader.IsXmlLoca(data)) continue;

            var parsed = LocaReader.ParseXml(data);
            foreach (var (uid, text) in parsed)
            {
                // Exact match first
                if (handles.Contains(uid) && !locaMap.ContainsKey(uid))
                {
                    locaMap[uid] = text;
                    continue;
                }

                // Prefix match: LSF may store truncated handles (null-terminated)
                foreach (var h in handles)
                {
                    if (!locaMap.ContainsKey(h) &&
                        uid.StartsWith(h, StringComparison.OrdinalIgnoreCase))
                    {
                        locaMap[h] = text;
                        break;
                    }
                }
            }

            if (locaMap.Count >= handles.Count) break;
        }

        return locaMap;
    }
}
