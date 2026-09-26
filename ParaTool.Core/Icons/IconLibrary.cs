using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using ParaTool.Core.Models;
using ParaTool.Core.Services;
using ParaTool.Core.Textures;

namespace ParaTool.Core.Icons;

/// <summary>What an icon can be given to in the game.</summary>
public enum IconKind
{
    /// <summary>
    /// Has a tooltip picture (380×380, <c>GUI/Assets/Tooltips/Icons</c>) next to its hotbar tile —
    /// the two sizes a spell shows. Fits spells, passives and statuses.
    /// </summary>
    Spell,

    /// <summary>Only a small tile in a skills atlas (64×64): enough for a status, not for a spell tooltip.</summary>
    Status,

    /// <summary>An inventory icon (item atlas tile, <c>Tooltips/ItemIcons</c> picture).</summary>
    Item,
}

/// <summary>One pak ParaTool reads icons from, with the name shown as the icon's origin.</summary>
public sealed record IconPakSource(string PakPath, string Source, bool IsVanilla = false, bool IsAmp = false);

/// <summary>A file inside a pak.</summary>
public sealed record PakFile(string PakPath, FileEntry Entry)
{
    public byte[] Read()
    {
        using var fs = File.OpenRead(PakSource.Resolve(PakPath));
        return PakReader.ExtractFileData(fs, Entry);
    }
}

/// <summary>An icon atlas: a texture and the tiles an <c>IconUVList</c> cuts from it.</summary>
public sealed class IconAtlas
{
    public required string Name { get; init; }
    public required string Source { get; init; }
    public required Func<byte[]?> LoadTexture { get; init; }
    public int IconSize { get; init; }
    internal readonly List<IconEntry> Entries = [];
    internal readonly object Gate = new();
    internal bool ThumbsDone;
}

public sealed class IconEntry
{
    public required string Name { get; init; }
    public IconKind Kind { get; internal set; }

    /// <summary>"Vanilla", "AMP" or the mod's name — the pak the icon comes from.</summary>
    public required string Source { get; init; }
    public bool IsVanilla { get; init; }
    public bool IsAmp { get; init; }

    internal IconAtlas? Atlas;
    internal float U1, U2, V1, V2;
    internal PakFile? Large;
    internal PakFile? Controller;

    /// <summary>A tile in an atlas — what the hotbar, status bar and inventory draw.</summary>
    public bool HasTile => Atlas != null;

    /// <summary>A tooltip picture of its own (380×380).</summary>
    public bool HasLarge => Large != null;
    public bool HasController => Controller != null;
    public string? AtlasName => Atlas?.Name;

    /// <summary>Small RGBA copy for grids (at most <see cref="IconLibrary.ThumbSize"/> px), filled on demand.</summary>
    internal byte[]? Thumb;
    internal int ThumbW, ThumbH;
}

/// <summary>
/// Every icon the game and the installed mods ship, read from their paks: the atlases listed in
/// <c>Public/*/GUI/*.lsx</c> (hotbar, status and inventory tiles) and the tooltip pictures in
/// <c>GUI/Assets/Tooltips</c>. Nothing is decoded while building; tiles and pictures are read when asked for.
/// </summary>
public sealed class IconLibrary
{
    public const int ThumbSize = 64;

    private readonly Dictionary<string, IconEntry> _byName = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<IconEntry> _all = [];

    public IReadOnlyList<IconEntry> All => _all;
    public IconEntry? Find(string? name) => string.IsNullOrEmpty(name) ? null : _byName.GetValueOrDefault(name.Trim());

    /// <summary>Where the vanilla icons came from: the game install, or only the item atlases ParaTool embeds.</summary>
    public string? GameDataDir { get; private set; }

    /// <summary>Game paks that hold icons, in load order (a later pak overrides an earlier one).</summary>
    public static IEnumerable<string> GamePaks(string dataDir)
    {
        foreach (var name in new[] { "Shared.pak", "Game.pak", "GustavX.pak", "Icons.pak", "Gustav_Textures.pak" })
        {
            var p = Path.Combine(dataDir, name);
            if (File.Exists(p)) yield return p;
        }
        foreach (var patch in Directory.GetFiles(dataDir, "Patch*.pak").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            yield return patch;
    }

    /// <summary>
    /// Builds the library. <paramref name="modPaks"/> go after the game, AMP first. An atlas tile
    /// without a tooltip picture is kept when its atlas is a skills or item atlas, or when a stats
    /// entry uses it (<paramref name="statsIcons"/>) — this leaves out character creation atlases
    /// (hair, faces, dyes) that mods ship alongside.
    /// </summary>
    public static IconLibrary Build(string? gameDataDir, IEnumerable<IconPakSource> modPaks,
        ISet<string>? statsIcons = null, bool embeddedItemAtlases = true)
    {
        var lib = new IconLibrary();
        var groups = new List<Group>();

        if (gameDataDir != null && GameDataLocator.IsDataDir(gameDataDir))
        {
            lib.GameDataDir = gameDataDir;
            groups.Add(ReadGroup(GamePaks(gameDataDir).Select(p => new IconPakSource(p, "Vanilla", IsVanilla: true)).ToList()));
        }
        if (embeddedItemAtlases && !groups.Any(g => g.Atlases.Any(a => a.atlas.Name.StartsWith("Icons_Items", StringComparison.OrdinalIgnoreCase))))
            groups.Add(EmbeddedItemAtlases());

        foreach (var src in modPaks.OrderByDescending(m => m.IsAmp))
            groups.Add(ReadGroup([src]));

        lib.Merge(groups, statsIcons);
        return lib;
    }

    // ── Reading ─────────────────────────────────────────────

    private sealed class Group
    {
        public required IconPakSource Source;
        public readonly List<(IconAtlas atlas, List<(string name, float u1, float u2, float v1, float v2)> tiles)> Atlases = [];
        public readonly Dictionary<string, PakFile> SkillLarge = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, PakFile> SkillController = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, PakFile> ItemLarge = new(StringComparer.OrdinalIgnoreCase);
        public readonly Dictionary<string, PakFile> ItemController = new(StringComparer.OrdinalIgnoreCase);
    }

    private static readonly Regex AtlasLsxPath = new(@"^Public/([^/]+)/GUI/[^/]+\.lsx$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static Group ReadGroup(IReadOnlyList<IconPakSource> paks)
    {
        var group = new Group { Source = paks[0] };
        // Every file of the group by path: a vanilla atlas lists its texture in another pak (Icons.pak).
        var files = new Dictionary<string, PakFile>(StringComparer.OrdinalIgnoreCase);
        var lsxFiles = new List<(string module, PakFile file)>();

        foreach (var src in paks)
        {
            IReadOnlyList<FileEntry> entries;
            try
            {
                using var fs = File.OpenRead(PakSource.Resolve(src.PakPath));
                entries = PakReader.ReadFileList(fs, PakReader.ReadHeader(fs));
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"IconLibrary: cannot read {src.PakPath}: {ex.Message}");
                continue;
            }

            foreach (var e in entries)
            {
                if (e.ArchivePart != 0) continue;
                var path = e.Path.Replace('\\', '/');
                var file = new PakFile(src.PakPath, e);
                if (path.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                {
                    files[path] = file;
                    if (path.Contains("/AssetsLowRes/", StringComparison.OrdinalIgnoreCase)) continue;
                    var name = Path.GetFileNameWithoutExtension(path);
                    if (path.Contains("/GUI/Assets/Tooltips/Icons/", StringComparison.OrdinalIgnoreCase)) group.SkillLarge[name] = file;
                    else if (path.Contains("/GUI/Assets/Tooltips/ItemIcons/", StringComparison.OrdinalIgnoreCase)) group.ItemLarge[name] = file;
                    else if (path.Contains("/GUI/Assets/ControllerUIIcons/skills_png/", StringComparison.OrdinalIgnoreCase)) group.SkillController[name] = file;
                    else if (path.Contains("/GUI/Assets/ControllerUIIcons/items_png/", StringComparison.OrdinalIgnoreCase)) group.ItemController[name] = file;
                }
                else if (AtlasLsxPath.Match(path) is { Success: true } m)
                {
                    lsxFiles.Add((m.Groups[1].Value, file));
                }
            }
        }

        foreach (var (module, lsx) in lsxFiles)
        {
            try
            {
                var text = System.Text.Encoding.UTF8.GetString(lsx.Read());
                if (!text.Contains("IconUVList", StringComparison.Ordinal)) continue;
                var parsed = ParseAtlasLsx(text);
                if (parsed == null || parsed.Value.tiles.Count == 0) continue;
                var (texPath, iconSize, tiles) = parsed.Value;
                var texture = files.GetValueOrDefault($"Public/{module}/{texPath}")
                              ?? files.FirstOrDefault(kv => kv.Key.EndsWith("/" + texPath, StringComparison.OrdinalIgnoreCase)).Value;
                if (texture == null)
                {
                    AppLogger.Warn($"IconLibrary: atlas {lsx.Entry.Path} names a missing texture {texPath}");
                    continue;
                }
                var atlas = new IconAtlas
                {
                    Name = Path.GetFileNameWithoutExtension(lsx.Entry.Path),
                    Source = group.Source.Source,
                    IconSize = iconSize,
                    LoadTexture = texture.Read,
                };
                group.Atlases.Add((atlas, tiles));
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"IconLibrary: skip atlas {lsx.Entry.Path}: {ex.Message}");
            }
        }
        return group;
    }

    /// <summary>The item atlases ParaTool embeds, for when the game folder is not found.</summary>
    private static Group EmbeddedItemAtlases()
    {
        var group = new Group { Source = new IconPakSource("", "Vanilla", IsVanilla: true) };
        var assembly = typeof(IconLibrary).Assembly;
        const string prefix = "ParaTool.Core.Resources.VanillaIcons.";
        foreach (var res in assembly.GetManifestResourceNames().Where(n => n.StartsWith(prefix) && n.EndsWith(".lsx")).OrderBy(n => n))
        {
            using var stream = assembly.GetManifestResourceStream(res);
            if (stream == null) continue;
            var parsed = ParseAtlasLsx(new StreamReader(stream).ReadToEnd());
            if (parsed == null) continue;
            var ddsRes = res[..^".lsx".Length] + ".dds";
            var atlas = new IconAtlas
            {
                Name = res[prefix.Length..^".lsx".Length],
                Source = "Vanilla",
                IconSize = parsed.Value.iconSize,
                LoadTexture = () =>
                {
                    using var s = assembly.GetManifestResourceStream(ddsRes);
                    if (s == null) return null;
                    using var ms = new MemoryStream();
                    s.CopyTo(ms);
                    return ms.ToArray();
                },
            };
            group.Atlases.Add((atlas, parsed.Value.tiles));
        }
        return group;
    }

    /// <summary>Texture path, tile size and tiles of an atlas <c>.lsx</c>; null when it is not one.</summary>
    internal static (string texturePath, int iconSize, List<(string name, float u1, float u2, float v1, float v2)> tiles)? ParseAtlasLsx(string text)
    {
        var doc = XDocument.Parse(text);
        string? Attr(XElement node, string id) =>
            node.Elements("attribute").FirstOrDefault(a => (string?)a.Attribute("id") == id)?.Attribute("value")?.Value;
        static float F(string? v) => float.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var f) ? f : 0;

        var tiles = new List<(string, float, float, float, float)>();
        foreach (var node in doc.Descendants("node").Where(n => (string?)n.Attribute("id") == "IconUV"))
        {
            var key = Attr(node, "MapKey");
            if (string.IsNullOrEmpty(key)) continue;
            tiles.Add((key, F(Attr(node, "U1")), F(Attr(node, "U2")), F(Attr(node, "V1")), F(Attr(node, "V2"))));
        }

        var pathNode = doc.Descendants("node").FirstOrDefault(n => (string?)n.Attribute("id") == "TextureAtlasPath");
        var texPath = pathNode != null ? Attr(pathNode, "Path") : null;
        if (string.IsNullOrEmpty(texPath)) return null;
        var sizeNode = doc.Descendants("node").FirstOrDefault(n => (string?)n.Attribute("id") == "TextureAtlasIconSize");
        int.TryParse(sizeNode != null ? Attr(sizeNode, "Width") : null, out var iconSize);
        return (texPath.Replace('\\', '/').TrimStart('/'), iconSize, tiles);
    }

    // ── Merging and classification ──────────────────────────

    private void Merge(List<Group> groups, ISet<string>? statsIcons)
    {
        PakFile? Lookup(Func<Group, Dictionary<string, PakFile>> pick, string name)
        {
            foreach (var g in groups)
                if (pick(g).TryGetValue(name, out var f)) return f;
            return null;
        }

        foreach (var g in groups)
        {
            foreach (var (atlas, tiles) in g.Atlases)
            {
                // An atlas is a skills or an item atlas by what its tiles have pictures for.
                int skill = tiles.Count(t => Lookup(x => x.SkillLarge, t.name) != null);
                int item = tiles.Count(t => Lookup(x => x.ItemLarge, t.name) != null);
                IconKind? atlasKind = skill > 0 && skill >= item ? IconKind.Status
                    : item > 0 ? IconKind.Item
                    : atlas.Name.Contains("Items", StringComparison.OrdinalIgnoreCase) ? IconKind.Item
                    : atlas.Name.Contains("Skills", StringComparison.OrdinalIgnoreCase) ? IconKind.Status
                    : null;

                foreach (var (name, u1, u2, v1, v2) in tiles)
                {
                    if (_byName.ContainsKey(name)) continue;
                    var large = Lookup(x => x.SkillLarge, name);
                    var itemLarge = Lookup(x => x.ItemLarge, name);
                    IconKind kind;
                    if (large != null) kind = IconKind.Spell;
                    else if (itemLarge != null) kind = IconKind.Item;
                    else if (atlasKind is { } k) kind = k;
                    else if (statsIcons?.Contains(name) == true) kind = IconKind.Status;
                    else continue;

                    var entry = new IconEntry
                    {
                        Name = name,
                        Kind = kind,
                        Source = g.Source.Source,
                        IsVanilla = g.Source.IsVanilla,
                        IsAmp = g.Source.IsAmp,
                        Atlas = atlas,
                        U1 = u1, U2 = u2, V1 = v1, V2 = v2,
                        Large = kind == IconKind.Item ? itemLarge : large,
                        Controller = kind == IconKind.Item ? Lookup(x => x.ItemController, name) : Lookup(x => x.SkillController, name),
                    };
                    atlas.Entries.Add(entry);
                    Add(entry);
                }
            }
        }

        // Item pictures without an atlas tile: the old item browser listed them, and an item's
        // tooltip reads the picture by name.
        foreach (var g in groups)
            foreach (var (name, file) in g.ItemLarge)
                if (!_byName.ContainsKey(name))
                    Add(new IconEntry
                    {
                        Name = name, Kind = IconKind.Item, Source = g.Source.Source,
                        IsVanilla = g.Source.IsVanilla, IsAmp = g.Source.IsAmp,
                        Large = file, Controller = Lookup(x => x.ItemController, name),
                    });

        _all.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
    }

    private void Add(IconEntry e)
    {
        _byName[e.Name] = e;
        _all.Add(e);
    }

    // ── Pixels ──────────────────────────────────────────────

    private readonly ConcurrentDictionary<IconAtlas, (int w, int h, byte[] rgba)> _decoded = new();
    private readonly Queue<IconAtlas> _decodedOrder = new();
    private const int MaxDecodedAtlases = 2;

    private (int w, int h, byte[] rgba)? Decode(IconAtlas atlas)
    {
        if (_decoded.TryGetValue(atlas, out var hit)) return hit;
        try
        {
            var dds = atlas.LoadTexture();
            if (dds == null) return null;
            var decoded = DdsReader.Decode(dds);
            lock (_decodedOrder)
            {
                _decoded[atlas] = decoded;
                _decodedOrder.Enqueue(atlas);
                while (_decodedOrder.Count > MaxDecodedAtlases && _decodedOrder.TryDequeue(out var old))
                    _decoded.TryRemove(old, out _);
            }
            return decoded;
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"IconLibrary: cannot decode atlas {atlas.Name}: {ex.Message}");
            return null;
        }
    }

    private static byte[]? Crop((int w, int h, byte[] rgba) atlas, IconEntry e, out int tw, out int th)
    {
        var (aw, ah, rgba) = atlas;
        int x1 = (int)Math.Round(e.U1 * aw), y1 = (int)Math.Round(e.V1 * ah);
        int x2 = (int)Math.Round(e.U2 * aw), y2 = (int)Math.Round(e.V2 * ah);
        tw = x2 - x1; th = y2 - y1;
        if (tw <= 0 || th <= 0 || x1 < 0 || y1 < 0 || x2 > aw || y2 > ah) return null;
        var tile = new byte[tw * th * 4];
        for (int row = 0; row < th; row++)
            Buffer.BlockCopy(rgba, ((y1 + row) * aw + x1) * 4, tile, row * tw * 4, tw * 4);
        return tile;
    }

    /// <summary>The icon's atlas tile at its own size (what the hotbar or status bar draws).</summary>
    public byte[]? GetTile(IconEntry e, out int w, out int h)
    {
        w = h = 0;
        if (e.Atlas == null) return null;
        lock (e.Atlas.Gate)
        {
            var atlas = Decode(e.Atlas);
            return atlas == null ? null : Crop(atlas.Value, e, out w, out h);
        }
    }

    /// <summary>
    /// A small copy for grids. The first tile asked for decodes its atlas once and keeps a copy of
    /// every tile in it, so scrolling an atlas does not decode it again.
    /// </summary>
    public byte[]? GetThumbnail(IconEntry e, out int w, out int h)
    {
        if (e.Thumb == null)
        {
            if (e.Atlas is { } atlas)
            {
                lock (atlas.Gate)
                {
                    if (!atlas.ThumbsDone)
                    {
                        var decoded = Decode(atlas);
                        if (decoded != null)
                            foreach (var t in atlas.Entries)
                            {
                                var tile = Crop(decoded.Value, t, out var tw, out var th);
                                if (tile != null) (t.Thumb, t.ThumbW, t.ThumbH) = Downscale(tile, tw, th, ThumbSize);
                            }
                        atlas.ThumbsDone = true;
                    }
                }
            }
            else if (GetLarge(e, out var lw, out var lh) is { } large)
            {
                (e.Thumb, e.ThumbW, e.ThumbH) = Downscale(large, lw, lh, ThumbSize);
            }
        }
        w = e.ThumbW; h = e.ThumbH;
        return e.Thumb;
    }

    /// <summary>The tooltip picture (380×380), else the controller one (144×144); null when it has neither.</summary>
    public byte[]? GetLarge(IconEntry e, out int w, out int h)
    {
        w = h = 0;
        foreach (var file in new[] { e.Large, e.Controller })
        {
            if (file == null) continue;
            try
            {
                var decoded = DdsReader.Decode(file.Read());
                (w, h) = (decoded.width, decoded.height);
                return decoded.rgba;
            }
            catch (Exception ex)
            {
                AppLogger.Warn($"IconLibrary: cannot read {file.Entry.Path}: {ex.Message}");
            }
        }
        return null;
    }

    /// <summary>Box-filtered copy no larger than <paramref name="max"/> on either side.</summary>
    internal static (byte[] rgba, int w, int h) Downscale(byte[] rgba, int w, int h, int max)
    {
        if (w <= max && h <= max) return (rgba, w, h);
        double scale = Math.Min((double)max / w, (double)max / h);
        int nw = Math.Max(1, (int)Math.Round(w * scale)), nh = Math.Max(1, (int)Math.Round(h * scale));
        var dst = new byte[nw * nh * 4];
        for (int y = 0; y < nh; y++)
        {
            int sy0 = y * h / nh, sy1 = Math.Max(sy0 + 1, (y + 1) * h / nh);
            for (int x = 0; x < nw; x++)
            {
                int sx0 = x * w / nw, sx1 = Math.Max(sx0 + 1, (x + 1) * w / nw);
                long r = 0, g = 0, b = 0, a = 0; int n = 0;
                for (int sy = sy0; sy < sy1; sy++)
                    for (int sx = sx0; sx < sx1; sx++)
                    {
                        int i = (sy * w + sx) * 4;
                        long alpha = rgba[i + 3];
                        r += rgba[i] * alpha; g += rgba[i + 1] * alpha; b += rgba[i + 2] * alpha; a += alpha; n++;
                    }
                int o = (y * nw + x) * 4;
                if (a > 0) { dst[o] = (byte)(r / a); dst[o + 1] = (byte)(g / a); dst[o + 2] = (byte)(b / a); }
                dst[o + 3] = (byte)(a / n);
            }
        }
        return (dst, nw, nh);
    }
}
