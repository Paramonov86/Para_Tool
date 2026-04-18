using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.App.Localization;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Localization;

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

    public IBrush SelectionBackground => IsSelected ? Themes.ThemeBrushes.HoverBg : Brushes.Transparent;
    partial void OnIsSelectedChanged(bool value) => OnPropertyChanged(nameof(SelectionBackground));

    /// <summary>Reference to parent for editing language.</summary>
    public Func<string>? GetEditingLang { get; set; }

    private string EditLang => GetEditingLang?.Invoke() ?? Loc.Instance.Lang;

    private readonly PropertyChangedEventHandler _langHandler;

    public ArtifactItemVM(ArtifactDefinition artifact)
    {
        Artifact = artifact;
        _langHandler = (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RefreshAll);
        Loc.Instance.PropertyChanged += _langHandler;
    }

    public void Detach() => Loc.Instance.PropertyChanged -= _langHandler;

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

    public LinearGradientBrush RarityGradient
    {
        get
        {
            var c = GetRarityColor(Artifact.Rarity);
            return new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(0, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(140, c.R, c.G, c.B), 0),
                    new GradientStop(Color.FromArgb(50, c.R, c.G, c.B), 0.5),
                    new GradientStop(Color.FromArgb(0, c.R, c.G, c.B), 1),
                }
            };
        }
    }

    public LinearGradientBrush RarityGlowBottom
    {
        get
        {
            // Fixed pale violet glow, independent of rarity
            return new LinearGradientBrush
            {
                StartPoint = new Avalonia.RelativePoint(0, 0, Avalonia.RelativeUnit.Relative),
                EndPoint = new Avalonia.RelativePoint(0, 1, Avalonia.RelativeUnit.Relative),
                GradientStops =
                {
                    new GradientStop(Color.FromArgb(0, 108, 92, 231), 0),
                    new GradientStop(Color.FromArgb(0, 108, 92, 231), 0.55),
                    new GradientStop(Color.FromArgb(25, 108, 92, 231), 0.8),
                    new GradientStop(Color.FromArgb(50, 108, 92, 231), 1),
                }
            };
        }
    }

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

    /// <summary>DamageTypes without "None" — weapons must have a real damage type.</summary>
    public static string[] DamageTypeOptions => ParaTool.Core.Schema.BoostMapping.DamageTypes[1..];

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
        set
        {
            if (Artifact.Rarity == value) return;
            Artifact.Rarity = value;
            // Auto-recalculate price based on rarity + pool
            var pool = Artifact.LootPool ?? "Armor";
            var cat = ParaTool.Core.Models.PricingGrid.GetSlotCategory(pool);
            Artifact.ValueOverride = ParaTool.Core.Models.PricingGrid.GetPrice(cat, value);
            MarkDirty();
            OnPropertyChanged();
            OnPropertyChanged(nameof(RarityColor));
            OnPropertyChanged(nameof(RarityGradient));
            OnPropertyChanged(nameof(PreviewRarityText));
            OnPropertyChanged(nameof(EditValueOverride));
        }
    }

    public string EditUsingBase => Artifact.UsingBase;

    public string EditPool
    {
        get => Artifact.LootPool ?? "";
        set
        {
            Artifact.LootPool = string.IsNullOrWhiteSpace(value) ? null : value;
            // Auto-recalculate price
            var cat = ParaTool.Core.Models.PricingGrid.GetSlotCategory(value ?? "Armor");
            Artifact.ValueOverride = ParaTool.Core.Models.PricingGrid.GetPrice(cat, Artifact.Rarity);
            MarkDirty();
            OnPropertyChanged();
            OnPropertyChanged(nameof(EditValueOverride));
        }
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

    public string EditDamageType
    {
        get => Artifact.DamageType ?? "";
        set { Artifact.DamageType = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); }
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

    public string EditBoostsOnEquipMainHand
    {
        get => Artifact.BoostsOnEquipMainHand ?? "";
        set { Artifact.BoostsOnEquipMainHand = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); NotifyPreviewDebounced(nameof(HasMainHandBoosts), nameof(PreviewMainHandText)); }
    }

    public string EditBoostsOnEquipOffHand
    {
        get => Artifact.BoostsOnEquipOffHand ?? "";
        set { Artifact.BoostsOnEquipOffHand = string.IsNullOrWhiteSpace(value) ? null : value; MarkDirty(); OnPropertyChanged(); NotifyPreviewDebounced(nameof(HasOffHandBoosts), nameof(PreviewOffHandText)); }
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
        set { Artifact.Boosts = value ?? ""; MarkDirty(); OnPropertyChanged(); NotifyPreviewDebounced(nameof(HasBoosts), nameof(PreviewBoostsText)); }
    }

    private string TranslateBoostKey(string key)
    {
        if (key.StartsWith("stat."))
        {
            var statId = key[5..];
            var lang = Localization.Loc.Instance.Lang;
            var name = Controls.SearchPickerChip.ResolveStatDisplayName(statId, lang,
                Controls.BoostBlocksEditor.GlobalResolver, Controls.BoostBlocksEditor.GlobalLocaService);
            return name ?? key;
        }
        return Localization.Loc.Instance[key];
    }

    public string PreviewBoostsText => ParaTool.Core.Schema.BoostMapping.FormatBoostsForPreview(
        Artifact.Boosts, TranslateBoostKey);

    public string PreviewMainHandText => ParaTool.Core.Schema.BoostMapping.FormatBoostsForPreview(
        Artifact.BoostsOnEquipMainHand ?? "", TranslateBoostKey);
    public bool HasMainHandBoosts => !string.IsNullOrEmpty(Artifact.BoostsOnEquipMainHand);

    public string PreviewOffHandText => ParaTool.Core.Schema.BoostMapping.FormatBoostsForPreview(
        Artifact.BoostsOnEquipOffHand ?? "", TranslateBoostKey);
    public bool HasOffHandBoosts => !string.IsNullOrEmpty(Artifact.BoostsOnEquipOffHand);

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
        set { SetLangValue(Artifact.DisplayName, value); MarkDirty(); OnPropertyChanged(); NotifyPreviewDebounced(nameof(DisplayLabel), nameof(PreviewName)); }
    }

    public string EditDescription
    {
        get => GetLangValue(Artifact.Description);
        set { SetLangValue(Artifact.Description, value); MarkDirty(); OnPropertyChanged(); NotifyPreviewDebounced(nameof(PreviewDescription), nameof(HasDescription)); }
    }

    // === Passives (editable list) ===

    public ObservableCollection<PassiveVM> PassiveVMs { get; } = [];

    public void AddExistingPassive(string passiveName, Core.Parsing.StatsResolver? resolver,
        Core.Services.LocaService? locaService = null)
    {
        // Check if already added
        if (PassiveVMs.Any(p => p.Name.Equals(passiveName, StringComparison.OrdinalIgnoreCase)))
            return;

        var passive = new Core.Artifacts.PassiveDefinition
        {
            Name = passiveName,
            UsingBase = passiveName,
            Properties = "Highlighted",
        };

        var lang = Localization.Loc.Instance.Lang;

        // Resolve fields from stats
        if (resolver != null)
        {
            var fields = resolver.ResolveAll(passiveName);
            if (fields.TryGetValue("Properties", out var props)) passive.Properties = props;
            if (fields.TryGetValue("Boosts", out var boosts)) passive.Boosts = boosts;
            if (fields.TryGetValue("BoostContext", out var ctx)) passive.BoostContext = ctx;
            if (fields.TryGetValue("BoostConditions", out var cond)) passive.BoostConditions = cond;
            if (fields.TryGetValue("StatsFunctors", out var sf)) passive.StatsFunctors = sf;
            if (fields.TryGetValue("StatsFunctorContext", out var sfc)) passive.StatsFunctorContext = sfc;
            if (fields.TryGetValue("Conditions", out var conditions)) passive.Conditions = conditions;
            if (fields.TryGetValue("DescriptionParams", out var dp)) passive.DescriptionParams = dp;
            if (fields.TryGetValue("Icon", out var icon)) passive.Icon = icon;

            // Resolve DisplayName from loca handle
            if (fields.TryGetValue("DisplayName", out var dnHandleRaw))
            {
                var dnHandle = Core.Localization.HandleGenerator.Parse(dnHandleRaw).handle;
                passive.DisplayNameHandle = dnHandle;
                if (locaService != null)
                {
                    var resolved = locaService.ResolveHandle(dnHandle, lang);
                    if (resolved != null) passive.DisplayName[lang] = BbCode.FromBg3Xml(resolved);
                    if (lang != "en") { var en = locaService.ResolveHandle(dnHandle, "en"); if (en != null) passive.DisplayName["en"] = BbCode.FromBg3Xml(en); }
                }
                // Fallback: vanilla loca
                if (string.IsNullOrEmpty(passive.DisplayName.GetValueOrDefault(lang)))
                {
                    var vn = Core.Services.VanillaLocaService.GetDisplayName(passiveName, lang);
                    if (vn != null) passive.DisplayName[lang] = vn;
                }
            }

            // Resolve Description from loca handle
            if (fields.TryGetValue("Description", out var descHandleRaw))
            {
                var descHandle = Core.Localization.HandleGenerator.Parse(descHandleRaw).handle;
                passive.DescriptionHandle = descHandle;
                if (locaService != null)
                {
                    var resolved = locaService.ResolveHandle(descHandle, lang);
                    if (resolved != null) passive.Description[lang] = BbCode.FromBg3Xml(resolved);
                    if (lang != "en") { var en = locaService.ResolveHandle(descHandle, "en"); if (en != null) passive.Description["en"] = BbCode.FromBg3Xml(en); }
                }
                // Fallback: vanilla loca
                if (string.IsNullOrEmpty(passive.Description.GetValueOrDefault(lang)))
                {
                    var vd = Core.Services.VanillaLocaService.GetDescription(passiveName, lang);
                    if (vd != null) passive.Description[lang] = vd;
                }
            }
        }

        Artifact.Passives.Add(passive);
        PassiveVMs.Add(new PassiveVM(passive, this));
        IsDirty = true;
        OnPropertyChanged(nameof(HasPassives));
    }

    public void RemovePassive(PassiveVM pvm)
    {
        Artifact.Passives.Remove(pvm.Passive);
        PassiveVMs.Remove(pvm);
        IsDirty = true;
        OnPropertyChanged(nameof(HasPassives));
    }

    public void AddNewPassive()
    {
        var passive = new Core.Artifacts.PassiveDefinition
        {
            Name = "Passive_New_" + Guid.NewGuid().ToString("N")[..6],
            Properties = "Highlighted",
        };
        Artifact.Passives.Add(passive);
        PassiveVMs.Add(new PassiveVM(passive, this));
        IsDirty = true;
        OnPropertyChanged(nameof(HasPassives));
    }

    public void LoadPassivesFromArtifact()
    {
        PassiveVMs.Clear();
        foreach (var p in Artifact.Passives ?? [])
        {
            if (p != null)
                PassiveVMs.Add(new PassiveVM(p, this));
        }
        OnPropertyChanged(nameof(HasPassives));
    }

    // === Preview ===

    public string PreviewName => !string.IsNullOrEmpty(GetPreviewName()) ? GetPreviewName() : Artifact.StatId;
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

    // Debounce preview updates to avoid UI lag on every keystroke
    private DispatcherTimer? _previewDebounce;
    private readonly HashSet<string> _pendingNotifications = new();

    private void NotifyPreviewDebounced(params string[] propertyNames)
    {
        foreach (var name in propertyNames)
            _pendingNotifications.Add(name);

        _previewDebounce?.Stop();
        _previewDebounce ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(300) };
        _previewDebounce.Tick -= FlushPreviewNotifications;
        _previewDebounce.Tick += FlushPreviewNotifications;
        _previewDebounce.Start();
    }

    private void FlushPreviewNotifications(object? sender, EventArgs e)
    {
        _previewDebounce?.Stop();
        foreach (var name in _pendingNotifications)
            OnPropertyChanged(name);
        _pendingNotifications.Clear();
    }

    private string GetLocalizedName()
    {
        var lang = Loc.Instance.Lang;
        if (Artifact.DisplayName.TryGetValue(lang, out var n) && !string.IsNullOrEmpty(n)) return n;
        if (Artifact.DisplayName.TryGetValue("en", out var en) && !string.IsNullOrEmpty(en)) return en;
        return "";
    }

    /// <summary>Name for preview — uses editing language (preview selector), not UI language.</summary>
    private string GetPreviewName()
    {
        var lang = EditLang;
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
        OnPropertyChanged(nameof(RarityColor)); OnPropertyChanged(nameof(RarityGradient)); OnPropertyChanged(nameof(RarityGlowBottom));
        OnPropertyChanged(nameof(PreviewName));
        OnPropertyChanged(nameof(PreviewRarityText));
        OnPropertyChanged(nameof(PreviewSlot));
        OnPropertyChanged(nameof(PreviewSubtitle));
        OnPropertyChanged(nameof(PreviewDescription));
        OnPropertyChanged(nameof(HasDescription));
        OnPropertyChanged(nameof(HasBoosts));
        OnPropertyChanged(nameof(PreviewBoostsText));
        OnPropertyChanged(nameof(HasMainHandBoosts));
        OnPropertyChanged(nameof(PreviewMainHandText));
        OnPropertyChanged(nameof(HasOffHandBoosts));
        OnPropertyChanged(nameof(PreviewOffHandText));
        OnPropertyChanged(nameof(HasPassives));
        OnPropertyChanged(nameof(EditDisplayName));
        OnPropertyChanged(nameof(EditDescription));
        OnPropertyChanged(nameof(EditBoosts));
        OnPropertyChanged(nameof(EditPassivesOnEquip));
        OnPropertyChanged(nameof(EditStatusOnEquip));
        OnPropertyChanged(nameof(EditSpellsOnEquip));
        LoadPassivesFromArtifact();
    }

    private static Color GetRarityColor(string rarity) => Themes.ThemeBrushes.GetRarityColor(rarity);

    private static SolidColorBrush GetRarityBrush(string rarity) => Themes.ThemeBrushes.GetRarity(rarity);
}

/// <summary>
/// Wraps a single PassiveDefinition for editing in the UI.
/// </summary>
public partial class PassiveVM : ObservableObject
{
    public PassiveDefinition Passive { get; }
    private readonly ArtifactItemVM _parent;

    private string EditLang => _parent.GetEditingLang?.Invoke() ?? Loc.Instance.Lang;

    public static string[] PassivePropertyOptions { get; } =
    [
        "Highlighted", "IsHidden", "IsToggled", "ToggledDefaultOn",
        "OncePerTurn", "OncePerAttack", "OncePerShortRest", "OncePerLongRest", "OncePerCombat",
        "DisplayBoostInTooltip", "Temporary", "ToggledDefaultAddToHotbar", "ToggleForParty"
    ];

    public static string[] TriggerOptions { get; } = ParaTool.Core.Schema.BoostMapping.TriggerEvents.Keys.ToArray();
    public static string[] TriggerLabels => ParaTool.Core.Schema.BoostMapping.TriggerEvents.Values
        .Select(v =>
        {
            var parts = v.Split('/');
            return Localization.Loc.Instance.Lang == "ru" && parts.Length > 1
                ? parts[1].Trim() : parts[0].Trim();
        }).ToArray();

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

    /// <summary>True if passive has localized content to show in preview.</summary>
    public bool HasVisibleLoca => !string.IsNullOrWhiteSpace(EditDisplayName) || !string.IsNullOrWhiteSpace(EditDescription);

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
                var transliterated = Transliterator.ToLatin((value ?? "").Trim());
                var cleaned = System.Text.RegularExpressions.Regex.Replace(transliterated, @"[^a-zA-Z0-9\s]", "");
                var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                Passive.Name = parts.Length > 0
                    ? "Passive_" + string.Join("_", parts.Select(p => char.ToUpper(p[0]) + (p.Length > 1 ? p[1..] : "")))
                    : "Passive_" + Guid.NewGuid().ToString("N")[..8];
            }
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(HasVisibleLoca));
            OnPropertyChanged();
        }
    }

    public string EditDescription
    {
        get => GetLang(Passive.Description);
        set { SetLang(Passive.Description, value); _parent.IsDirty = true; OnPropertyChanged(nameof(HasVisibleLoca)); OnPropertyChanged(); }
    }

    public string EditDescriptionParams
    {
        get => Passive.DescriptionParams;
        set { Passive.DescriptionParams = value ?? ""; _parent.IsDirty = true; OnPropertyChanged(); }
    }

    public string EditProperties
    {
        get => Passive.Properties;
        set
        {
            Passive.Properties = value ?? "";
            _parent.IsDirty = true;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasVisibleLoca));
        }
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
