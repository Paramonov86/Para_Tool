using System.Globalization;
using System.Reflection;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParaTool.App.Localization;

public class LangInfo
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public override string ToString() => Name;
}

public partial class Loc : ObservableObject
{
    public static Loc Instance { get; } = new();

    private Dictionary<string, string> _strings = new();
    private const string ResourcePrefix = "ParaTool.App.Localization.langs.";

    [ObservableProperty] private string _lang = "en";

    public LangInfo[] AvailableLanguages { get; private set; } = [];

    private Loc()
    {
        DiscoverLanguages();
        var sysLang = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        var defaultCode = AvailableLanguages.Any(l => l.Code == sysLang) ? sysLang : "en";
        LoadStrings(defaultCode);
        _lang = defaultCode;
    }

    private void DiscoverLanguages()
    {
        var assembly = typeof(Loc).Assembly;
        var names = assembly.GetManifestResourceNames()
            .Where(n => n.StartsWith(ResourcePrefix) && n.EndsWith(".json"))
            .ToList();

        var langs = new List<LangInfo>();
        foreach (var name in names)
        {
            try
            {
                var code = name[ResourcePrefix.Length..^".json".Length];
                using var stream = assembly.GetManifestResourceStream(name);
                if (stream == null) continue;
                var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(stream);
                if (dict == null) continue;
                var displayName = dict.TryGetValue("_name", out var n) ? n : code;
                langs.Add(new LangInfo { Code = code, Name = displayName });
            }
            catch { /* skip malformed language file */ }
        }

        AvailableLanguages = langs.OrderBy(l => l.Name, StringComparer.CurrentCulture).ToArray();
    }

    public void SetLanguage(string code)
    {
        Lang = code;
        LoadStrings(code);
        OnPropertyChanged(string.Empty);
    }

    private void LoadStrings(string code)
    {
        var assembly = typeof(Loc).Assembly;
        var resourceName = $"{ResourcePrefix}{code}.json";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream != null)
        {
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new();
            return;
        }

        // Fallback to English
        var enResource = $"{ResourcePrefix}en.json";
        using var enStream = assembly.GetManifestResourceStream(enResource);
        if (enStream != null)
            _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(enStream) ?? new();
    }

    /// <summary>
    /// Get a localized string by key. Returns the key itself if not found.
    /// </summary>
    public string this[string key] => _strings.TryGetValue(key, out var val) ? val : key;

    // === XAML binding properties ===

    public string AppTitle => this["AppTitle"];
    public string ErrorModsNotFound => this["ErrorModsNotFound"];
    public string SelectFolder => this["SelectFolder"];

    public string PleaseWait => this["PleaseWait"];
    public string ScanningMods => this["ScanningMods"];
    public string MayTakeTime => this["MayTakeTime"];
    public string ModsScanned => this["ModsScanned"];
    public string ModsSelected => this["ModsSelected"];

    public string EditorTitle => this["EditorTitle"];
    public string AllMods => this["AllMods"];
    public string Instructions => this["Instructions"];
    public string InstructionStep1 => this["InstructionStep1"];
    public string InstructionStep2 => this["InstructionStep2"];
    public string InstructionStep3 => this["InstructionStep3"];
    public string InstructionStep4 => this["InstructionStep4"];
    public string PatchButton => this["PatchButton"];
    public string ThemesLabel => this["ThemesLabel"];
    public string ModThemes => this["ModThemes"];

    public string ModsFoundInfo(int count) => string.Format(this["ModsFoundInfo"], count);

    public string IntegratorTitle => this["IntegratorTitle"];

    public string PatchingInProgress => this["PatchingInProgress"];
    public string PatchSuccessLabel => this["PatchSuccess"];
    public string PatchErrorLabel => this["PatchError"];

    public string PatchSuccessMessage(int count) => string.Format(this["PatchSuccessMessage"], count);

    // === Dynamic lookups ===

    public string PoolName(string pool)
    {
        var key = $"Slot.{pool}";
        return _strings.TryGetValue(key, out var val) ? val : pool;
    }

    public string RarityName(string rarity)
    {
        var key = $"Rarity.{rarity}";
        return _strings.TryGetValue(key, out var val) ? val : rarity;
    }

    public string ThemeName(string theme)
    {
        var key = $"Theme.{theme}";
        return _strings.TryGetValue(key, out var val) ? val : theme;
    }
}
