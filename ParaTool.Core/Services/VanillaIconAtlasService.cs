using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using ParaTool.Core.Textures;

namespace ParaTool.Core.Services;

/// <summary>
/// Parses vanilla BG3 icon atlases from embedded resources.
/// Icons_Items*.lsx (UV coords) + Icons_Items*.dds (atlas textures).
/// Extracts individual ~144x144 tiles.
/// </summary>
public sealed class VanillaIconAtlasService
{
    public sealed class AtlasIcon
    {
        public required string Name { get; init; }
        public required string AtlasName { get; init; }
        public float U1 { get; init; }
        public float V1 { get; init; }
        public float U2 { get; init; }
        public float V2 { get; init; }
        public byte[]? RgbaData { get; set; }
    }

    private static readonly string ResourcePrefix = "ParaTool.Core.Resources.VanillaIcons.";
    private List<AtlasIcon>? _icons;
    private readonly Dictionary<string, (int w, int h, byte[] rgba)> _atlasCache = new();
    private static Dictionary<string, string>? _uuidToIcon;

    /// <summary>
    /// Load all icon definitions from embedded Icons_Items*.lsx.
    /// </summary>
    public List<AtlasIcon> LoadIconList()
    {
        if (_icons != null) return _icons;

        _icons = [];
        var assembly = typeof(VanillaIconAtlasService).Assembly;
        var lsxResources = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix) && n.EndsWith(".lsx"))
            .OrderBy(n => n);

        foreach (var resName in lsxResources)
        {
            var atlasName = resName[ResourcePrefix.Length..^".lsx".Length];
            using var stream = assembly.GetManifestResourceStream(resName);
            if (stream == null) continue;
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            _icons.AddRange(ParseLsx(text, atlasName));
        }

        return _icons;
    }

    /// <summary>
    /// Extract RGBA pixel data for a specific icon tile.
    /// </summary>
    public byte[]? ExtractIcon(AtlasIcon icon)
    {
        if (icon.RgbaData != null) return icon.RgbaData;

        var atlas = LoadAtlas(icon.AtlasName);
        if (atlas == null) return null;

        var (aw, ah, rgba) = atlas.Value;

        int x1 = (int)(icon.U1 * aw);
        int y1 = (int)(icon.V1 * ah);
        int x2 = (int)(icon.U2 * aw);
        int y2 = (int)(icon.V2 * ah);

        int tileW = x2 - x1;
        int tileH = y2 - y1;
        if (tileW <= 0 || tileH <= 0 || tileW > 512 || tileH > 512) return null;

        var tile = new byte[tileW * tileH * 4];
        for (int row = 0; row < tileH; row++)
        {
            var srcOff = ((y1 + row) * aw + x1) * 4;
            var dstOff = row * tileW * 4;
            if (srcOff + tileW * 4 <= rgba.Length)
                Array.Copy(rgba, srcOff, tile, dstOff, tileW * 4);
        }

        icon.RgbaData = tile;
        return tile;
    }

    public (int w, int h) GetTileSize(AtlasIcon icon)
    {
        var atlas = LoadAtlas(icon.AtlasName);
        if (atlas == null) return (144, 144);
        var (aw, ah, _) = atlas.Value;
        return ((int)((icon.U2 - icon.U1) * aw), (int)((icon.V2 - icon.V1) * ah));
    }

    private (int w, int h, byte[] rgba)? LoadAtlas(string atlasName)
    {
        if (_atlasCache.TryGetValue(atlasName, out var cached))
            return cached;

        var assembly = typeof(VanillaIconAtlasService).Assembly;
        var resName = $"{ResourcePrefix}{atlasName}.dds";
        using var stream = assembly.GetManifestResourceStream(resName);
        if (stream == null) return null;

        try
        {
            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            var ddsData = ms.ToArray();
            var (w, h, rgba) = DdsReader.Decode(ddsData);
            var result = (w, h, rgba);
            _atlasCache[atlasName] = result;
            return result;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Get icon name for a RootTemplate UUID from embedded vanilla mapping.
    /// </summary>
    public static string? GetIconNameByUuid(string uuid)
    {
        if (_uuidToIcon == null)
        {
            _uuidToIcon = new(StringComparer.OrdinalIgnoreCase);
            var assembly = typeof(VanillaIconAtlasService).Assembly;
            var resName = $"{ResourcePrefix}uuid_to_icon.tsv";
            using var stream = assembly.GetManifestResourceStream(resName);
            if (stream != null)
            {
                using var reader = new StreamReader(stream);
                string? line;
                while ((line = reader.ReadLine()) != null)
                {
                    var parts = line.Split('\t');
                    if (parts.Length == 2)
                        _uuidToIcon.TryAdd(parts[0], parts[1]);
                }
            }
        }
        return _uuidToIcon.TryGetValue(uuid, out var icon) ? icon : null;
    }

    private static List<AtlasIcon> ParseLsx(string text, string atlasName)
    {
        var icons = new List<AtlasIcon>();
        var matches = Regex.Matches(text,
            @"<node id=""IconUV"">\s*" +
            @"<attribute id=""MapKey""[^>]*value=""([^""]+)""/>\s*" +
            @"<attribute id=""U1""[^>]*value=""([^""]+)""/>\s*" +
            @"<attribute id=""U2""[^>]*value=""([^""]+)""/>\s*" +
            @"<attribute id=""V1""[^>]*value=""([^""]+)""/>\s*" +
            @"<attribute id=""V2""[^>]*value=""([^""]+)""/>",
            RegexOptions.Singleline);

        foreach (Match m in matches)
        {
            icons.Add(new AtlasIcon
            {
                Name = m.Groups[1].Value,
                AtlasName = atlasName,
                U1 = float.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture),
                U2 = float.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture),
                V1 = float.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture),
                V2 = float.Parse(m.Groups[5].Value, CultureInfo.InvariantCulture),
            });
        }
        return icons;
    }
}
