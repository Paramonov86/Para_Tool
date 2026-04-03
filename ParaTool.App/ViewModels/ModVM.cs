using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.App.Themes;
using ParaTool.Core.Models;
using ParaTool.Core.Services;

namespace ParaTool.App.ViewModels;

public partial class ModVM : ObservableObject
{
    public ModVM(ModInfo mod, LocaService? locaService = null)
    {
        ModInfo = mod;
        Name = mod.Name;
        Items = new ObservableCollection<ItemVM>(
            mod.Items.Select(i => new ItemVM(i, locaService)));
        _enabled = true;
    }

    public ModInfo ModInfo { get; }
    public string Name { get; }
    public bool IsAmp => ModInfo.IsAmp;
    public IBrush ModBackground => IsAmp
        ? new SolidColorBrush(Themes.ThemeBrushes.Get("GoldBrush").Color, 0.2)
        : Themes.ThemeBrushes.PanelBg;
    public ObservableCollection<ItemVM> Items { get; }

    [ObservableProperty] private bool _enabled;
    [ObservableProperty] private bool _isExpanded;

    public int TotalItems => Items.Count;
    public int EnabledItems => Items.Count(i => i.Enabled);
    public int VisibleItems => Items.Count(i => i.IsVisibleInFilter);
    public bool HasVisibleItems => Items.Any(i => i.IsVisibleInFilter);
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

    public void RefreshFilterState()
    {
        OnPropertyChanged(nameof(VisibleItems));
        OnPropertyChanged(nameof(HasVisibleItems));
    }

    /// <summary>
    /// Set Enabled without triggering OnEnabledChanged cascade to items.
    /// </summary>
    public void SetEnabledSilent(bool value)
    {
        if (_enabled != value)
        {
            _enabled = value;
            OnPropertyChanged(nameof(Enabled));
        }
        RefreshCounts();
    }

    /// <summary>
    /// Set Enabled from item states without triggering OnEnabledChanged cascade.
    /// </summary>
    public void SyncEnabledFromItems()
    {
        SetEnabledSilent(Items.Any(i => i.Enabled));
    }
}
