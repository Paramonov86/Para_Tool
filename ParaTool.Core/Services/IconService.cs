using System.Collections.Concurrent;
using ParaTool.Core.Models;

namespace ParaTool.Core.Services;

/// <summary>
/// Info about an available icon.
/// </summary>
public sealed class IconInfo
{
    public required string Name { get; init; }
    public required string Source { get; init; } // "AMP", mod name, or "Vanilla"
    public byte[]? DdsData { get; set; }
}

/// <summary>
/// Lazy icon loader and browser — extracts DDS icons from pak files.
/// Supports individual ItemIcons and atlas slicing.
/// Caches PAK entry lists to avoid re-reading the same PAK multiple times.
/// </summary>
public sealed class IconService
{
    private readonly string[] _pakPaths;
    private readonly ConcurrentDictionary<string, byte[]?> _cache = new(StringComparer.OrdinalIgnoreCase);
    private List<IconInfo>? _allIcons;
    private bool _allIconsLoaded;
    private HashSet<string>? _allIconNames;

    // Cached PAK entry lists: pakPath → (entries, iconEntryIndex)
    private readonly ConcurrentDictionary<string, IReadOnlyList<FileEntry>> _pakEntries = new(StringComparer.OrdinalIgnoreCase);

    public IconService(string[] pakPaths)
    {
        _pakPaths = pakPaths;
    }

    /// <summary>
    /// Get raw DDS data for a single icon by name. Cached.
    /// </summary>
    public byte[]? GetIconDds(string iconName)
    {
        if (string.IsNullOrEmpty(iconName)) return null;
        if (_cache.TryGetValue(iconName, out var cached)) return cached;
        var dds = LoadSingleIcon(iconName);
        _cache[iconName] = dds;
        return dds;
    }

    /// <summary>
    /// Get list of ALL available icon names from all paks (ItemIcons/*.DDS).
    /// Loads on first call, then cached.
    /// </summary>
    public List<IconInfo> GetAllIcons()
    {
        if (_allIconsLoaded && _allIcons != null) return _allIcons;

        _allIcons = [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pakPath in _pakPaths)
        {
            if (!File.Exists(pakPath)) continue;
            try
            {
                var entries = GetEntries(pakPath);

                var pakName = Path.GetFileNameWithoutExtension(pakPath);
                var source = pakName.Contains("REL_Full_Ancient", StringComparison.OrdinalIgnoreCase) ? "AMP" : pakName;

                foreach (var entry in entries)
                {
                    if (!entry.Path.Contains("ItemIcons", StringComparison.OrdinalIgnoreCase) &&
                        !entry.Path.Contains("items_png", StringComparison.OrdinalIgnoreCase)) continue;
                    if (!entry.Path.EndsWith(".DDS", StringComparison.OrdinalIgnoreCase) &&
                        !entry.Path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase)) continue;

                    var name = Path.GetFileNameWithoutExtension(entry.Path);
                    if (seen.Contains(name)) continue;
                    seen.Add(name);

                    _allIcons.Add(new IconInfo { Name = name, Source = source });
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"IconService.GetAllIcons failed for {pakPath}", ex);
            }
        }

        _allIcons.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        _allIconsLoaded = true;
        return _allIcons;
    }

    /// <summary>
    /// Load DDS data for an IconInfo (lazy — only when thumbnail needed).
    /// </summary>
    public byte[]? LoadIconData(IconInfo icon)
    {
        if (icon.DdsData != null) return icon.DdsData;
        var dds = GetIconDds(icon.Name);
        icon.DdsData = dds;
        return dds;
    }

    /// <summary>
    /// Try to find icon by walking the stats using-chain.
    /// Tries: StatId, UsingBase, each ancestor in chain.
    /// Returns the icon name that was found, or null.
    /// </summary>
    public string? FindIconName(string statId, Parsing.StatsResolver? resolver)
    {
        if (resolver == null) return null;

        // Try statId itself
        if (HasIcon(statId)) return statId;

        // Walk using chain
        var current = statId;
        int depth = 0;
        while (current != null && depth < 20)
        {
            var entry = resolver.Get(current);
            if (entry == null) break;

            // Try this entry name
            if (HasIcon(entry.Name)) return entry.Name;

            current = entry.Using;
            depth++;
        }

        return null;
    }

    private bool HasIcon(string name)
    {
        // Use cached set of all icon names for fast lookup
        if (_allIconNames == null)
        {
            _allIconNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var icon in GetAllIcons())
                _allIconNames.Add(icon.Name);
        }
        return _allIconNames.Contains(name);
    }

    /// <summary>Read and cache PAK entry list (avoids re-reading header+filelist for each icon).</summary>
    private IReadOnlyList<FileEntry> GetEntries(string pakPath)
    {
        if (_pakEntries.TryGetValue(pakPath, out var cached)) return cached;

        using var fs = File.OpenRead(pakPath);
        var header = PakReader.ReadHeader(fs);
        var entries = PakReader.ReadFileList(fs, header);
        _pakEntries[pakPath] = entries;
        return entries;
    }

    private byte[]? LoadSingleIcon(string iconName)
    {
        foreach (var pakPath in _pakPaths)
        {
            if (!File.Exists(pakPath)) continue;
            try
            {
                var entries = GetEntries(pakPath);

                // Prefer 380x380 (ItemIcons) for quality, fallback to 144x144 (items_png)
                var entry = entries.FirstOrDefault(e =>
                    e.Path.EndsWith($"ItemIcons/{iconName}.DDS", StringComparison.OrdinalIgnoreCase) ||
                    e.Path.EndsWith($"ItemIcons/{iconName}.dds", StringComparison.OrdinalIgnoreCase));

                if (entry.Path == null)
                    entry = entries.FirstOrDefault(e =>
                        e.Path.EndsWith($"items_png/{iconName}.DDS", StringComparison.OrdinalIgnoreCase) ||
                        e.Path.EndsWith($"items_png/{iconName}.dds", StringComparison.OrdinalIgnoreCase));

                // Fallback: Tooltips/Icons/ (some mods store icons here)
                if (entry.Path == null)
                    entry = entries.FirstOrDefault(e =>
                        e.Path.EndsWith($"Icons/{iconName}.DDS", StringComparison.OrdinalIgnoreCase) ||
                        e.Path.EndsWith($"Icons/{iconName}.dds", StringComparison.OrdinalIgnoreCase) ||
                        e.Path.EndsWith($"Icons/{iconName}.png", StringComparison.OrdinalIgnoreCase));

                if (entry.Path != null)
                {
                    using var fs = File.OpenRead(pakPath);
                    var data = PakReader.ExtractFileData(fs, entry);
                    if (data.Length > 0) return data;
                }
            }
            catch (Exception ex)
            {
                AppLogger.Error($"IconService.LoadSingleIcon failed for {iconName} in {pakPath}", ex);
            }
        }
        return null;
    }
}
