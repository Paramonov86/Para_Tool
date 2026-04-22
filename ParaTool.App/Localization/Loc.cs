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
        _lang = code;
        LoadStrings(code);
        // Defer all PropertyChanged notifications to avoid collection-modified crashes
        // when handlers subscribe/unsubscribe during enumeration
        Avalonia.Threading.Dispatcher.UIThread.Post(() => OnPropertyChanged(string.Empty));
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
    /// Special key "_lang" returns the current language code (e.g. "en", "ru").
    /// </summary>
    public string this[string key] => key == "_lang" ? _lang : _strings.TryGetValue(key, out var val) ? val : key;

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
    public string ConstructorGuideTitle => this["ConstructorGuideTitle"];
    public string ConstructorStep1 => this["ConstructorStep1"];
    public string ConstructorStep2 => this["ConstructorStep2"];
    public string ConstructorStep3 => this["ConstructorStep3"];
    public string ConstructorStep4 => this["ConstructorStep4"];
    public string ConstructorStep5 => this["ConstructorStep5"];
    public string ConstructorStep6 => this["ConstructorStep6"];
    public string ConstructorStep7 => this["ConstructorStep7"];
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

    public string LblArmor => this["LblArmor"];
    public string LblWeapon => this["LblWeapon"];
    public string LblPassives => this["LblPassives"];
    public string LblArmorClass => this["LblArmorClass"];
    public string LblArmorType => this["LblArmorType"];
    public string LblProficiency => this["LblProficiency"];
    public string LblWeight => this["LblWeight"];
    public string LblDamage => this["LblDamage"];
    public string LblDamageType => this["LblDamageType"];
    public string LblVersatile => this["LblVersatile"];
    public string LblDefaultBoosts => this["LblDefaultBoosts"];
    public string LblBoostsMainHand => this["LblBoostsMainHand"];
    public string LblBoostsOffHand => this["LblBoostsOffHand"];
    public string LblProperties => this["LblProperties"];
    public string LblUnique => this["LblUnique"];
    public string TipUnique => this["TipUnique"];
    public string LblPermanentBoosts => this["LblPermanentBoosts"];
    public string LblTriggerCondAction => this["LblTriggerCondAction"];
    public string LblWhen => this["LblWhen"];
    public string LblIf => this["LblIf"];
    public string LblDo => this["LblDo"];
    public string LblContext => this["LblContext"];
    public string LblCreateNewItem => this["LblCreateNewItem"];
    public string LblBasedOn => this["LblBasedOn"];
    public string BtnCreateArtifact => this["BtnCreateArtifact"];
    public string LblChooseIcon => this["LblChooseIcon"];
    public string BtnClose => this["BtnClose"];
    public string LblUploadPng => this["LblUploadPng"];
    public string TipUploadPng => this["TipUploadPng"];
    public string LblLoadingIcons => this["LblLoadingIcons"];
    public string WmSearchIcons => this["WmSearchIcons"];
    public string WmItemName => this["WmItemName"];
    public string WmPassiveName => this["WmPassiveName"];
    public string WmNewItemName => this["WmNewItemName"];
    public string WmSearch => this["WmSearch"];
    public string WmSearchBoost => this["WmSearchBoost"];
    public string BoostPickerIfBoost => this["BoostPickerIfBoost"];
    public string BoostPickerIfFunctor => this["BoostPickerIfFunctor"];
    public string BoostPickerWeaponSection => this["BoostPickerWeaponSection"];
    public string SortChips => this["SortChips"];
    public string ShowInExplorer => this["ShowInExplorer"];
    public string ShowInExplorerTooltip => this["ShowInExplorerTooltip"];
    public string LblCustomOption => this["LblCustomOption"];
    public string WmTypeAndEnter => this["WmTypeAndEnter"];
    public string LblMadeWith => this["LblMadeWith"];
    public string LblSupportTool => this["LblSupportTool"];
    public string OpenArtifactsFolder => this["OpenArtifactsFolder"];
    public string OpenArtifactsFolderTooltip => this["OpenArtifactsFolderTooltip"];
    public string LblIconCount => this["LblIconCount"];
    public string BbTipBold => this["BbTipBold"];
    public string BbTipItalic => this["BbTipItalic"];
    public string BbTipStatus => this["BbTipStatus"];
    public string BbTipTooltip => this["BbTipTooltip"];
    public string BbTipSpell => this["BbTipSpell"];
    public string BbTipPassive => this["BbTipPassive"];
    public string BbTipResource => this["BbTipResource"];
    public string BbTipParam => this["BbTipParam"];
    public string BbTipLineBreak => this["BbTipLineBreak"];
    public string TipToggleCodePreview => this["TipToggleCodePreview"];
    public string LblCardMechanics => this["LblCardMechanics"];
    public string TipDefaultTab => this["TipDefaultTab"];
    public string TipFontSize => this["TipFontSize"];
    public string TipColorTheme => this["TipColorTheme"];
    public string WmBrowseIcons => this["WmBrowseIcons"];
    public string WmPassiveDesc => this["WmPassiveDesc"];
    public string WmDescriptionLore => this["WmDescriptionLore"];
    public string LblNoIconSelected => this["LblNoIconSelected"];
    public string DlgSelectPng => this["DlgSelectPng"];
    public string WmSearchPassive => this["WmSearchPassive"];
    public string LblAddEmptyPassive => this["LblAddEmptyPassive"];
    public string WmSearchItems => this["WmSearchItems"];
    public string LblHideDisabled => this["LblHideDisabled"];
    public string LblThemeFilter => this["LblThemeFilter"];
    public string LblShowThemes => this["LblShowThemes"];
    public string LblNoTheme => this["LblNoTheme"];
    public string TipOpenInConstructor => this["TipOpenInConstructor"];
    public string CtxRename => this["CtxRename"];
    public string CtxReplace => this["CtxReplace"];
    public string WmSearchCondition => this["WmSearchCondition"];
    public string DlgRenameTitle => this["DlgRenameTitle"];
    public string WmNewName => this["WmNewName"];

    /// <summary>Get display labels for enum values, using loca with EnumLabels fallback.</summary>
    public string[] GetEnumDisplayLabels(string[] values)
    {
        return values.Select(v =>
        {
            var key = $"enum.{v}";
            if (_strings.TryGetValue(key, out var locaVal)) return locaVal;
            return ParaTool.Core.Schema.EnumLabels.GetLabel(v, _lang);
        }).ToArray();
    }

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
