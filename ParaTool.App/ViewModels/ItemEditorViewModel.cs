using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Localization;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;
using ParaTool.Core.Services;

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

    // Profile state
    [ObservableProperty] private ObservableCollection<string> _profileNames = new();
    [ObservableProperty] private string? _selectedProfile;
    [ObservableProperty] private string? _newProfileName;
    [ObservableProperty] private bool _hasMissingItems;
    [ObservableProperty] private string? _missingItemsText;
    [ObservableProperty] private bool _showProfileToast;
    [ObservableProperty] private string? _profileToastText;
    private bool _suppressProfileLoad;

    public string? AmpPakPath { get; set; }

    public int TotalItems => Mods.Sum(m => m.TotalItems);
    public int TotalEnabled => Mods.Sum(m => m.EnabledItems);
    public string AllModsCount => $"{TotalEnabled}/{TotalItems}";
    public string ModsCountText => Loc.Instance.ModsFoundInfo(Mods.Count);

    public bool ShowPatchButton => !IsPatching && !PatchSuccess && PatchError == null;

    public ItemEditorViewModel()
    {
        Loc.Instance.PropertyChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(ModsCountText));
            if (HasMissingItems)
                OnPropertyChanged(nameof(MissingItemsText));
        };
        RefreshProfileList();
    }

    partial void OnIsPatchingChanged(bool value) => OnPropertyChanged(nameof(ShowPatchButton));
    partial void OnPatchSuccessChanged(bool value) => OnPropertyChanged(nameof(ShowPatchButton));
    partial void OnPatchErrorChanged(string? value) => OnPropertyChanged(nameof(ShowPatchButton));

    partial void OnSelectedProfileChanged(string? value)
    {
        if (_suppressProfileLoad || value == null) return;
        var data = ProfileService.LoadProfile(value);
        if (data != null)
            ApplyProfileData(data);
    }

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

        var modInfos = Mods.Where(m => !m.IsAmp).Select(m => m.ModInfo).ToList();
        var ampModInfo = Mods.FirstOrDefault(m => m.IsAmp)?.ModInfo;
        var patcher = new AmpPatcher();

        var progress = new Progress<PatchProgress>(p =>
        {
            PatchPercent = p.Percent;
            PatchStatus = p.Stage;
        });

        var result = await patcher.PatchAsync(AmpPakPath, modInfos, ampModInfo, progress);

        if (result.Success)
        {
            PatchSuccess = true;
            PatchSuccessMessage = Loc.Instance.PatchSuccessMessage(result.ItemsPatched);

            // Auto-save last session (with mod-level enabled)
            try
            {
                var allModInfos = Mods.Select(m => m.ModInfo).ToList();
                var sessionData = ProfileService.CaptureState(allModInfos);
                foreach (var mod in Mods)
                    if (sessionData.Mods.TryGetValue(mod.ModInfo.UUID, out var sel))
                        sel.Enabled = mod.Enabled;
                var sessionPath = ProfileService.GetLastSessionPath();
                Directory.CreateDirectory(Path.GetDirectoryName(sessionPath)!);
                File.WriteAllText(sessionPath, System.Text.Json.JsonSerializer.Serialize(sessionData,
                    new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
            }
            catch { /* ignore */ }
        }
        else
        {
            PatchError = result.Error;
            PatchStatus = $"{Loc.Instance.PatchErrorLabel} {result.Error}";
        }

        IsPatching = false;
    }

    // === Profile methods ===

    public void RefreshProfileList()
    {
        var profiles = ProfileService.ListProfiles();
        ProfileNames = new ObservableCollection<string>(profiles);
    }

    public void ApplyProfileData(ProfileData profile)
    {
        // Sync themes from VMs to entries first
        foreach (var mod in Mods)
            foreach (var item in mod.Items)
                item.SyncThemesToEntry();

        var modInfos = Mods.Select(m => m.ModInfo).ToList();
        var result = ProfileService.ApplyProfile(profile, modInfos);

        // Sync entries back to VMs
        foreach (var mod in Mods)
            foreach (var item in mod.Items)
                item.SyncFromEntry();

        // Apply mod-level enabled from profile (without cascade to items)
        foreach (var mod in Mods)
        {
            if (profile.Mods.TryGetValue(mod.ModInfo.UUID, out var sel))
                mod.SetEnabledSilent(sel.Enabled);
            else
                mod.SyncEnabledFromItems();
        }
        RefreshCounts();

        // Handle missing items
        if (result.MissingItems.Count > 0)
        {
            var grouped = result.MissingItems
                .GroupBy(m => m.ModName)
                .Select(g => $"  - {g.Key}: {string.Join(", ", g.Select(i => i.StatId))}");
            MissingItemsText = $"{Loc.Instance.MissingItemsWarning}\n{string.Join("\n", grouped)}";
            HasMissingItems = true;
        }
        else
        {
            HasMissingItems = false;
            MissingItemsText = null;
        }
    }

    [RelayCommand]
    private void DismissMissingItems()
    {
        HasMissingItems = false;
        MissingItemsText = null;
    }

    [RelayCommand]
    private void LoadProfile()
    {
        if (SelectedProfile == null) return;
        var data = ProfileService.LoadProfile(SelectedProfile);
        if (data != null)
            ApplyProfileData(data);
    }

    [RelayCommand]
    private void DeleteProfile()
    {
        if (SelectedProfile == null) return;
        ProfileService.DeleteProfile(SelectedProfile);
        RefreshProfileList();
        SelectedProfile = null;
    }

    public void SaveProfileByName(string name)
    {
        // Sync themes from VMs to entries
        foreach (var mod in Mods)
            foreach (var item in mod.Items)
                item.SyncThemesToEntry();

        var modInfos = Mods.Select(m => m.ModInfo).ToList();
        // Save with mod-level enabled
        var data = ProfileService.CaptureState(modInfos);
        foreach (var mod in Mods)
            if (data.Mods.TryGetValue(mod.ModInfo.UUID, out var sel))
                sel.Enabled = mod.Enabled;
        var path = ProfileService.GetProfilePath(name);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(data,
            new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));

        _suppressProfileLoad = true;
        RefreshProfileList();
        SelectedProfile = name;
        _suppressProfileLoad = false;
    }

    [RelayCommand]
    private void SaveProfile()
    {
        var newName = NewProfileName?.Trim();
        string saveName;

        if (!string.IsNullOrEmpty(newName))
        {
            saveName = newName;
            NewProfileName = "";
        }
        else if (SelectedProfile != null)
        {
            saveName = SelectedProfile;
        }
        else
        {
            return;
        }

        SaveProfileByName(saveName);
        ShowProfileToastMessage(Loc.Instance.ProfileSaved);
    }

    private async void ShowProfileToastMessage(string message)
    {
        ProfileToastText = message;
        ShowProfileToast = true;
        await Task.Delay(3000);
        ShowProfileToast = false;
    }
}
