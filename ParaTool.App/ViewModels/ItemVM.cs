using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.App.Localization;
using ParaTool.Core.Models;

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

    public ItemVM(ItemEntry entry)
    {
        _entry = entry;
        _enabled = entry.Enabled;
        _selectedPool = PoolOptions.First(o => o.Value == entry.EffectivePool);
        _selectedRarity = RarityOptions.First(o => o.Value == entry.EffectiveRarity);
        _selectedThemes = new ObservableCollection<string>(entry.UserThemes);

        Loc.Instance.PropertyChanged += (_, _) => OnLanguageChanged();
    }

    private void OnLanguageChanged()
    {
        // Update Display on each LabeledOption — triggers PropertyChanged via ObservableObject
        foreach (var o in PoolOptions) o.Display = Loc.Instance.PoolName(o.Value);
        foreach (var o in RarityOptions) o.Display = Loc.Instance.RarityName(o.Value);

        OnPropertyChanged(nameof(ThemesDisplay));
    }

    public string StatId => _entry.StatId;
    public string StatType => _entry.StatType;
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

    public IBrush RarityColor => SelectedRarity.Value switch
    {
        "Uncommon" => new SolidColorBrush(Color.Parse("#2ECC71")),
        "Rare" => new SolidColorBrush(Color.Parse("#3498DB")),
        "VeryRare" => new SolidColorBrush(Color.Parse("#9B59B6")),
        "Legendary" => new SolidColorBrush(Color.Parse("#C8A96E")),
        _ => new SolidColorBrush(Color.Parse("#8A8494")),
    };

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

    // === Option lists (shared across all instances) ===

    private static readonly string[] _poolKeys =
    [
        "Clothes", "Armor", "Shields", "Hats", "Cloaks",
        "Gloves", "Boots", "Amulets", "Rings",
        "Weapons", "Weapons_1H", "Weapons_2H"
    ];

    private static readonly string[] _rarityKeys =
    [
        "Uncommon", "Rare", "VeryRare", "Legendary"
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
