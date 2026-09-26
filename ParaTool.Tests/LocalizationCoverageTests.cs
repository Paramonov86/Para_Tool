using System.Text.Json;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

public class LocalizationCoverageTests
{
    private readonly ITestOutputHelper _output;
    public LocalizationCoverageTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] Languages =
        ["de", "en", "es", "fr", "it", "ja", "ko", "pl", "pt", "ru", "tr", "uk", "zh"];

    private static string LangDir
    {
        get
        {
            // Walk up from test assembly location to repo root, then dive into ParaTool.App.
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ParaTool.sln")))
                dir = dir.Parent;
            if (dir == null) throw new DirectoryNotFoundException("Repo root not found");
            return Path.Combine(dir.FullName, "ParaTool.App", "Localization", "langs");
        }
    }

    private static Dictionary<string, string> LoadLang(string code)
    {
        var path = Path.Combine(LangDir, $"{code}.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)
            ?? new Dictionary<string, string>();
    }

    [Fact]
    public void AllLanguages_HaveAllBoostKeys_FromEnglish()
    {
        // Only check keys starting with "boost." — UI strings may legitimately be missing
        // from some locales during ongoing translation work.
        var en = LoadLang("en");
        var enBoost = en.Keys.Where(k => k.StartsWith("boost.") || k.StartsWith("enum.") || k.StartsWith("vl.")).ToList();

        var missing = new List<string>();
        foreach (var lang in Languages.Where(l => l != "en"))
        {
            var dict = LoadLang(lang);
            foreach (var key in enBoost)
            {
                if (!dict.ContainsKey(key))
                    missing.Add($"{lang}: missing '{key}'");
            }
        }
        if (missing.Any())
        {
            _output.WriteLine($"Total missing boost/enum keys: {missing.Count}");
            var sample = string.Join("\n", missing.Take(20));
            Assert.Fail($"Languages have missing boost/enum keys:\n{sample}{(missing.Count > 20 ? $"\n... (+{missing.Count - 20} more)" : "")}");
        }
    }

    // Helper templates used by other descriptions (not user-facing chip previews) —
    // safe to skip in coverage check.
    private static readonly HashSet<string> HelperKeys =
    [
        "SavingThrow", "ArmorType.Clothing", "WeaponSkill", "WeaponSkills", "Attack",
        "Encumber", "HeavyEncumber", "ExceedCapacity", "CapacityExceeded",
    ];

    [Fact]
    public void AllEngineDescriptions_HaveBoostKey_InEnglishJson()
    {
        var en = LoadLang("en");
        var missing = ParaTool.Core.Schema.BoostMapping.EngineDescriptions.Keys
            .Where(k => !HelperKeys.Contains(k))
            .Where(k => !en.ContainsKey($"boost.{k}"))
            .ToList();
        Assert.True(missing.Count == 0,
            $"EngineDescriptions keys missing from en.json:\n  boost.{string.Join("\n  boost.", missing)}");
    }

    [Fact]
    public void AllLanguages_HaveEveryKey_FromEnglish()
    {
        var en = LoadLang("en");
        var missing = Languages.Where(l => l != "en")
            .SelectMany(l => { var d = LoadLang(l); return en.Keys.Where(k => !d.ContainsKey(k)).Select(k => $"{l}: {k}"); })
            .ToList();
        Assert.True(missing.Count == 0, string.Join("\n", missing.Take(30)));
    }

    /// <summary>Strings that are rightly the same as the English in a language (loanwords, abbreviations, names).</summary>
    private static readonly Dictionary<string, string[]> SameAsEnglish = new()
    {
        ["de"] = ["BoostCat_TagsFlags", "WmAuraRadius", "boost.CreateExplosion", "boost.Initiative", "enum.Finesse", "enum.Neutral", "enum.Religion", "enum.Resistant", "enum.Sprint", "enum.SurfaceLava"],
        ["es"] = ["PatchError", "boost.Invulnerable", "enum.Abjuration", "enum.Concentration", "enum.Evocation", "enum.Gargantuan", "enum.Investigation", "enum.Neutral", "enum.Perception", "enum.Resistant", "enum.SG_Invisible", "enum.Sprint", "enum.SurfaceAlcohol", "enum.SurfaceLava", "enum.Transmutation", "enum.Vulnerable"],
        ["fr"] = ["condparam.source", "enum.Abjuration", "enum.Acrobatics", "enum.Concentration", "enum.Divination", "enum.Enchantment", "enum.Finesse", "enum.Force", "enum.Gargantuan", "enum.Investigation", "enum.Lance", "enum.Nature", "enum.OBSERVER_SOURCE", "enum.Permanent", "enum.Poison", "enum.Radiant", "enum.Rage", "enum.Religion", "enum.SG_Invisible", "enum.SourceDialogue", "enum.Sprint", "enum.SurfacePoison", "enum.Transmutation", "enum.Vulnerable"],
        ["it"] = ["enum.Immune", "enum.Resistant", "enum.SurfaceLava", "enum.Versatile", "enum.Vulnerable"],
        ["ja"] = ["JrnStatId"],
        ["ko"] = ["JrnStatId"],
        ["pl"] = ["JrnStatId", "enum.Sprint"],
        ["pt"] = ["JrnStatId", "enum.Abjuration", "enum.Concentration", "enum.Cone", "enum.Conjuration", "enum.Evocation", "enum.Investigation", "enum.Resistant", "enum.SG_Incapacitated", "enum.Sprint", "enum.SurfaceLava", "enum.Transmutation", "enum.Vulnerable"],
        ["tr"] = ["JrnStatId", "enum.BonusActionPoint", "enum.Sprint"],
        ["uk"] = ["JrnStatId"],
        ["zh"] = ["JrnStatId"],
    };

    [Fact]
    public void NoLanguage_ShowsAnEnglishCopy()
    {
        // A string left in English reads as untranslated; one that is rightly the same goes into SameAsEnglish.
        var en = LoadLang("en");
        // A single word the same in both is usually the language's own (German Name, French Type, Spanish Invisible);
        // an English phrase left as it is reads as untranslated.
        static bool Trivial(string v)
        {
            var words = System.Text.RegularExpressions.Regex.Replace(v, @"\[\d+\]|\{\d+\}", "").Trim();
            return !words.Contains(' ') || !words.Any(char.IsLetter);
        }
        var copies = new List<string>();
        foreach (var lang in Languages.Where(l => l is not ("en" or "ru")))
        {
            var d = LoadLang(lang);
            var allowed = SameAsEnglish.GetValueOrDefault(lang) ?? [];
            copies.AddRange(en.Where(kv => !kv.Key.StartsWith("_") && d.TryGetValue(kv.Key, out var v) && v == kv.Value
                                           && !Trivial(kv.Value) && !allowed.Contains(kv.Key))
                .Select(kv => $"{lang}: {kv.Key} = '{kv.Value}'"));
        }
        Assert.True(copies.Count == 0, $"{copies.Count} English copies:\n" + string.Join("\n", copies.Take(40)));
    }
}
