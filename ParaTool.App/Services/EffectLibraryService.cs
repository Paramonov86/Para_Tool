using ParaTool.Core.Effects;
using ParaTool.Core.Icons;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;

namespace ParaTool.App.Services;

/// <summary>
/// What a status card's appearance settings pick from, built in the background after a scan: the
/// visual effects of the game and the mods (<see cref="EffectLibrary"/>), and the sounds and
/// animations the scanned stats use. <see cref="Current"/> is null until the first build finishes;
/// <see cref="Changed"/> fires after every build.
/// </summary>
public sealed class EffectLibraryService
{
    public static EffectLibraryService? Current { get; private set; }
    public static event Action? Changed;

    public EffectLibrary Library { get; }

    /// <summary>Stats field → the values the game's and the mods' statuses and spells give it (sounds, animations).</summary>
    public IReadOnlyDictionary<string, string[]> UsedValues { get; }

    /// <summary>Effects any status uses, for the top of the list.</summary>
    public IReadOnlySet<string> StatusEffects { get; }

    private EffectLibraryService(EffectLibrary library, Dictionary<string, string[]> usedValues, HashSet<string> statusEffects)
    {
        Library = library;
        UsedValues = usedValues;
        StatusEffects = statusEffects;
    }

    /// <summary>Fields whose values come from what the scanned stats use.</summary>
    public static readonly string[] ValueFields =
        ["SoundStart", "SoundLoop", "SoundStop", "AnimationStart", "AnimationLoop", "AnimationEnd"];

    private static readonly string[] EffectFields =
        ["StatusEffect", "ApplyEffect", "StatusEffectOnTurn", "EndEffect", "StatusEffectOverride"];

    private static int _generation;
    private static IReadOnlyList<(string pakPath, string? modName)> _lastPaks = [];
    private static StatsResolver? _lastResolver;

    public static Task RebuildAsync() => BuildAsync(_lastPaks, _lastResolver);

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
                var sources = modPaks.Select(p =>
                {
                    var file = Path.GetFileNameWithoutExtension(p.pakPath);
                    var isAmp = file.StartsWith("REL_Full_Ancient", StringComparison.OrdinalIgnoreCase);
                    return new IconPakSource(p.pakPath, isAmp ? "AMP" : p.modName ?? IconLibraryService.ModNameFromFile(file), IsAmp: isAmp);
                });
                var library = EffectLibrary.Build(GameDataLocator.Find(userPath), sources);
                var (used, statusEffects) = IndexStats(resolver);
                AppLogger.Info($"Effect library: {library.All.Count} effects in {sw.ElapsedMilliseconds} ms");
                if (generation != _generation) return;
                Current = new EffectLibraryService(library, used, statusEffects);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => Changed?.Invoke());
            }
            catch (Exception ex)
            {
                AppLogger.Error("Effect library build failed", ex);
            }
        });
    }

    private static (Dictionary<string, string[]>, HashSet<string>) IndexStats(StatsResolver? resolver)
    {
        var sets = ValueFields.ToDictionary(f => f, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
        var statusEffects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (resolver != null)
            foreach (var entry in resolver.Definitions)
            {
                if (entry.Type is not ("StatusData" or "SpellData")) continue;
                foreach (var f in ValueFields)
                    if (entry.Data.TryGetValue(f, out var v) && !string.IsNullOrWhiteSpace(v))
                        sets[f].Add(v.Trim());
                if (entry.Type == "StatusData")
                    foreach (var f in EffectFields)
                        if (entry.Data.TryGetValue(f, out var v) && !string.IsNullOrWhiteSpace(v))
                            statusEffects.Add(v.Trim());
            }
        // Start, loop and stop sounds share one list (a loop's stop event is a sound all the same);
        // so do the three animations.
        string[] Merge(params string[] fields) =>
            fields.SelectMany(f => sets[f]).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(v => AnimationName(v), StringComparer.OrdinalIgnoreCase).ToArray();
        var sounds = Merge("SoundStart", "SoundLoop", "SoundStop");
        var anims = Merge("AnimationStart", "AnimationLoop", "AnimationEnd");
        return (new Dictionary<string, string[]>
        {
            ["SoundStart"] = sounds, ["SoundLoop"] = sounds, ["SoundStop"] = sounds,
            ["AnimationStart"] = anims, ["AnimationLoop"] = anims, ["AnimationEnd"] = anims,
        }, statusEffects);
    }

    /// <summary>"06cff5ab-…(STAT_Dazed_Combat_01_Loop)" → "STAT_Dazed_Combat_01_Loop"; anything else as it is.</summary>
    public static string AnimationName(string value)
    {
        var open = value.IndexOf('(');
        return open > 0 && value.EndsWith(')') ? value[(open + 1)..^1] : value;
    }
}
