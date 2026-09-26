using System.Text.RegularExpressions;
using ParaTool.Core.Effects;
using ParaTool.Core.Icons;
using ParaTool.Core.Services;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

/// <summary>
/// The effect library names the effects statuses use by their <c>.lsfx</c> files. Runs against the
/// game install and AMP's pak on this machine; skipped where they are not.
/// </summary>
public class EffectLibraryTests
{
    private readonly ITestOutputHelper _output;
    public EffectLibraryTests(ITestOutputHelper output) => _output = output;

    private const string AmpPak = @"C:\Users\user\AppData\Local\Larian Studios\Baldur's Gate 3\Mods\REL_Full_Ancient_c6c0d2bd-6198-de9e-30ad-e8cda1793025.pak";

    private static readonly Lazy<(EffectLibrary lib, long ms)?> Library = new(() =>
    {
        var data = GameDataLocator.Find();
        if (data == null) return null;
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var mods = File.Exists(AmpPak) ? new[] { new IconPakSource(AmpPak, "AMP", IsAmp: true) } : [];
        var lib = EffectLibrary.Build(data, mods);
        return (lib, sw.ElapsedMilliseconds);
    });

    [SkippableFact]
    public void VanillaAndAmpEffects_AreNamedByTheirLsfxFiles()
    {
        Skip.If(Library.Value == null, "game install not found");
        var (lib, ms) = Library.Value!.Value;
        _output.WriteLine($"{lib.All.Count} effects in {ms} ms");

        var bless = lib.Find("61fe31f9-ae4b-4926-a033-e56ea67c7d92");
        Assert.NotNull(bless);
        Assert.Equal("BLESS_StatusEffect", bless!.Name);
        Assert.Equal("VFX_Status_Blessed_01", bless.Display);
        Assert.Equal("Vanilla", bless.Source);

        if (File.Exists(AmpPak))
        {
            var amp = lib.Find("6aed7147-dfa9-4ae8-b476-6b4ee2e4b5aa") ?? lib.All.FirstOrDefault(e => e.Source == "AMP");
            Assert.NotNull(amp);
            _output.WriteLine($"AMP sample: {amp!.Uuid} {amp.Name} -> {string.Join(", ", amp.Files)}");
            Assert.Contains(lib.All, e => e.Source == "AMP" && e.Files.Count > 0);
        }
    }

    [SkippableFact]
    public void EveryEffectTheGamesStatusesUse_IsKnown()
    {
        Skip.If(Library.Value == null, "game install not found");
        var lib = Library.Value!.Value.lib;
        var asm = typeof(EffectLibrary).Assembly;
        var name = asm.GetManifestResourceNames().Single(n => n.EndsWith("Vanilla_Statuses.txt"));
        using var s = asm.GetManifestResourceStream(name)!;
        var text = new StreamReader(s).ReadToEnd();
        var used = Regex.Matches(text, "data \"(?:StatusEffect|ApplyEffect|StatusEffectOnTurn|EndEffect|BeamEffect|StatusEffectOverride)\" \"([0-9a-fA-F-]{36})\"")
            .Select(m => m.Groups[1].Value).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

        var unknown = used.Where(u => lib.Find(u) == null).ToList();
        // A MultiEffectInfo listing no effect has no file to name; it shows its own name.
        var unnamed = used.Where(u => lib.Find(u) is { Files.Count: 0, EffectCount: > 0 }).ToList();
        _output.WriteLine($"{used.Count} effects used; unknown {unknown.Count}: {string.Join(", ", unknown.Take(5))}; without a file {unnamed.Count}");
        Assert.Empty(unknown);
        Assert.True(unnamed.Count <= used.Count / 100, $"{unnamed.Count} effects without an .lsfx name");
    }
}
