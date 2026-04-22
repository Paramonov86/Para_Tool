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
        var enBoost = en.Keys.Where(k => k.StartsWith("boost.") || k.StartsWith("enum.")).ToList();

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
}
