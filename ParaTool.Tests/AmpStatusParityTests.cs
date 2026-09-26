using ParaTool.Core.Artifacts;
using ParaTool.Core.Parsing;
using ParaTool.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

/// <summary>
/// A status card copied from a game or AMP status and left untouched must resolve in game to the
/// very same status: every field, inherited or written, equal to the original's — only mentions
/// of the status itself point at the copy. Runs over AMP's decompiled stats when they are on disk.
/// </summary>
public class AmpStatusParityTests
{
    private readonly ITestOutputHelper _output;
    public AmpStatusParityTests(ITestOutputHelper output) => _output = output;

    private const string AmpStatsDir =
        @"E:\SteamLibrary\steamapps\common\Baldurs Gate 3\Data\Public\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025\Stats\Generated\Data";

    private static readonly Lazy<StatsResolver> Vanilla = new(() =>
    {
        var db = new VanillaDatabase();
        db.Load();
        return db.Resolver;
    });

    /// <summary>Vanilla plus AMP, and the names AMP declares.</summary>
    private static readonly Lazy<(StatsResolver resolver, List<string> ampStatuses)?> WithAmp = new(() =>
    {
        if (!Directory.Exists(AmpStatsDir)) return null;
        var r = new StatsResolver();
        r.AddEntries(Vanilla.Value.Definitions);
        var names = new List<string>();
        foreach (var file in Directory.GetFiles(AmpStatsDir, "*.txt"))
        {
            var entries = StatsParser.Parse(File.ReadAllText(file));
            r.AddEntries(entries);
            names.AddRange(entries.Where(e => e.Type == "StatusData").Select(e => e.Name));
        }
        return (r, names.Distinct(StringComparer.Ordinal).ToList());
    });

    private static ArtifactDefinition NewRing() => new()
    {
        StatId = "TEST_Ring",
        StatType = "Armor",
        UsingBase = "ARM_Ring_A",
        LootPool = "Rings",
    };

    /// <summary>Every field that differs between the original and its untouched compiled copy.</summary>
    internal static List<string> Differences(string name, StatsResolver resolver, bool editOriginal = false)
    {
        var diffs = new List<string>();
        var art = NewRing();
        var card = StatusCloner.CloneFrom(name, resolver);
        card.EditOriginal = editOriginal;
        art.Statuses.Add(card);
        // Twice on the same object, as the patcher does; the second pass is what goes into the pak.
        ArtifactCompiler.Compile(art, resolver: resolver);
        var compiled = StatsParser.Parse(ArtifactCompiler.Compile(art, resolver: resolver).StatsText);
        var copyName = art.Statuses[0].Name;
        if (!compiled.Any(e => e.Name == copyName)) return [$"{name}: not emitted"];

        var game = new StatsResolver();
        game.AddEntries(resolver.Definitions);
        game.AddEntries(compiled);
        var want = resolver.ResolveAll(name);
        var got = game.ResolveAll(copyName);
        var renames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { [name] = copyName };
        foreach (var key in want.Keys.Union(got.Keys))
        {
            var w = want.GetValueOrDefault(key) ?? "";
            var g = got.GetValueOrDefault(key) ?? "";
            // The copy's own mentions of the status follow it.
            w = ArtifactCompiler.RewriteStatusText(w, renames) ?? "";
            if (key is "DisplayName" or "Description") { w = w.Split(';')[0]; g = g.Split(';')[0]; }
            if (w != g) diffs.Add($"{name}.{key}: '{w}' -> '{g}'");
        }
        return diffs;
    }

    [Fact]
    public void EveryVanillaStatus_CopiedUntouched_ResolvesToTheOriginal()
    {
        var resolver = Vanilla.Value;
        var names = resolver.AllEntries.Values.Where(e => e.Type == "StatusData").Select(e => e.Name).ToList();
        var diffs = names.SelectMany(n => Differences(n, resolver)).ToList();
        foreach (var d in diffs.Take(60)) _output.WriteLine(d);
        Assert.True(diffs.Count == 0, $"{diffs.Count} fields differ across {names.Count} statuses");
    }

    [Fact]
    public void EveryAmpStatus_CopiedUntouched_ResolvesToTheOriginal()
    {
        if (WithAmp.Value is not { } amp) { _output.WriteLine("AMP stats not on disk — skipped"); return; }
        // AMP names a few statuses like its weapons but in capitals (WPN_LONGSWORD_L vs WPN_Longsword_l):
        // different entries, in the game and in the resolver.
        Assert.Empty(amp.ampStatuses.Where(n => amp.resolver.Get(n)?.Type != "StatusData"));
        var diffs = amp.ampStatuses.SelectMany(n => Differences(n, amp.resolver)).ToList();
        foreach (var d in diffs.Take(80)) _output.WriteLine(d);
        _output.WriteLine($"{amp.ampStatuses.Count} AMP statuses");
        Assert.True(diffs.Count == 0, $"{diffs.Count} fields differ across {amp.ampStatuses.Count} statuses");
    }

    [Fact]
    public void EveryVanillaStatus_EditedOriginalUntouched_StaysTheSame()
    {
        var resolver = Vanilla.Value;
        var names = resolver.AllEntries.Values.Where(e => e.Type == "StatusData").Select(e => e.Name).ToList();
        var diffs = names.SelectMany(n => Differences(n, resolver, editOriginal: true)).ToList();
        foreach (var d in diffs.Take(60)) _output.WriteLine(d);
        Assert.True(diffs.Count == 0, $"{diffs.Count} fields differ across {names.Count} statuses");
    }

    [Fact]
    public void EveryAmpStatus_EditedOriginalUntouched_StaysTheSame()
    {
        if (WithAmp.Value is not { } amp) { _output.WriteLine("AMP stats not on disk — skipped"); return; }
        var names = amp.ampStatuses;
        var diffs = names.SelectMany(n => Differences(n, amp.resolver, editOriginal: true)).ToList();
        foreach (var d in diffs.Take(80)) _output.WriteLine(d);
        Assert.True(diffs.Count == 0, $"{diffs.Count} fields differ across {names.Count} statuses");
    }
}
