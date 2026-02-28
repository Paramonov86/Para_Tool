using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Localization;
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
    [ObservableProperty] private string? _patchSuccessMessage;

    public string? AmpPakPath { get; set; }

    public int TotalItems => Mods.Sum(m => m.TotalItems);
    public int TotalEnabled => Mods.Sum(m => m.EnabledItems);
    public string AllModsCount => $"{TotalEnabled}/{TotalItems}";
    public string ModsCountText => Loc.Instance.ModsFoundInfo(Mods.Count);

    public bool ShowPatchButton => !IsPatching && !PatchSuccess && PatchError == null;

    public ItemEditorViewModel()
    {
        Loc.Instance.PropertyChanged += (_, _) => OnPropertyChanged(nameof(ModsCountText));
    }

    partial void OnIsPatchingChanged(bool value) => OnPropertyChanged(nameof(ShowPatchButton));
    partial void OnPatchSuccessChanged(bool value) => OnPropertyChanged(nameof(ShowPatchButton));
    partial void OnPatchErrorChanged(string? value) => OnPropertyChanged(nameof(ShowPatchButton));

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
            PatchSuccessMessage = Loc.Instance.PatchSuccessMessage(result.ItemsPatched);
        }
        else
        {
            PatchError = result.Error;
            PatchStatus = $"{Loc.Instance.PatchErrorLabel} {result.Error}";
        }

        IsPatching = false;
    }
}
