using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.Core.Models;

namespace ParaTool.App.ViewModels;

public partial class ItemVM : ObservableObject
{
    private readonly ItemEntry _entry;

    public ItemVM(ItemEntry entry)
    {
        _entry = entry;
        _enabled = entry.Enabled;
        _selectedPool = entry.EffectivePool;
        _selectedRarity = entry.EffectiveRarity;
    }

    public string StatId => _entry.StatId;
    public string StatType => _entry.StatType;
    public string DetectedPool => _entry.DetectedPool ?? "?";
    public string DetectedRarity => _entry.DetectedRarity ?? "?";
    public ItemEntry Entry => _entry;

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private string _selectedPool;
    [ObservableProperty] private string _selectedRarity;

    public List<string> SelectedThemes { get; set; } = new();

    partial void OnEnabledChanged(bool value) => _entry.Enabled = value;
    partial void OnSelectedPoolChanged(string value) => _entry.UserPool = value;
    partial void OnSelectedRarityChanged(string value) => _entry.UserRarity = value;

    public static string[] AvailablePools => new[]
    {
        "Clothes", "Armor", "Shields", "Hats", "Cloaks",
        "Gloves", "Boots", "Amulets", "Rings",
        "Weapons", "Weapons_1H", "Weapons_2H"
    };

    public static string[] AvailableRarities => new[]
    {
        "Uncommon", "Rare", "VeryRare", "Legendary"
    };

    public static string[] AvailableThemes => new[]
    {
        "Swamp", "Aquatic", "Shadowfell", "Arcane", "Celestial",
        "Nature", "Destructive", "War", "Psionic", "Primal"
    };

    public void SyncThemesToEntry()
    {
        _entry.UserThemes = new List<string>(SelectedThemes);
    }
}
