using System.Globalization;
using System.Text.RegularExpressions;
using ParaTool.Core.Textures;

namespace ParaTool.Core.Services;

/// <summary>
/// Parses vanilla BG3 icon atlases (Icons_Items*.lsx + .dds) from unpacked game data.
/// Extracts individual 144x144 icon tiles by UV coordinates.
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
        public byte[]? RgbaData { get; set; } // 144x144 RGBA
    }

    private readonly string _unpackedDataPath;
    private List<AtlasIcon>? _icons;
    private readonly Dictionary<string, (int w, int h, byte[] rgba)> _atlasCache = new();

    public VanillaIconAtlasService(string unpackedDataPath)
    {
        _unpackedDataPath = unpackedDataPath;
    }

    /// <summary>
    /// Load all icon definitions from Icons_Items*.lsx files.
    /// Does NOT decode atlases yet — call ExtractIcon() to get pixel data.
    /// </summary>
    public List<AtlasIcon> LoadIconList()
    {
        if (_icons != null) return _icons;

        _icons = [];
        var guiDir = Path.Combine(_unpackedDataPath, "Shared", "Public", "Shared", "GUI");
        if (!Directory.Exists(guiDir)) return _icons;

        var lsxFiles = Directory.GetFiles(guiDir, "Icons_Items*.lsx");
        foreach (var lsxPath in lsxFiles.OrderBy(f => f))
        {
            var atlasName = Path.GetFileNameWithoutExtension(lsxPath);
            var entries = ParseLsx(lsxPath, atlasName);
            _icons.AddRange(entries);
        }

        return _icons;
    }

    /// <summary>
    /// Extract 144x144 RGBA pixel data for a specific icon.
    /// Decodes the parent atlas DDS on first use (cached).
    /// </summary>
    public byte[]? ExtractIcon(AtlasIcon icon)
    {
        if (icon.RgbaData != null) return icon.RgbaData;

        var atlas = LoadAtlas(icon.AtlasName);
        if (atlas == null) return null;

        var (aw, ah, rgba) = atlas.Value;

        // UV to pixel coords
        int x1 = (int)(icon.U1 * aw);
        int y1 = (int)(icon.V1 * ah);
        int x2 = (int)(icon.U2 * aw);
        int y2 = (int)(icon.V2 * ah);

        int tileW = x2 - x1;
        int tileH = y2 - y1;
        if (tileW <= 0 || tileH <= 0 || tileW > 256 || tileH > 256) return null;

        // Crop tile from atlas
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

    /// <summary>Width/height of extracted tile (from UV).</summary>
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

        // Find DDS file
        var ddsPath = FindAtlasDds(atlasName);
        if (ddsPath == null || !File.Exists(ddsPath)) return null;

        try
        {
            var ddsData = File.ReadAllBytes(ddsPath);
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

    private string? FindAtlasDds(string atlasName)
    {
        var searchDirs = new[]
        {
            Path.Combine(_unpackedDataPath, "Icons", "Public", "Shared", "Assets", "Textures", "Icons"),
            Path.Combine(_unpackedDataPath, "Icons", "Public", "SharedDev", "Assets", "Textures", "Icons"),
            Path.Combine(_unpackedDataPath, "Shared", "Public", "Shared", "Assets", "Textures", "Icons"),
        };

        foreach (var dir in searchDirs)
        {
            if (!Directory.Exists(dir)) continue;
            var file = Path.Combine(dir, $"{atlasName}.dds");
            if (File.Exists(file)) return file;
        }

        return null;
    }

    private static List<AtlasIcon> ParseLsx(string path, string atlasName)
    {
        var icons = new List<AtlasIcon>();
        try
        {
            var text = File.ReadAllText(path);
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
        }
        catch { }
        return icons;
    }
}
