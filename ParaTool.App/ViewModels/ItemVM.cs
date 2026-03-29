using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.App.Localization;
using ParaTool.Core.Localization;
using ParaTool.App.Themes;
using ParaTool.Core.Models;
using ParaTool.Core.Services;

namespace ParaTool.App.ViewModels;

/// <summary>
/// Key-value pair for ComboBox: Value is the internal key, Display is the localized name.
/// Implements INotifyPropertyChanged so ComboBox ItemTemplate updates when Display changes.
/// </summary>
public partial class LabeledOption : ObservableObject
{
    public string Value { get; }

    [ObservableProperty] private string _display;

    public LabeledOption(string value, string display)
    {
        Value = value;
        _display = display;
    }

    public override string ToString() => Display;
    public override bool Equals(object? obj) => obj is LabeledOption o && o.Value == Value;
    public override int GetHashCode() => Value.GetHashCode();
}

public partial class ItemVM : ObservableObject
{
    private readonly ItemEntry _entry;

    private readonly LocaService? _locaService;

    public ItemVM(ItemEntry entry, LocaService? locaService = null)
    {
        _entry = entry;
        _locaService = locaService;
        _enabled = entry.Enabled;
        _selectedPool = PoolOptions.First(o => o.Value == entry.EffectivePool);
        _selectedRarity = RarityOptions.First(o => o.Value == entry.EffectiveRarity);
        _selectedThemes = new ObservableCollection<string>(entry.EffectiveThemes);

        Loc.Instance.PropertyChanged += (_, _) => OnLanguageChanged();
    }

    private void OnLanguageChanged()
    {
        foreach (var o in PoolOptions) o.Display = Loc.Instance.PoolName(o.Value);
        foreach (var o in RarityOptions) o.Display = Loc.Instance.RarityName(o.Value);

        OnPropertyChanged(nameof(ItemLabel));
        OnPropertyChanged(nameof(ItemLabelForeground));
        OnPropertyChanged(nameof(ThemesDisplay));
    }

    public string StatId => _entry.StatId;
    public string StatType => _entry.StatType;
    public string? DisplayName => _entry.DisplayName;
    public bool HasArtifactOverride => _entry.HasArtifactOverride;
    public string ItemLabel
    {
        get
        {
            var lang = Loc.Instance.Lang;
            string name;
            if (_locaService != null && !string.IsNullOrEmpty(_entry.DisplayNameHandle))
            {
                var resolved = _locaService.ResolveHandle(_entry.DisplayNameHandle, lang);
                name = resolved != null ? BbCode.FromBg3Xml(resolved) : null!;
            }
            else
            {
                name = null!;
            }
            name ??= VanillaLocaService.GetDisplayName(_entry.StatId, lang)
                ?? _entry.DisplayName ?? _entry.StatId;

            return _entry.HasArtifactOverride ? $"\U0001f527 {name}" : name;
        }
    }
    public bool HasDisplayName => _entry.DisplayName != null
        || VanillaLocaService.GetDisplayName(_entry.StatId, "en") != null;
    public string DetectedPool => _entry.DetectedPool ?? "?";
    public string DetectedRarity => _entry.DetectedRarity ?? "?";
    public ItemEntry Entry => _entry;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private LabeledOption _selectedPool;
    [ObservableProperty] private LabeledOption _selectedRarity;
    [ObservableProperty] private ObservableCollection<string> _selectedThemes;

    public string ThemesDisplay => SelectedThemes.Count == 0
        ? "---"
        : string.Join(", ", SelectedThemes.Select(t => Loc.Instance.ThemeName(t)));

    partial void OnEnabledChanged(bool value) => _entry.Enabled = value;
    partial void OnSelectedPoolChanged(LabeledOption value) => _entry.UserPool = value.Value;
    partial void OnSelectedRarityChanged(LabeledOption value)
    {
        _entry.UserRarity = value.Value;
        OnPropertyChanged(nameof(RarityColor));
    }

    public IBrush ItemLabelForeground => _entry.HasArtifactOverride
        ? ThemeBrushes.Get("WarningBrush")
        : ThemeBrushes.TextSecondary;

    public IBrush RarityColor => ThemeBrushes.GetRarity(SelectedRarity.Value);

    public void SyncThemesToEntry()
    {
        _entry.UserThemes = new List<string>(SelectedThemes);
    }

    public void NotifyThemesChanged()
    {
        OnPropertyChanged(nameof(ThemesDisplay));
    }

    public void ToggleTheme(string theme)
    {
        if (SelectedThemes.Contains(theme))
            SelectedThemes.Remove(theme);
        else
            SelectedThemes.Add(theme);
        NotifyThemesChanged();
    }

    /// <summary>
    /// Sync VM state from the underlying ItemEntry (after profile apply).
    /// </summary>
    public void SyncFromEntry()
    {
        Enabled = _entry.Enabled;
        SelectedPool = PoolOptions.FirstOrDefault(o => o.Value == _entry.EffectivePool)
                       ?? PoolOptions[0];
        SelectedRarity = RarityOptions.FirstOrDefault(o => o.Value == _entry.EffectiveRarity)
                         ?? RarityOptions[0];
        SelectedThemes = new ObservableCollection<string>(_entry.EffectiveThemes);
        NotifyThemesChanged();
    }

    public void NotifyArtifactOverrideChanged()
    {
        OnPropertyChanged(nameof(HasArtifactOverride));
        OnPropertyChanged(nameof(ItemLabel));
        OnPropertyChanged(nameof(ItemLabelForeground));
    }

    // === Option lists (shared across all instances) ===

    private static readonly string[] _poolKeys =
    [
        "Clothes", "Armor", "Shields", "Hats", "Cloaks",
        "Gloves", "Boots", "Amulets", "Rings",
        "Weapons", "Weapons_1H", "Weapons_2H"
    ];

    private static readonly string[] _rarityKeys =
    [
        "Common", "Uncommon", "Rare", "VeryRare", "Legendary"
    ];

    public static LabeledOption[] PoolOptions { get; } =
        _poolKeys.Select(k => new LabeledOption(k, Loc.Instance.PoolName(k))).ToArray();

    public static LabeledOption[] RarityOptions { get; } =
        _rarityKeys.Select(k => new LabeledOption(k, Loc.Instance.RarityName(k))).ToArray();

    public static string[] AvailableThemes =>
    [
        "Swamp", "Aquatic", "Shadowfell", "Arcane", "Celestial",
        "Nature", "Destructive", "War", "Psionic", "Primal"
    ];
}
