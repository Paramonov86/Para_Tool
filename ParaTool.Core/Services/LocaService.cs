using System.Collections.Concurrent;

namespace ParaTool.Core.Services;

/// <summary>
/// On-demand localization loader. Caches loaded languages.
/// Loads handle → text mappings from pak files for any BG3 language.
/// </summary>
public sealed class LocaService
{
    private readonly string[] _pakPaths;
    private readonly ConcurrentDictionary<string, Dictionary<string, string>> _cache = new();

    /// <summary>BG3 folder name → ParaTool lang code.</summary>
    public static readonly Dictionary<string, string> Bg3ToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["English"] = "en", ["Russian"] = "ru", ["German"] = "de", ["French"] = "fr",
        ["Spanish"] = "es", ["LatinSpanish"] = "es", ["Italian"] = "it", ["Polish"] = "pl",
        ["Japanese"] = "ja", ["Korean"] = "ko", ["Turkish"] = "tr", ["Ukrainian"] = "uk",
        ["Chinese"] = "zh", ["ChineseTraditional"] = "zh", ["BrazilianPortuguese"] = "pt"
    };

    /// <summary>ParaTool lang code → BG3 folder name.</summary>
    public static readonly Dictionary<string, string> CodeToBg3 = new(StringComparer.OrdinalIgnoreCase)
    {
        ["en"] = "English", ["ru"] = "Russian", ["de"] = "German", ["fr"] = "French",
        ["es"] = "Spanish", ["it"] = "Italian", ["pl"] = "Polish",
        ["ja"] = "Japanese", ["ko"] = "Korean", ["tr"] = "Turkish", ["uk"] = "Ukrainian",
        ["zh"] = "Chinese", ["pt"] = "BrazilianPortuguese"
    };

    public LocaService(string[] pakPaths)
    {
        _pakPaths = pakPaths;
    }

    /// <summary>
    /// Pre-populate cache with already-loaded loca map (from scan).
    /// </summary>
    public void SeedCache(string langCode, Dictionary<string, string> locaMap)
    {
        _cache[langCode] = locaMap;
    }

    /// <summary>
    /// Get loca map for a given language code. Loads from paks on first request, then cached.
    /// Returns null if language has no loca data in any pak.
    /// </summary>
    public Dictionary<string, string>? GetLocaMap(string langCode)
    {
        if (_cache.TryGetValue(langCode, out var cached))
            return cached;

        // Load from all paks for this language
        var combined = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var pakPath in _pakPaths)
        {
            if (!File.Exists(pakPath)) continue;
            try
            {
                var loca = ItemNameResolver.ReadAllLocalization(pakPath, langCode);
                foreach (var (handle, text) in loca)
                    combined.TryAdd(handle, text);
            }
            catch (Exception ex) { AppLogger.Warn($"LocaService: skipping pak {pakPath}: {ex.Message}"); }
        }

        if (combined.Count == 0)
        {
            // No data for this language — don't cache empty to allow retry
            return null;
        }

        _cache[langCode] = combined;
        return combined;
    }

    /// <summary>
    /// Resolve a handle field (e.g. "hXXXXgXXXX;1") to text for a given language.
    /// </summary>
    public string? ResolveHandle(string handleField, string langCode)
    {
        var map = GetLocaMap(langCode);
        if (map == null) return null;

        var handle = handleField.Split(';')[0].Trim();
        if (string.IsNullOrEmpty(handle)) return null;

        if (map.TryGetValue(handle, out var text)) return text;

        // Prefix match
        foreach (var (key, value) in map)
            if (key.StartsWith(handle, StringComparison.OrdinalIgnoreCase))
                return value;

        return null;
    }
}
