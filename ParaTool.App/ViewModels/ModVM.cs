using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.Core.Models;

namespace ParaTool.App.ViewModels;

public partial class ModVM : ObservableObject
{
    public ModVM(ModInfo mod)
    {
        ModInfo = mod;
        Name = mod.Name;
        Items = new ObservableCollection<ItemVM>(
            mod.Items.Select(i => new ItemVM(i)));
        _enabled = true;
    }

    public ModInfo ModInfo { get; }
    public string Name { get; }
    public ObservableCollection<ItemVM> Items { get; }

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isExpanded;

    public int TotalItems => Items.Count;
    public int EnabledItems => Items.Count(i => i.Enabled);
    public string CountDisplay => $"{EnabledItems}/{TotalItems}";

    partial void OnEnabledChanged(bool value)
    {
        foreach (var item in Items)
            item.Enabled = value;
        OnPropertyChanged(nameof(EnabledItems));
        OnPropertyChanged(nameof(CountDisplay));
    }

    public void RefreshCounts()
    {
        OnPropertyChanged(nameof(EnabledItems));
        OnPropertyChanged(nameof(CountDisplay));
    }
}
