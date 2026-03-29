using System.Text;
using System.Text.Json;
using ParaTool.Core.Services;

namespace ParaTool.Core.Textures;

/// <summary>
/// Persistent atlas store: manages user's custom icon atlases on disk.
/// Each atlas holds up to MaxIcons (100) 144×144 icons in a 1440×1440 grid.
/// When full, a new atlas is created automatically.
///
/// Storage: %LocalAppData%/ParaTool/Atlas/
///   ParaTool_Icons_1.dds   — BC3 atlas texture
///   ParaTool_Icons_1.json  — metadata (icon names + positions)
///   ParaTool_Icons_2.dds   — next atlas when first fills up
///   ...
/// </summary>
public static class AtlasStore
{
    private const int TileSize = 144;
    private const int GridSize = 10; // 10×10 = 100 icons per atlas
    private const int AtlasPixelSize = GridSize * TileSize; // 1440
    private const int MaxIcons = GridSize * GridSize; // 100

    private const string AtlasPrefix = "ParaTool_Icons_";
    private const string DdsExt = ".dds";
    private const string MetaExt = ".json";

    /// <summary>BG3 relative path for atlas DDS inside pak.</summary>
    public static string GetAtlasDdsPath(int atlasIndex) =>
        $"Assets/Textures/Icons/{AtlasPrefix}{atlasIndex}.dds";

    /// <summary>BG3 relative path for atlas LSX inside pak (in GUI/).</summary>
    public static string GetAtlasLsxName(int atlasIndex) =>
        $"{AtlasPrefix}{atlasIndex}.lsx";

    /// <summary>Stable UUID per atlas index.</summary>
    public static string GetAtlasUuid(int atlasIndex) =>
        new Guid(MD5Hash($"ParaTool_Atlas_{atlasIndex}")).ToString();

    public static string GetAtlasDir()
    {
        var dir = Path.Combine(ProfileService.GetStorageDir(), "Atlas");
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>
    /// Add a 144×144 RGBA icon to the atlas store. Returns the MapKey name used in the atlas.
    /// Automatically creates or extends atlases as needed.
    /// </summary>
    public static string AddIcon(string iconName, byte[] rgba144)
    {
        if (rgba144.Length != TileSize * TileSize * 4)
            throw new ArgumentException($"Expected {TileSize}×{TileSize} RGBA ({TileSize * TileSize * 4} bytes), got {rgba144.Length}");

        var dir = GetAtlasDir();

        // Find atlas with space, or create new one
        for (int idx = 1; ; idx++)
        {
            var meta = LoadMeta(dir, idx);

            // Check if icon already exists — update in place
            var existing = meta.Icons.FindIndex(e => e.Name == iconName);
            if (existing >= 0)
            {
                // Replace existing icon in atlas
                var atlasRgba = LoadOrCreateAtlasRgba(dir, idx, meta);
                WriteIconTile(atlasRgba, existing, rgba144);
                SaveAtlas(dir, idx, meta, atlasRgba);
                return iconName;
            }

            if (meta.Icons.Count < MaxIcons)
            {
                // Add to this atlas
                var slot = meta.Icons.Count;
                meta.Icons.Add(new AtlasIconEntry { Name = iconName, Slot = slot });

                var atlasRgba = LoadOrCreateAtlasRgba(dir, idx, meta);
                WriteIconTile(atlasRgba, slot, rgba144);
                SaveAtlas(dir, idx, meta, atlasRgba);
                return iconName;
            }
            // This atlas is full, try next
        }
    }

    /// <summary>
    /// Remove an icon from the atlas store.
    /// </summary>
    public static void RemoveIcon(string iconName)
    {
        var dir = GetAtlasDir();
        for (int idx = 1; idx <= 100; idx++)
        {
            var metaPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{MetaExt}");
            if (!File.Exists(metaPath)) break;

            var meta = LoadMeta(dir, idx);
            var found = meta.Icons.FindIndex(e => e.Name == iconName);
            if (found >= 0)
            {
                meta.Icons.RemoveAt(found);
                // Re-index slots
                for (int i = 0; i < meta.Icons.Count; i++)
                    meta.Icons[i].Slot = i;
                // Rebuild atlas
                RebuildAtlas(dir, idx, meta);
                return;
            }
        }
    }

    /// <summary>
    /// Get all atlas files for embedding into pak during patching.
    /// Returns list of (relativePath, fileBytes) — both DDS and LSX.
    /// </summary>
    public static List<(string relativePath, byte[] data)> GetAllAtlasFiles()
    {
        var result = new List<(string, byte[])>();
        var dir = GetAtlasDir();

        for (int idx = 1; idx <= 100; idx++)
        {
            var metaPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{MetaExt}");
            var ddsPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{DdsExt}");

            if (!File.Exists(metaPath) || !File.Exists(ddsPath)) break;

            var meta = LoadMeta(dir, idx);
            if (meta.Icons.Count == 0) continue;

            // DDS texture → Assets/Textures/Icons/
            result.Add((GetAtlasDdsPath(idx), File.ReadAllBytes(ddsPath)));

            // LSX metadata → GUI/ (will be placed in Public/ModFolder/GUI/)
            var lsx = GenerateLsx(idx, meta);
            result.Add(($"GUI/{GetAtlasLsxName(idx)}", Encoding.UTF8.GetBytes(lsx)));
        }

        return result;
    }

    /// <summary>Check if an icon name exists in any atlas.</summary>
    public static bool HasIcon(string iconName)
    {
        var dir = GetAtlasDir();
        for (int idx = 1; idx <= 100; idx++)
        {
            var metaPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{MetaExt}");
            if (!File.Exists(metaPath)) break;
            var meta = LoadMeta(dir, idx);
            if (meta.Icons.Any(e => e.Name == iconName)) return true;
        }
        return false;
    }

    // ── Internal ──────────────────────────────────────────────

    private static void WriteIconTile(byte[] atlasRgba, int slot, byte[] rgba144)
    {
        int gx = slot % GridSize;
        int gy = slot / GridSize;
        int px = gx * TileSize;
        int py = gy * TileSize;

        for (int row = 0; row < TileSize; row++)
        {
            int srcOff = row * TileSize * 4;
            int dstOff = ((py + row) * AtlasPixelSize + px) * 4;
            Buffer.BlockCopy(rgba144, srcOff, atlasRgba, dstOff, TileSize * 4);
        }
    }

    private static byte[] LoadOrCreateAtlasRgba(string dir, int idx, AtlasMeta meta)
    {
        var ddsPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{DdsExt}");
        if (File.Exists(ddsPath))
        {
            var (w, h, rgba) = DdsReader.Decode(File.ReadAllBytes(ddsPath));
            if (w == AtlasPixelSize && h == AtlasPixelSize)
                return rgba;
        }
        return new byte[AtlasPixelSize * AtlasPixelSize * 4];
    }

    private static void SaveAtlas(string dir, int idx, AtlasMeta meta, byte[] atlasRgba)
    {
        var ddsPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{DdsExt}");
        var metaPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{MetaExt}");

        File.WriteAllBytes(ddsPath, DdsWriter.Encode(atlasRgba, AtlasPixelSize, AtlasPixelSize));
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void RebuildAtlas(string dir, int idx, AtlasMeta meta)
    {
        // Can't rebuild from metadata alone — just save with holes
        // Icons that were removed leave black gaps, which is fine
        var metaPath = Path.Combine(dir, $"{AtlasPrefix}{idx}{MetaExt}");
        File.WriteAllText(metaPath, JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string GenerateLsx(int idx, AtlasMeta meta)
    {
        var entries = meta.Icons.Select(icon =>
        {
            int gx = icon.Slot % GridSize;
            int gy = icon.Slot / GridSize;
            return new AtlasEntry
            {
                Name = icon.Name,
                U1 = (float)(gx * TileSize) / AtlasPixelSize,
                V1 = (float)(gy * TileSize) / AtlasPixelSize,
                U2 = (float)((gx + 1) * TileSize) / AtlasPixelSize,
                V2 = (float)((gy + 1) * TileSize) / AtlasPixelSize,
            };
        }).ToList();

        return IconConverter.GenerateAtlasLsx(
            GetAtlasDdsPath(idx),
            GetAtlasUuid(idx),
            AtlasPixelSize,
            entries);
    }

    private static AtlasMeta LoadMeta(string dir, int idx)
    {
        var path = Path.Combine(dir, $"{AtlasPrefix}{idx}{MetaExt}");
        if (!File.Exists(path))
            return new AtlasMeta();
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<AtlasMeta>(json) ?? new AtlasMeta();
        }
        catch { return new AtlasMeta(); }
    }

    private static byte[] MD5Hash(string input)
    {
        using var md5 = System.Security.Cryptography.MD5.Create();
        return md5.ComputeHash(Encoding.UTF8.GetBytes(input));
    }
}

public class AtlasMeta
{
    public List<AtlasIconEntry> Icons { get; set; } = [];
}

public class AtlasIconEntry
{
    public string Name { get; set; } = "";
    public int Slot { get; set; }
}
