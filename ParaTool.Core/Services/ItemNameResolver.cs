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
        catch (Exception ex) { AppLogger.Error("ItemNameResolver failed", ex); }

        return result;
    }

    /// <summary>
    /// Resolves both display names AND descriptions for items.
    /// </summary>
    public static (Dictionary<string, string> names, Dictionary<string, string> descriptions)
        ResolveFromPakExtended(string pakPath, Dictionary<string, List<string>> uuidToStatIds, string langCode)
    {
        var (names, descs, _, _) = ResolveFromPakFull(pakPath, uuidToStatIds, langCode);
        return (names, descs);
    }

    /// <summary>
    /// Full resolve returning names, descriptions, AND raw loca handles (for multi-language support).
    /// </summary>
    public static (Dictionary<string, string> names, Dictionary<string, string> descriptions,
                    Dictionary<string, string> nameHandles, Dictionary<string, string> descHandles)
        ResolveFromPakFull(string pakPath, Dictionary<string, List<string>> uuidToStatIds, string langCode)
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var descs = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var nhOut = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var dhOut = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (uuidToStatIds.Count == 0) return (names, descs, nhOut, dhOut);

        try
        {
            using var fs = File.OpenRead(pakPath);
            var header = PakReader.ReadHeader(fs);
            var entries = PakReader.ReadFileList(fs, header);

            var uuidsToFind = new HashSet<string>(uuidToStatIds.Keys, StringComparer.OrdinalIgnoreCase);
            var (nameHandles, descHandles) = ScanTemplatesForUuidsEx(fs, entries, uuidsToFind);

            // Export raw handles
            foreach (var (k, v) in nameHandles) nhOut.TryAdd(k, v);
            foreach (var (k, v) in descHandles) dhOut.TryAdd(k, v);

            // Collect ALL handles to resolve
            var allHandles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var h in nameHandles.Values) allHandles.Add(h);
            foreach (var h in descHandles.Values) allHandles.Add(h);

            var locaMap = ReadLocalization(fs, entries, allHandles, langCode);

            foreach (var (uuid, handle) in nameHandles)
                if (locaMap.TryGetValue(handle, out var name) && !string.IsNullOrWhiteSpace(name))
                    names[uuid] = name;

            foreach (var (uuid, handle) in descHandles)
                if (locaMap.TryGetValue(handle, out var desc) && !string.IsNullOrWhiteSpace(desc))
                    descs[uuid] = desc;
        }
        catch (Exception ex) { AppLogger.Error("ItemNameResolver failed", ex); }

        return (names, descs, nhOut, dhOut);
    }

    /// <summary>
    /// Reads ALL localization entries from a PAK for a given language.
    /// Returns handle → text dictionary (not filtered by specific handles).
    /// </summary>
    public static Dictionary<string, string> ReadAllLocalization(string pakPath, string langCode)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var fs = File.OpenRead(pakPath);
            var header = PakReader.ReadHeader(fs);
            var entries = PakReader.ReadFileList(fs, header);

            var targetLangs = new List<string>();
            foreach (var (bg3Lang, codes) in LangMapping)
                if (codes.Contains(langCode)) targetLangs.Add(bg3Lang);
            if (!targetLangs.Contains("English")) targetLangs.Add("English");

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
                    result.TryAdd(uid, text);
            }
        }
        catch (Exception ex) { AppLogger.Error("ItemNameResolver failed", ex); }
        return result;
    }

    /// <summary>
    /// Extended template scan returning both DisplayName and Description handles.
    /// </summary>
    private static (Dictionary<string, string> names, Dictionary<string, string> descs)
        ScanTemplatesForUuidsEx(FileStream fs, IReadOnlyList<FileEntry> entries, IReadOnlySet<string> uuids)
    {
        var nameResult = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var descResult = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var remaining = new HashSet<string>(uuids, StringComparer.OrdinalIgnoreCase);

        var rtFiles = entries.Where(e =>
            e.Path.Contains("RootTemplates", StringComparison.OrdinalIgnoreCase) &&
            (e.Path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase) ||
             e.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase))).ToList();

        // Pass 1: individual files
        foreach (var entry in rtFiles)
        {
            if (remaining.Count == 0) break;
            var fileName = Path.GetFileNameWithoutExtension(entry.Path);
            if (!remaining.Contains(fileName)) continue;

            var data = PakReader.ExtractFileData(fs, entry);
            var handle = LsfScanner.FindHandleNearUuid(data, fileName);
            if (handle != null)
            {
                nameResult[fileName] = handle;
                // For individual files, try to get second handle as description
                var text = System.Text.Encoding.Latin1.GetString(data);
                var handles = new List<string>();
                foreach (System.Text.RegularExpressions.Match m in
                    System.Text.RegularExpressions.Regex.Matches(text,
                        @"h[0-9a-f]{7,8}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{4}g[0-9a-f]{10,12}"))
                    handles.Add(m.Value);
                if (handles.Count >= 2 && handles[0] == handle)
                    descResult[fileName] = handles[1];

                remaining.Remove(fileName);
            }
        }

        if (remaining.Count == 0) return (nameResult, descResult);

        // Pass 2: merged files
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
            var (foundNames, foundDescs) = LsfScanner.FindHandlesForUuidsEx(data, remaining);
            foreach (var (uuid, handle) in foundNames)
            {
                nameResult[uuid] = handle;
                remaining.Remove(uuid);
            }
            foreach (var (uuid, handle) in foundDescs)
                descResult[uuid] = handle;
        }

        return (nameResult, descResult);
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
