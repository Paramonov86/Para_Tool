using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Avalonia.Media.Imaging;
using ParaTool.Core.Icons;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;

namespace ParaTool.App.Services;

/// <summary>
/// The icon library the Constructor picks from, built in the background after a scan from the
/// game install and the scanned mods, with bitmaps for the UI. <see cref="Current"/> is null
/// until the first build finishes; <see cref="Changed"/> fires after every build.
/// </summary>
public sealed class IconLibraryService
{
    public static IconLibraryService? Current { get; private set; }
    public static event Action? Changed;

    public IconLibrary Library { get; }

    /// <summary>Icon name → the stats entries that use it (name, type).</summary>
    public IReadOnlyDictionary<string, List<(string stat, string type)>> UsedBy { get; }

    private readonly ConcurrentDictionary<string, WriteableBitmap?> _thumbs = new(StringComparer.OrdinalIgnoreCase);

    private IconLibraryService(IconLibrary library, Dictionary<string, List<(string, string)>> usedBy)
    {
        Library = library;
        UsedBy = usedBy;
    }

    private static int _generation;
    private static IReadOnlyList<(string pakPath, string? modName)> _lastPaks = [];
    private static StatsResolver? _lastResolver;

    /// <summary>Builds again from the last scan, e.g. after the user pointed ParaTool at the game folder.</summary>
    public static Task RebuildAsync() => BuildAsync(_lastPaks, _lastResolver);

    /// <summary>
    /// Builds the library off the UI thread and publishes it. The game folder is the one the user
    /// set, or found on its own. A later call supersedes an earlier one still running.
    /// </summary>
    public static Task BuildAsync(IReadOnlyList<(string pakPath, string? modName)> modPaks, StatsResolver? resolver)
    {
        _lastPaks = modPaks;
        _lastResolver = resolver;
        var generation = Interlocked.Increment(ref _generation);
        var userPath = UiSettingsService.Load().GameDataPath;
        return Task.Run(() =>
        {
            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                var (usedBy, statsIcons) = IndexStatsIcons(resolver);
                var sources = modPaks.Select(p =>
                {
                    var file = Path.GetFileNameWithoutExtension(p.pakPath);
                    var isAmp = file.StartsWith("REL_Full_Ancient", StringComparison.OrdinalIgnoreCase);
                    return new IconPakSource(p.pakPath, isAmp ? "AMP" : p.modName ?? ModNameFromFile(file), IsAmp: isAmp);
                });
                var library = IconLibrary.Build(GameDataLocator.Find(userPath), sources, statsIcons);
                AppLogger.Info($"Icon library: {library.All.Count} icons in {sw.ElapsedMilliseconds} ms (game data: {library.GameDataDir ?? "not found"})");
                if (generation != _generation) return;
                Current = new IconLibraryService(library, usedBy);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Changed?.Invoke());
            }
            catch (Exception ex)
            {
                AppLogger.Error("Icon library build failed", ex);
            }
        });
    }

    /// <summary>"mysticclass_93c6116d-631a-cdc4-hhcx" → "mysticclass".</summary>
    internal static string ModNameFromFile(string file) =>
        Regex.Replace(file, @"_[0-9a-fA-F]{6,8}-[0-9a-zA-Z-]*$", "") is { Length: > 0 } name ? name : file;

    private static (Dictionary<string, List<(string, string)>>, HashSet<string>) IndexStatsIcons(StatsResolver? resolver)
    {
        var usedBy = new Dictionary<string, List<(string, string)>>(StringComparer.OrdinalIgnoreCase);
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (resolver == null) return (usedBy, names);
        foreach (var entry in resolver.AllEntries.Values)
        {
            if (entry.Type is not ("SpellData" or "StatusData" or "PassiveData")) continue;
            if (!entry.Data.TryGetValue("Icon", out var icon) || string.IsNullOrWhiteSpace(icon)) continue;
            icon = icon.Trim();
            names.Add(icon);
            if (!usedBy.TryGetValue(icon, out var list)) usedBy[icon] = list = [];
            list.Add((entry.Name, entry.Type ?? ""));
        }
        return (usedBy, names);
    }

    public IconEntry? Find(string? name) => Library.Find(name);

    /// <summary>Grid-size picture (at most 64 px), cached. Decodes the icon's atlas the first time.</summary>
    public WriteableBitmap? Thumb(string? name)
    {
        if (Find(name) is not { } entry) return null;
        return _thumbs.GetOrAdd(entry.Name, _ =>
        {
            var rgba = Library.GetThumbnail(entry, out var w, out var h);
            return rgba == null ? null : IconBitmaps.FromRgba(rgba, w, h);
        });
    }

    public bool HasThumbCached(string name) => _thumbs.ContainsKey(name);

    /// <summary>The atlas tile at its own size (what the hotbar and status bar draw).</summary>
    public WriteableBitmap? Tile(IconEntry entry)
    {
        var rgba = Library.GetTile(entry, out var w, out var h);
        return rgba == null ? null : IconBitmaps.FromRgba(rgba, w, h);
    }

    /// <summary>The tooltip picture (380×380), else the controller one, else the tile.</summary>
    public WriteableBitmap? Large(IconEntry entry)
    {
        var rgba = Library.GetLarge(entry, out var w, out var h);
        return rgba != null ? IconBitmaps.FromRgba(rgba, w, h) : Tile(entry);
    }
}

public static class IconBitmaps
{
    public static WriteableBitmap? FromRgba(byte[] rgba, int w, int h)
    {
        if (w <= 0 || h <= 0 || rgba.Length < w * h * 4) return null;
        try
        {
            var bitmap = new WriteableBitmap(new Avalonia.PixelSize(w, h), new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormats.Rgba8888, Avalonia.Platform.AlphaFormat.Unpremul);
            using var fb = bitmap.Lock();
            for (int y = 0; y < h; y++)
                System.Runtime.InteropServices.Marshal.Copy(rgba, y * w * 4, fb.Address + y * fb.RowBytes, w * 4);
            return bitmap;
        }
        catch { return null; }
    }
}
