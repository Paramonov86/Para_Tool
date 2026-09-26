using ParaTool.Core.Icons;
using ParaTool.Core.LSLib;
using ParaTool.Core.Models;
using ParaTool.Core.Services;

namespace ParaTool.Core.Effects;

/// <summary>
/// A visual effect a stats field can name (StatusEffect, ApplyEffect, …). The field holds the UUID
/// of a MultiEffectInfo, which plays one or more effect resources — each an <c>.lsfx</c> file.
/// </summary>
public sealed class EffectEntry
{
    public required string Uuid { get; init; }

    /// <summary>The MultiEffectInfo's own name (<c>BLESS_StatusEffect</c>).</summary>
    public required string Name { get; init; }

    /// <summary>"Vanilla", "AMP" or the mod's name.</summary>
    public required string Source { get; init; }

    /// <summary>Its effect resources, as MultiEffectInfo lists them.</summary>
    internal readonly List<(string resource, bool conditional)> Effects = [];

    /// <summary>How many effect resources it lists (some list none and play nothing).</summary>
    public int EffectCount => Effects.Count;

    /// <summary>The <c>.lsfx</c> files it plays, the main one first; filled once the effect banks are read.</summary>
    public IReadOnlyList<string> Files { get; internal set; } = [];

    /// <summary>What to show: the main <c>.lsfx</c> file, else the MultiEffectInfo's name.</summary>
    public string Display => Files.Count > 0 ? Files[0] : Name;
}

/// <summary>
/// Every visual effect the game and the installed mods ship: MultiEffectInfos
/// (<c>Public/*/MultiEffectInfos/&lt;uuid&gt;.lsf</c>) and the effect banks naming their <c>.lsfx</c>
/// files (<c>Resource</c> nodes of <c>EffectBank</c> regions under <c>Public/*/Content</c>).
/// </summary>
public sealed class EffectLibrary
{
    private readonly Dictionary<string, EffectEntry> _byUuid = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<EffectEntry> All => _byUuid.Values;
    public EffectEntry? Find(string? uuid) => string.IsNullOrWhiteSpace(uuid) ? null : _byUuid.GetValueOrDefault(uuid.Trim());

    /// <summary>The effect's <c>.lsfx</c> name, or the value itself when the library doesn't know it.</summary>
    public string Display(string uuid) => Find(uuid)?.Display ?? uuid;

    /// <summary>Game paks with effects, in load order (a later one overrides an earlier one).</summary>
    public static IEnumerable<string> GamePaks(string dataDir)
    {
        foreach (var name in new[] { "Shared.pak", "Engine.pak", "Gustav.pak", "GustavX.pak", "Game.pak" })
        {
            var p = Path.Combine(dataDir, name);
            if (File.Exists(p)) yield return p;
        }
        foreach (var patch in Directory.GetFiles(dataDir, "Patch*.pak").OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            yield return patch;
    }

    /// <summary>Builds the library from the game install (if found) and the mods, AMP first among them.</summary>
    public static EffectLibrary Build(string? gameDataDir, IEnumerable<IconPakSource> modPaks)
    {
        var sources = new List<IconPakSource>();
        if (gameDataDir != null && GameDataLocator.IsDataDir(gameDataDir))
            sources.AddRange(GamePaks(gameDataDir).Select(p => new IconPakSource(p, "Vanilla", IsVanilla: true)));
        sources.AddRange(modPaks.OrderByDescending(m => m.IsAmp));

        var lib = new EffectLibrary();
        var files = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase); // resource id → .lsfx name
        foreach (var src in sources)
            lib.ReadPak(src, files);

        foreach (var e in lib._byUuid.Values)
        {
            var named = e.Effects.Select(x => (name: files.GetValueOrDefault(x.resource), x.conditional))
                .Where(x => x.name != null).ToList();
            // The main file: one that always plays and is not a sound-only effect (VFX_Sound_*).
            var main = named.FindIndex(x => !x.conditional && !x.name!.StartsWith("VFX_Sound_", StringComparison.OrdinalIgnoreCase));
            if (main > 0) { var m = named[main]; named.RemoveAt(main); named.Insert(0, m); }
            e.Files = named.Select(x => x.name!).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        }
        return lib;
    }

    private void ReadPak(IconPakSource src, Dictionary<string, string> files)
    {
        IReadOnlyList<FileEntry> entries;
        try
        {
            using var fs = File.OpenRead(PakSource.Resolve(src.PakPath));
            entries = PakReader.ReadFileList(fs, PakReader.ReadHeader(fs));
        }
        catch (Exception ex)
        {
            AppLogger.Warn($"EffectLibrary: cannot read {src.PakPath}: {ex.Message}");
            return;
        }

        var wanted = new List<(FileEntry entry, bool isInfo)>();
        foreach (var e in entries)
        {
            if (e.ArchivePart != 0) continue;
            var path = e.Path.Replace('\\', '/');
            if (!path.StartsWith("Public/", StringComparison.OrdinalIgnoreCase)) continue;
            var lsf = path.EndsWith(".lsf", StringComparison.OrdinalIgnoreCase);
            if (!lsf && !path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)) continue;
            if (path.Contains("/MultiEffectInfos/", StringComparison.OrdinalIgnoreCase))
                wanted.Add((e, true));
            // The game keeps its effect banks under Content/Assets/Effects; mods put them anywhere in Content.
            else if (path.Contains("/Content/", StringComparison.OrdinalIgnoreCase)
                     && (!src.IsVanilla || path.Contains("/Effects/", StringComparison.OrdinalIgnoreCase)))
                wanted.Add((e, false));
        }
        if (wanted.Count == 0) return;

        var settings = new NodeSerializationSettings();
        using var pak = File.OpenRead(PakSource.Resolve(src.PakPath));
        foreach (var (entry, isInfo) in wanted)
        {
            Resource resource;
            try
            {
                var data = PakReader.ExtractFileData(pak, entry);
                using var ms = new MemoryStream(data);
                resource = entry.Path.EndsWith(".lsx", StringComparison.OrdinalIgnoreCase)
                    ? new LSXReader(ms).Read()
                    : new LSFReader(ms).Read();
            }
            catch { continue; }

            if (isInfo)
            {
                if (!resource.Regions.TryGetValue("MultiEffectInfos", out var info)) continue;
                var uuid = Attr(info, "UUID", settings);
                if (string.IsNullOrEmpty(uuid)) continue;
                var effect = new EffectEntry { Uuid = uuid, Name = Attr(info, "Name", settings) ?? uuid, Source = src.Source };
                foreach (var ei in info.Children.GetValueOrDefault("EffectInfo") ?? [])
                    if (Attr(ei, "EffectResourceGuid", settings) is { Length: > 0 } res)
                        effect.Effects.Add((res, ei.Children.ContainsKey("TargetTag") && ei.Children["TargetTag"].Count > 0));
                _byUuid[uuid] = effect;
            }
            else if (resource.Regions.TryGetValue("EffectBank", out var bank))
            {
                foreach (var res in Descendants(bank).Where(n => n.Name == "Resource"))
                    if (Attr(res, "ID", settings) is { Length: > 0 } id && Attr(res, "Name", settings) is { Length: > 0 } name)
                        files[id] = name;
            }
        }
    }

    private static IEnumerable<Node> Descendants(Node node)
    {
        foreach (var list in node.Children.Values)
            foreach (var child in list)
            {
                yield return child;
                foreach (var d in Descendants(child)) yield return d;
            }
    }

    private static string? Attr(Node node, string name, NodeSerializationSettings settings) =>
        node.Attributes.TryGetValue(name, out var a) ? a.AsString(settings) : null;
}
