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

    public string ProfileLabel => this["ProfileLabel"];
    public string ProfileSave => this["ProfileSave"];
    public string ProfileSaveAs => this["ProfileSaveAs"];
    public string ProfileLoad => this["ProfileLoad"];
    public string ProfileDelete => this["ProfileDelete"];
    public string ProfileNewName => this["ProfileNewName"];
    public string ProfileSaved => this["ProfileSaved"];
    public string MissingItemsWarning => this["MissingItemsWarning"];
    public string MissingItemsDismiss => this["MissingItemsDismiss"];

    public string SortByName => this["SortByName"];
    public string SortByRarity => this["SortByRarity"];
    public string SortByTheme => this["SortByTheme"];
    public string SortBySlot => this["SortBySlot"];

    public string ScanStageMods => this["ScanStageMods"];
    public string ScanStageAMP => this["ScanStageAMP"];
    public string ScanStageResolver => this["ScanStageResolver"];
    public string ScanStageNames => this["ScanStageNames"];
    public string ScanStageTemplates => this["ScanStageTemplates"];
    public string ScanStageLoca => this["ScanStageLoca"];
    public string ScanStageDone => this["ScanStageDone"];

    public string FolderPickerTitle => this["FolderPickerTitle"];
    public string UpdateCheckTooltip => this["UpdateCheckTooltip"];
    public string UpdateCheckingTooltip => this["UpdateCheckingTooltip"];
    public string UpdateAvailableTooltip(string version) => string.Format(this["UpdateAvailableTooltip"], version);
    public string UpdateDownloadingTooltip(int progress) => string.Format(this["UpdateDownloadingTooltip"], progress);
    public string UpdateUpToDateTooltip => this["UpdateUpToDateTooltip"];
    public string UpdateFailedTooltip => this["UpdateFailedTooltip"];

    public string PatchingInProgress => this["PatchingInProgress"];
    public string PatchSuccessLabel => this["PatchSuccess"];
    public string PatchErrorLabel => this["PatchError"];

    public string PatchSuccessMessage(int count) => string.Format(this["PatchSuccessMessage"], count);

    public string RestoreAmpTooltip => this["RestoreAmpTooltip"];
    public string RestoringAmp => this["RestoringAmp"];

    public string TabPatcher => this["TabPatcher"];
    public string TabConstructor => this["TabConstructor"];

    public string NavTitle => this["NavTitle"];
    public string NavSearch => this["NavSearch"];
    public string NavMyItems => this["NavMyItems"];
    public string NavAllItems => this["NavAllItems"];
    public string NavCreateInheritor => this["NavCreateInheritor"];
    public string NavDuplicate => this["NavDuplicate"];
    public string NavDelete => this["NavDelete"];
    public string ConstructorEditorTitle => this["ConstructorEditorTitle"];
    public string ConstructorPreviewTitle => this["ConstructorPreviewTitle"];

    public string EditorNoSelection => this["EditorNoSelection"];
    public string CardIdentity => this["CardIdentity"];
    public string CardArmorProps => this["CardArmorProps"];
    public string CardWeaponProps => this["CardWeaponProps"];
    public string CardMechanics => this["CardMechanics"];
    public string CardDescription => this["CardDescription"];
    public string FieldType => this["FieldType"];
    public string FieldRarity => this["FieldRarity"];
    public string FieldUnique => this["FieldUnique"];
    public string FieldValue => this["FieldValue"];
    public string FieldWeight => this["FieldWeight"];
    public string FieldName => this["FieldName"];
    public string FieldDescription => this["FieldDescription"];
    public string ChipSlot => this["ChipSlot"];
    public string ChipThemes => this["ChipThemes"];
    public string BtnSave => this["BtnSave"];
    public string BtnReset => this["BtnReset"];

    public string LblBoosts => this["LblBoosts"];
    public string LblStatuses => this["LblStatuses"];
    public string LblSpells => this["LblSpells"];
    public string LblWhenApplied => this["LblWhenApplied"];
    public string LblEffects => this["LblEffects"];
    public string LblTrigger => this["LblTrigger"];
    public string LblCondition => this["LblCondition"];
    public string LblAction => this["LblAction"];
    public string LblIcon => this["LblIcon"];
    public string LblIconBrowser => this["LblIconBrowser"];

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
