using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Localization;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;
using ParaTool.Core.Services;

namespace ParaTool.App.ViewModels;

public enum SortMode { Name, Rarity, Theme, Slot }

public partial class ItemEditorViewModel : ViewModelBase
{
    [ObservableProperty] private ObservableCollection<ModVM> _mods = new();
    [ObservableProperty] private SortMode _currentSort = SortMode.Name;
    [ObservableProperty] private SortMode _secondarySort = SortMode.Name;
    [ObservableProperty] private bool _sortDescending;
    [ObservableProperty] private bool _isPatching;
    [ObservableProperty] private int _patchPercent;
    [ObservableProperty] private string? _patchStatus;
    [ObservableProperty] private bool _patchSuccess;
    [ObservableProperty] private string? _patchError;
    [ObservableProperty] private ObservableCollection<string> _patchWarnings = new();
    [ObservableProperty] private bool _allEnabled = true;
    [ObservableProperty] private string? _patchSuccessMessage;

    // Filter state
    [ObservableProperty] private string? _searchText;
    [ObservableProperty] private bool _hideDisabled;

    // Theme filter: themes NOT in this set are hidden
    public HashSet<string> HiddenThemes { get; } = new(StringComparer.OrdinalIgnoreCase);

    // Profile state
    [ObservableProperty] private ObservableCollection<string> _profileNames = new();
    [ObservableProperty] private string? _selectedProfile;
    [ObservableProperty] private string? _newProfileName;
    [ObservableProperty] private bool _hasMissingItems;
    [ObservableProperty] private string? _missingItemsText;
    [ObservableProperty] private bool _showProfileToast;
    [ObservableProperty] private string? _profileToastText;
    private bool _suppressProfileLoad;

    // Backup/restore state
    [ObservableProperty] private bool _hasBackup;
    [ObservableProperty] private bool _isRestoring;
    [ObservableProperty] private bool _restoreSuccess;

    /// <summary>
    /// Flat projection the mod list is bound to: a row per visible mod header, followed
    /// by a row per visible item of every expanded mod. Flat + virtualized, because a
    /// nested ItemsControl per mod realizes a container for every item at once (~59
    /// visuals per row) and never releases them.
    /// </summary>
    public BulkObservableCollection<object> Rows { get; } = new();

    private bool _suspendRows;

    public string? AmpPakPath { get; set; }

    /// <summary>Raised when user clicks "Go to Constructor" on an item.</summary>
    public event Action<string>? NavigateToConstructor;
    public void RaiseNavigateToConstructor(string statId) => NavigateToConstructor?.Invoke(statId);

    public int TotalItems => Mods.Sum(m => m.TotalItems);
    public int TotalEnabled => Mods.Sum(m => m.EnabledItems);
    public string AllModsCount => $"{TotalEnabled}/{TotalItems}";
    public string ModsCountText => Loc.Instance.ModsFoundInfo(Mods.Count);

    public bool ShowPatchButton => !IsPatching && !PatchSuccess && PatchError == null;
    public bool ShowRestoreButton => HasBackup && !IsPatching && !IsRestoring;

    private readonly PropertyChangedEventHandler _langHandler;

    /// <summary>
    /// Drop the language subscription so a re-scan does not keep the previous view model
    /// — and its whole ModVM/ItemVM graph — alive through the static Loc.Instance event.
    /// </summary>
    public void Detach() => Loc.Instance.PropertyChanged -= _langHandler;

    public ItemEditorViewModel()
    {
        _langHandler = (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ItemVM.RefreshSharedOptionLabels();
            foreach (var mod in Mods)
                foreach (var item in mod.Items)
                    item.RefreshLanguage();

            OnPropertyChanged(nameof(ModsCountText));
            if (HasMissingItems)
                OnPropertyChanged(nameof(MissingItemsText));
        });
        Loc.Instance.PropertyChanged += _langHandler;
        RefreshProfileList();

        // Restore persisted sort preference (no re-save while loading).
        _loadingSettings = true;
        var ui = ParaTool.App.Services.UiSettingsService.Load();
        if (Enum.TryParse<SortMode>(ui.PatcherSort, out var ps)) _currentSort = ps;
        if (Enum.TryParse<SortMode>(ui.PatcherSecondarySort, out var ss)) _secondarySort = ss;
        _sortDescending = ui.PatcherSortDesc;
        _loadingSettings = false;

        Mods.CollectionChanged += OnModsCollectionChanged;
    }

    partial void OnModsChanged(ObservableCollection<ModVM>? oldValue, ObservableCollection<ModVM> newValue)
    {
        if (oldValue != null)
        {
            oldValue.CollectionChanged -= OnModsCollectionChanged;
            foreach (var mod in oldValue) mod.PropertyChanged -= OnModPropertyChanged;
        }
        newValue.CollectionChanged += OnModsCollectionChanged;
        foreach (var mod in newValue) mod.PropertyChanged += OnModPropertyChanged;
        RebuildRows();
    }

    private void OnModsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null)
            foreach (ModVM mod in e.OldItems) mod.PropertyChanged -= OnModPropertyChanged;
        if (e.NewItems != null)
            foreach (ModVM mod in e.NewItems) mod.PropertyChanged += OnModPropertyChanged;
        RebuildRows();
    }

    private void OnModPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        // Expanding/collapsing a mod changes which item rows exist.
        if (e.PropertyName == nameof(ModVM.IsExpanded))
            RebuildRows();
    }

    /// <summary>
    /// Recompute <see cref="Rows"/> from the current filter, sort and expansion state.
    /// Cheap enough to run on every keystroke: it only walks the VM lists, and the
    /// bound panel only realizes the rows that fit on screen.
    /// </summary>
    public void RebuildRows()
    {
        if (_suspendRows) return;

        var rows = new List<object>();
        foreach (var mod in Mods)
        {
            if (!mod.HasVisibleItems) continue;
            rows.Add(mod);
            if (!mod.IsExpanded) continue;
            foreach (var item in SortItems(mod.Items.Where(i => i.IsVisibleInFilter)))
                rows.Add(item);
        }
        Rows.Reset(rows);
    }

    partial void OnIsPatchingChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowPatchButton));
        OnPropertyChanged(nameof(ShowRestoreButton));
    }
    partial void OnPatchSuccessChanged(bool value) => OnPropertyChanged(nameof(ShowPatchButton));
    partial void OnIsRestoringChanged(bool value) => OnPropertyChanged(nameof(ShowRestoreButton));
    partial void OnHasBackupChanged(bool value) => OnPropertyChanged(nameof(ShowRestoreButton));
    partial void OnPatchErrorChanged(string? value) => OnPropertyChanged(nameof(ShowPatchButton));
    partial void OnCurrentSortChanged(SortMode value) { ApplySort(); PersistSort(); }
    partial void OnSecondarySortChanged(SortMode value) { ApplySort(); PersistSort(); }
    partial void OnSortDescendingChanged(bool value) { ApplySort(); PersistSort(); }

    private bool _loadingSettings;

    private void PersistSort()
    {
        if (_loadingSettings) return;
        var s = ParaTool.App.Services.UiSettingsService.Load();
        s.PatcherSort = CurrentSort.ToString();
        s.PatcherSecondarySort = SecondarySort.ToString();
        s.PatcherSortDesc = SortDescending;
        ParaTool.App.Services.UiSettingsService.Save(s);
    }
    partial void OnSearchTextChanged(string? value) => ApplyFilters();
    partial void OnHideDisabledChanged(bool value) => ApplyFilters();

    [RelayCommand]
    private void SetSort(string mode)
    {
        CurrentSort = Enum.Parse<SortMode>(mode);
    }

    [RelayCommand]
    private void SetSecondarySort(string mode)
    {
        SecondarySort = Enum.Parse<SortMode>(mode);
    }

    [RelayCommand]
    private void ToggleSortDirection()
    {
        SortDescending = !SortDescending;
    }

    private static readonly Dictionary<string, int> RarityOrder = new()
    {
        ["Common"] = 0, ["Uncommon"] = 1, ["Rare"] = 2, ["VeryRare"] = 3, ["Legendary"] = 4
    };

    private static readonly Dictionary<string, int> SlotOrder = new()
    {
        ["Clothes"] = 0, ["Armor"] = 1, ["Shields"] = 2, ["Hats"] = 3,
        ["Cloaks"] = 4, ["Gloves"] = 5, ["Boots"] = 6,
        ["Amulets"] = 7, ["Rings"] = 8,
        ["Weapons"] = 9, ["Weapons_1H"] = 10, ["Weapons_2H"] = 11
    };

    private IComparable GetSortKey(ItemVM i, SortMode mode) => mode switch
    {
        SortMode.Rarity => RarityOrder.GetValueOrDefault(i.Entry.EffectiveRarity, 99),
        SortMode.Theme => (i.Entry.EffectiveThemes.Count == 0 ? "~" : i.Entry.EffectiveThemes.FirstOrDefault() ?? "~"),
        SortMode.Slot => SlotOrder.GetValueOrDefault(i.Entry.EffectivePool, 99),
        _ => (IComparable)(i.DisplayName ?? i.StatId)
    };

    private IEnumerable<ItemVM> SortItems(IEnumerable<ItemVM> items)
    {
        var sorted = items
            .OrderBy(i => GetSortKey(i, CurrentSort))
            .ThenBy(i => GetSortKey(i, SecondarySort));

        return SortDescending ? sorted.Reverse() : sorted;
    }

    /// <summary>
    /// Sorting is applied to the row projection, not by reordering ModVM.Items — moving
    /// items inside a bound collection costs one container move per item on top of an
    /// O(n^2) index lookup.
    /// </summary>
    public void ApplySort() => RebuildRows();

    public void ApplyFilters()
    {
        var query = SearchText?.Trim();
        var hasSearch = !string.IsNullOrEmpty(query);
        var hasThemeFilter = HiddenThemes.Count > 0;

        // Auto-expand below flips IsExpanded per mod; rebuild the rows once at the end
        // instead of once per mod.
        _suspendRows = true;
        try
        {
            foreach (var mod in Mods)
            {
                foreach (var item in mod.Items)
                {
                    bool visible = true;

                    if (HideDisabled && !item.Enabled)
                        visible = false;

                    if (visible && hasThemeFilter)
                    {
                        // Hide items whose ALL themes are in HiddenThemes (or have no themes and "None" is hidden)
                        var themes = item.Entry.EffectiveThemes;
                        if (themes.Count == 0)
                            visible = !HiddenThemes.Contains("None");
                        else
                            visible = themes.Any(t => !HiddenThemes.Contains(t));
                    }

                    if (visible && hasSearch)
                    {
                        visible = item.ItemLabel.Contains(query!, StringComparison.OrdinalIgnoreCase)
                               || item.StatId.Contains(query!, StringComparison.OrdinalIgnoreCase)
                               || (item.Entry.SearchableText != null
                                   && item.Entry.SearchableText.Contains(query!, StringComparison.OrdinalIgnoreCase));
                    }

                    item.IsVisibleInFilter = visible;
                }

                mod.RefreshFilterState();

                // Auto-expand mods with search results
                if (hasSearch && mod.HasVisibleItems)
                    mod.IsExpanded = true;
            }
        }
        finally { _suspendRows = false; }

        RebuildRows();
        RefreshCounts();
    }

    [RelayCommand]
    private void ClearSearch()
    {
        SearchText = "";
    }

    public void ToggleThemeFilter(string theme)
    {
        if (!HiddenThemes.Remove(theme))
            HiddenThemes.Add(theme);
        ApplyFilters();
    }

    /// <summary>
    /// Enable or disable all items matching a given pool across all mods.
    /// </summary>
    public void SetPoolEnabled(string pool, bool enabled)
    {
        foreach (var mod in Mods)
        {
            foreach (var item in mod.Items)
            {
                if (item.Entry.EffectivePool.Equals(pool, StringComparison.OrdinalIgnoreCase))
                    item.Enabled = enabled;
            }
            mod.RefreshCounts();
        }
        RefreshCounts();
    }

    /// <summary>
    /// Enable or disable all items matching a given theme across all mods.
    /// </summary>
    public void SetThemeEnabled(string theme, bool enabled)
    {
        foreach (var mod in Mods)
        {
            foreach (var item in mod.Items)
            {
                if (item.Entry.EffectiveThemes.Contains(theme))
                    item.Enabled = enabled;
            }
            mod.RefreshCounts();
        }
        RefreshCounts();
    }

    /// <summary>
    /// Check if any item in the given pool is enabled.
    /// </summary>
    public bool IsPoolEnabled(string pool) =>
        Mods.SelectMany(m => m.Items)
            .Where(i => i.Entry.EffectivePool.Equals(pool, StringComparison.OrdinalIgnoreCase))
            .Any(i => i.Enabled);

    /// <summary>
    /// Check if any item in the given theme is enabled.
    /// </summary>
    public bool IsThemeEnabled(string theme) =>
        Mods.SelectMany(m => m.Items)
            .Where(i => i.Entry.EffectiveThemes.Contains(theme))
            .Any(i => i.Enabled);

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
            PatchSuccessMessage = result.SubmodsPatched > 0
                ? $"{Loc.Instance.PatchSuccessMessage(result.ItemsPatched)}\n{Loc.Instance.PatchSubmodsMessage(result.SubmodsPatched)}"
                : Loc.Instance.PatchSuccessMessage(result.ItemsPatched);

            PatchWarnings.Clear();
            foreach (var w in result.Warnings)
                PatchWarnings.Add(w);

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

            // Reset success state after 5 seconds so user can patch again
            _ = Task.Run(async () =>
            {
                await Task.Delay(5000);
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    PatchSuccess = false;
                    PatchSuccessMessage = null;
                    OnPropertyChanged(nameof(ShowPatchButton));
                });
            });
        }
        else
        {
            PatchError = result.Error;
            PatchStatus = $"{Loc.Instance.PatchErrorLabel} {result.Error}";
        }

        IsPatching = false;
        CheckBackup();
    }

    public void CheckBackup()
    {
        HasBackup = (AmpPakPath != null && AmpBackupService.HasBackup(AmpPakPath))
            || SubmodPakPaths().Any(AmpBackupService.HasBackup);
    }

    /// <summary>
    /// Paks of AMP submods. Patching appends the stat overrides into these too — a submod loads
    /// after AMP, so entries it re-declares would otherwise keep the submod's own values — which
    /// means restoring has to put them back as well.
    /// </summary>
    private IEnumerable<string> SubmodPakPaths() => Mods
        .Where(m => m.IsAmpSubmod && !string.IsNullOrEmpty(m.ModInfo.PakPath))
        .Select(m => m.ModInfo.PakPath!);

    /// <summary>
    /// Raised when restore completes — MainWindowViewModel should re-scan.
    /// </summary>
    public event Action? RestoreCompleted;

    [RelayCommand]
    private async Task RestoreAmpAsync()
    {
        if (AmpPakPath == null) return;

        IsRestoring = true;
        RestoreSuccess = false;

        var submodPaks = SubmodPakPaths().ToList();
        var success = await Task.Run(() =>
        {
            var restored = AmpBackupService.Restore(AmpPakPath);
            foreach (var pak in submodPaks)
                restored |= AmpBackupService.RestorePak(pak);
            return restored;
        });

        IsRestoring = false;

        if (success)
        {
            RestoreSuccess = true;
            RestoreCompleted?.Invoke();
        }
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
