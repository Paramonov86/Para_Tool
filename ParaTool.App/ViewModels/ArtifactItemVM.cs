using System.Collections.ObjectModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.App.Localization;
using ParaTool.Core.Artifacts;

namespace ParaTool.App.ViewModels;

/// <summary>
/// In-memory working copy of an artifact. NO auto-save.
/// Changes are only persisted when the user clicks "Save".
/// </summary>
public partial class ArtifactItemVM : ObservableObject
{
    public ArtifactDefinition Artifact { get; }

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isDirty;
    [ObservableProperty] private WriteableBitmap? _iconBitmap;

    public bool HasIcon => IconBitmap != null;
    partial void OnIconBitmapChanged(WriteableBitmap? value) => OnPropertyChanged(nameof(HasIcon));

    /// <summary>StatId of the source item this was loaded from (for Reset).</summary>
    public string SourceStatId { get; set; } = "";

    /// <summary>Whether this artifact has been saved to disk at least once.</summary>
    public bool IsPersisted { get; set; }

    private static readonly SolidColorBrush SelectedBg = new(Color.Parse("#3D3A4D"));
    private static readonly SolidColorBrush NormalBg = new(Colors.Transparent);
    public IBrush SelectionBackground => IsSelected ? SelectedBg : NormalBg;
    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(SelectionBackground));

    /// <summary>Reference to parent for editing language.</summary>
    public Func<string>? GetEditingLang { get; set; }

    private string EditLang => GetEditingLang?.Invoke() ?? Loc.Instance.Lang;

    public ArtifactItemVM(ArtifactDefinition artifact)
    {
        Artifact = artifact;
    }

    // === Navigator display ===

    public string DisplayLabel
    {
        get
        {
            var name = GetLocalizedName();
            return !string.IsNullOrEmpty(name) ? name : Artifact.StatId;
        }
    }

    public IBrush RarityColor => GetRarityBrush(Artifact.Rarity);

    // === Chip options ===

    public static string[] RarityOptions => ["Common", "Uncommon", "Rare", "VeryRare", "Legendary"];
    public static string[] PoolOptions =>
    [
        "Clothes", "Armor", "Shields", "Hats", "Cloaks",
        "Gloves", "Boots", "Amulets", "Rings",
        "Weapons", "Weapons_1H", "Weapons_2H"
    ];
    public static string[] WeaponPropertyOptions => ParaTool.Core.Schema.BoostMapping.WeaponProperties;
    public static string[] ArmorTypeOptions => ParaTool.Core.Schema.BoostMapping.ArmorTypes;
    public static string[] ArmorTypeLabels => ParaTool.Core.Schema.BoostMapping.ArmorTypeLabels;
    public static string[] ProficiencyOptions => ParaTool.Core.Schema.BoostMapping.ProficiencyTypes;
    public static string[] ProficiencyLabels => ParaTool.Core.Schema.BoostMapping.ProficiencyLabels;

    public static string[] ThemeOptions =>
    [
        "Swamp", "Aquatic", "Shadowfell", "Arcane", "Celestial",
        "Nature", "Destructive", "War", "Psionic", "Primal"
    ];

    // === Editable properties (in-memory, no auto-save) ===

    public string EditStatId
    {
        get => Artifact.StatId;
        set { if (Artifact.StatId == value) return; Artifact.StatId = value; MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLabel)); }
    }

    public string EditRarity
    {
        get => Artifact.Rarity;
        set { if (Artifact.Rarity == value) return; Artifact.Rarity = value; MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(RarityColor)); OnPropertyChanged(nameof(PreviewRarityText)); }
    }

    public string EditUsingBase => Artifact.UsingBase;

    public string EditPool
    {
        get => Artifact.LootPool ?? "";
        set { Artifact.LootPool = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
    }

    public bool IsThemeSelected(string theme) => Artifact.LootThemes.Contains(theme);

    public void ToggleTheme(string theme)
    {
        if (Artifact.LootThemes.Contains(theme))
            Artifact.LootThemes.Remove(theme);
        else
            Artifact.LootThemes.Add(theme);
        MarkDirty();
    }

    // Type-dependent visibility
    public bool IsArmor => Artifact.StatType == "Armor";
    public bool IsWeapon => Artifact.StatType == "Weapon";

    /// <summary>Show AC only for actual armor/shields, not rings/amulets/etc.</summary>
    public bool ShowArmorClass
    {
        get
        {
            if (!IsArmor) return false;
            var pool = Artifact.LootPool;
            // Rings, Amulets, Cloaks, Boots, Gloves — no AC
            return pool is null or "Armor" or "Clothes" or "Shields" or "Hats";
        }
    }

    // Armor properties
    public string EditArmorClass
    {
        get => Artifact.ArmorClass < 0 ? "" : Artifact.ArmorClass.ToString();
        set { Artifact.ArmorClass = int.TryParse(value, out var v) ? v : -1; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditArmorType
    {
        get => Artifact.ArmorType ?? "";
        set { Artifact.ArmorType = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditProficiencyGroup
    {
        get => Artifact.ProficiencyGroup ?? "";
        set { Artifact.ProficiencyGroup = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
    }

    // Weapon properties
    public string EditDamage
    {
        get => Artifact.Damage ?? "";
        set { Artifact.Damage = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditVersatileDamage
    {
        get => Artifact.VersatileDamage ?? "";
        set { Artifact.VersatileDamage = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditDefaultBoosts
    {
        get => Artifact.DefaultBoosts ?? "";
        set { Artifact.DefaultBoosts = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditWeaponProperties
    {
        get => Artifact.WeaponProperties ?? "";
        set { Artifact.WeaponProperties = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditWeight
    {
        get => Artifact.Weight < 0 ? "" : (Artifact.Weight == (int)Artifact.Weight
            ? ((int)Artifact.Weight).ToString()
            : Artifact.Weight.ToString("G", System.Globalization.CultureInfo.InvariantCulture));
        set { Artifact.Weight = double.TryParse(value?.Replace(',', '.'), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var w) ? w : -1; MarkDirty(); OnPropertyChanged(); }
    }

    public bool EditUnique
    {
        get => Artifact.Unique;
        set { if (Artifact.Unique == value) return; Artifact.Unique = value; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditValueOverride
    {
        get => Artifact.ValueOverride.ToString();
        set { if (int.TryParse(value, out var v)) { Artifact.ValueOverride = v; MarkDirty(); OnPropertyChanged(); } }
    }

    // Mechanics
    public string EditBoosts
    {
        get => Artifact.Boosts;
        set { Artifact.Boosts = value ?? ""; MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(HasBoosts)); }
    }

    public string EditPassivesOnEquip
    {
        get => Artifact.PassivesOnEquip;
        set { Artifact.PassivesOnEquip = value ?? ""; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditStatusOnEquip
    {
        get => Artifact.StatusOnEquip;
        set { Artifact.StatusOnEquip = value ?? ""; MarkDirty(); OnPropertyChanged(); }
    }

    public string EditSpellsOnEquip
    {
        get => Artifact.SpellsOnEquip;
        set { Artifact.SpellsOnEquip = value ?? ""; MarkDirty(); OnPropertyChanged(); }
    }

    // Localization
    public string EditDisplayName
    {
        get => GetLangValue(Artifact.DisplayName);
        set { SetLangValue(Artifact.DisplayName, value); MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(DisplayLabel)); OnPropertyChanged(nameof(PreviewName)); }
    }

    public string EditDescription
    {
        get => GetLangValue(Artifact.Description);
        set { SetLangValue(Artifact.Description, value); MarkDirty(); OnPropertyChanged(); OnPropertyChanged(nameof(PreviewDescription)); OnPropertyChanged(nameof(HasDescription)); }
    }

    // === Passives (editable list) ===

    public ObservableCollection<PassiveVM> PassiveVMs { get; } = [];

    public void LoadPassivesFromArtifact()
    {
        PassiveVMs.Clear();
        foreach (var p in Artifact.Passives ?? [])
        {
            if (p?.Properties?.Contains("IsHidden", StringComparison.OrdinalIgnoreCase) ?? false) continue;
            if (p != null)
                PassiveVMs.Add(new PassiveVM(p, this));
        }
        OnPropertyChanged(nameof(HasPassives));
    }

    // === Preview ===

    public string PreviewName => !string.IsNullOrEmpty(GetLocalizedName()) ? GetLocalizedName() : Artifact.StatId;
    public string PreviewRarityText => Loc.Instance.RarityName(Artifact.Rarity);
    public string PreviewSlot => Artifact.LootPool != null ? Loc.Instance.PoolName(Artifact.LootPool) : Artifact.StatType;
    public string PreviewSubtitle => $"{PreviewRarityText} · {PreviewSlot}";
    public string PreviewUsing => !string.IsNullOrEmpty(Artifact.UsingBase) ? $"using {Artifact.UsingBase}" : "";
    public string PreviewDescription => GetLangValue(Artifact.Description);
    public bool HasBoosts => !string.IsNullOrWhiteSpace(Artifact.Boosts);
    public bool HasPassives => (Artifact.Passives?.Count ?? 0) > 0;
    public bool HasDescription => !string.IsNullOrWhiteSpace(PreviewDescription);

    public List<(string name, string description)> GetPassiveTexts()
    {
        var lang = EditLang;
        return (Artifact.Passives ?? [])
            .Where(p => !p.Properties.Contains("IsHidden", StringComparison.OrdinalIgnoreCase))
            .Select(p =>
            {
                var name = p.DisplayName.TryGetValue(lang, out var n) && !string.IsNullOrEmpty(n) ? n : p.Name;
                var desc = p.Description.TryGetValue(lang, out var d) ? d : "";
                return (name, desc);
            })
            .Where(t => !string.IsNullOrEmpty(t.name))
            .ToList();
    }

    // === Helpers ===

    private void MarkDirty() => IsDirty = true;

    private string GetLocalizedName()
    {
        var lang = Loc.Instance.Lang;
        if (Artifact.DisplayName.TryGetValue(lang, out var n) && !string.IsNullOrEmpty(n)) return n;
        if (Artifact.DisplayName.TryGetValue("en", out var en) && !string.IsNullOrEmpty(en)) return en;
        return "";
    }

    private string GetLangValue(Dictionary<string, string> dict)
    {
        var lang = EditLang;
        if (dict.TryGetValue(lang, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (dict.TryGetValue("en", out var en) && !string.IsNullOrEmpty(en)) return en;
        return "";
    }

    private void SetLangValue(Dictionary<string, string> dict, string? value)
    {
        dict[EditLang] = value ?? "";
    }

    public void RefreshAll()
    {
        OnPropertyChanged(nameof(DisplayLabel));
        OnPropertyChanged(nameof(RarityColor));
        OnPropertyChanged(nameof(PreviewName));
        OnPropertyChanged(nameof(PreviewRarityText));
        OnPropertyChanged(nameof(PreviewDescription));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasBoosts));
        OnPropertyChanged(nameof(HasPassives));
        OnPropertyChanged(nameof(EditDisplayName));
        OnPropertyChanged(nameof(EditDescription));
        OnPropertyChanged(nameof(EditBoosts));
        OnPropertyChanged(nameof(EditPassivesOnEquip));
        OnPropertyChanged(nameof(EditStatusOnEquip));
        OnPropertyChanged(nameof(EditSpellsOnEquip));
        LoadPassivesFromArtifact();
    }

    private static SolidColorBrush GetRarityBrush(string rarity) => rarity switch
    {
        "Common" => new(Color.Parse("#8A8494")),
        "Uncommon" => new(Color.Parse("#2ECC71")),
        "Rare" => new(Color.Parse("#3498DB")),
        "VeryRare" => new(Color.Parse("#9B59B6")),
        "Legendary" => new(Color.Parse("#C8A96E")),
        _ => new(Color.Parse("#8A8494")),
    };
}

/// <summary>
/// Wraps a single PassiveDefinition for editing in the UI.
/// </summary>
public partial class PassiveVM : ObservableObject
{
    public PassiveDefinition Passive { get; }
    private readonly ArtifactItemVM _parent;

    private string EditLang => _parent.GetEditingLang?.Invoke() ?? Loc.Instance.Lang;

    public static string[] TriggerOptions { get; } = ParaTool.Core.Schema.BoostMapping.TriggerEvents.Keys.ToArray();

    public PassiveVM(PassiveDefinition passive, ArtifactItemVM parent)
    {
        Passive = passive;
        _parent = parent;
    }

    [ObservableProperty] private bool _isExpanded;

    public string Name
    {
        get
        {
            // Try localized name first, fallback to StatId
            var lang = EditLang;
            if (Passive.DisplayName.TryGetValue(lang, out var locName) && !string.IsNullOrEmpty(locName))
                return locName;
            if (Passive.DisplayName.TryGetValue("en", out var enName) && !string.IsNullOrEmpty(enName))
                return enName;
            return Passive.Name;
        }
    }

    public string StatName => Passive.Name;

    public string EditDisplayName
    {
        get => GetLang(Passive.DisplayName);
        set
        {
            SetLang(Passive.DisplayName, value);
            _parent.IsDirty = true;
            // Auto-generate StatId from human name if creating new
            if (string.IsNullOrEmpty(Passive.Name) || Passive.Name.StartsWith("Passive_New"))
            {
                var cleaned = System.Text.RegularExpressions.Regex.Replace((value ?? "").Trim(), @"[^a-zA-Z0-9\s]", "");
                var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length > 0)
                {
                    Passive.Name = "Passive_" + string.Join("_", parts.Select(p => char.ToUpper(p[0]) + (p.Length > 1 ? p[1..] : "")));
                    OnPropertyChanged(nameof(Name));
                }
            }
            OnPropertyChanged();
        }
    }

    public string EditDescription
    {
        get => GetLang(Passive.Description);
        set { SetLang(Passive.Description, value); _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditDescriptionParams
    {
        get => Passive.DescriptionParams;
        set { Passive.DescriptionParams = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditBoostContext
    {
        get => Passive.BoostContext;
        set { Passive.BoostContext = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditBoosts
    {
        get => Passive.Boosts;
        set { Passive.Boosts = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditBoostConditions
    {
        get => Passive.BoostConditions;
        set { Passive.BoostConditions = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditStatsFunctorContext
    {
        get => Passive.StatsFunctorContext;
        set { Passive.StatsFunctorContext = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditConditions
    {
        get => Passive.Conditions;
        set { Passive.Conditions = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditStatsFunctors
    {
        get => Passive.StatsFunctors;
        set { Passive.StatsFunctors = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string? EditIcon
    {
        get => Passive.Icon;
        set { Passive.Icon = value; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    private string GetLang(Dictionary<string, string> dict)
    {
        var lang = EditLang;
        if (dict.TryGetValue(lang, out var v) && !string.IsNullOrEmpty(v)) return v;
        if (dict.TryGetValue("en", out var en) && !string.IsNullOrEmpty(en)) return en;
        return "";
    }

    private void SetLang(Dictionary<string, string> dict, string? value)
    {
        dict[EditLang] = value ?? "";
    }
}
