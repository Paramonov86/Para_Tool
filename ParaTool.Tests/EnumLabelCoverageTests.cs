using System.Text.Json;
using ParaTool.Core.Schema;
using Xunit;
using Xunit.Abstractions;

namespace ParaTool.Tests;

/// <summary>
/// Every value a chip drum or checklist offers reads as words in every language (an <c>enum.</c>
/// key), not as the game's identifier or an English-only fallback.
/// </summary>
public class EnumLabelCoverageTests
{
    private readonly ITestOutputHelper _output;
    public EnumLabelCoverageTests(ITestOutputHelper output) => _output = output;

    private static readonly string[] Languages =
        ["de", "en", "es", "fr", "it", "ja", "ko", "pl", "pt", "ru", "tr", "uk", "zh"];

    private static Dictionary<string, string> LoadLang(string code)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null && !File.Exists(Path.Combine(dir.FullName, "ParaTool.sln"))) dir = dir.Parent;
        var path = Path.Combine(dir!.FullName, "ParaTool.App", "Localization", "langs", $"{code}.json");
        using var stream = File.OpenRead(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
    }

    /// <summary>The values the boost, functor and condition chips put on a drum or checklist.</summary>
    internal static SortedSet<string> ChipEnumValues()
    {
        var values = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var def in BoostMapping.Boosts.Concat(BoostMapping.Functors))
        foreach (var p in def.Params)
            if (p.Type is "enum" or "flags" or "enumlist" && p.EnumValues != null)
                values.UnionWith(p.EnumValues);
        foreach (var f in ConditionSchema.Instance.Functions)
        foreach (var p in f.Params)
            if (p.Type is "enum" or "flags" && p.EnumValues != null && !ConditionSchema.IsEntityParam(p))
                values.UnionWith(p.EnumValues);
        values.UnionWith(BoostMapping.FormulaVariables);
        values.RemoveWhere(v => v.Length == 0 || v == "—");
        return values;
    }

    [Fact]
    public void EveryChipEnumValue_HasALabel_InEveryLanguage()
    {
        var values = ChipEnumValues();
        var missing = new List<string>();
        foreach (var lang in Languages)
        {
            var dict = LoadLang(lang);
            foreach (var v in values)
                if (!dict.TryGetValue($"enum.{v}", out var label) || string.IsNullOrWhiteSpace(label))
                    missing.Add($"{lang}\t{v}");
        }
        _output.WriteLine($"{values.Count} values, {missing.Count} missing");
        foreach (var m in missing.Where(m => m.StartsWith("en\t"))) _output.WriteLine(m);
        Assert.Empty(missing);
    }
}
