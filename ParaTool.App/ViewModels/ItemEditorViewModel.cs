using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;

namespace ParaTool.App.ViewModels;

public partial class ItemEditorViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<ModVM> _mods = new();
    [ObservableProperty] private bool _isPatching;
    [ObservableProperty] private int _patchPercent;
    [ObservableProperty] private string? _patchStatus;
    [ObservableProperty] private bool _patchSuccess;
    [ObservableProperty] private string? _patchError;
    [ObservableProperty] private bool _allEnabled = true;

    public string? AmpPakPath { get; set; }

    public int TotalItems => Mods.Sum(m => m.TotalItems);
    public int TotalEnabled => Mods.Sum(m => m.EnabledItems);
    public string AllModsCount => $"{TotalEnabled}/{TotalItems}";

    partial void OnAllEnabledChanged(bool value)
    {
        foreach (var mod in Mods)
            mod.Enabled = value;
        RefreshCounts();
    }

    public void RefreshCounts()
    {
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(TotalEnabled));
        OnPropertyChanged(nameof(AllModsCount));
    }

    [RelayCommand]
    private void ToggleExpand(ModVM mod)
    {
        mod.IsExpanded = !mod.IsExpanded;
    }

    [RelayCommand]
    private async Task PatchAsync()
    {
        if (AmpPakPath == null) return;

        IsPatching = true;
        PatchSuccess = false;
        PatchError = null;

        // Sync themes from VMs to entries
        foreach (var mod in Mods)
            foreach (var item in mod.Items)
                item.SyncThemesToEntry();

        var modInfos = Mods.Select(m => m.ModInfo).ToList();
        var patcher = new AmpPatcher();

        var progress = new Progress<PatchProgress>(p =>
        {
            PatchPercent = p.Percent;
            PatchStatus = p.Stage;
        });

        var result = await patcher.PatchAsync(AmpPakPath, modInfos, progress);

        if (result.Success)
        {
            PatchSuccess = true;
            PatchStatus = $"OK! {result.ItemsPatched} items patched. Backup: {result.BackupPath}";
        }
        else
        {
            PatchError = result.Error;
        }

        IsPatching = false;
    }
}
