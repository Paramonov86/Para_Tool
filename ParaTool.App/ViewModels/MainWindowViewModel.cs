using System.Globalization;
using System.Reflection;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Controls;
using ParaTool.App.Localization;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Models;
using ParaTool.Core.Patching;
using ParaTool.Core.Services;
using ParaTool.App.Services;

namespace ParaTool.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private ViewModelBase? _currentView;
    [ObservableProperty] private LangInfo? _selectedLanguage;

    // Tab bar
    [ObservableProperty] private bool _showTabBar;
    [ObservableProperty] private string _activeTab = "Patcher";

    // Update state
    [ObservableProperty] private UpdateState _updateState = UpdateState.Idle;
    [ObservableProperty] private string? _updateVersion;
    [ObservableProperty] private int _updateProgress;
    [ObservableProperty] private string? _updateError;

    private readonly UpdateService _updateService = new();
    private UpdateService.UpdateInfo? _pendingUpdate;

    // Cached views for tab switching
    private ItemEditorViewModel? _patcherView;
    private ConstructorViewModel? _constructorView;
    private ParaTool.Core.Parsing.StatsResolver? _statsResolver;
    private Dictionary<string, string>? _locaMap;
    private HashSet<string>? _existingStatIds;
    private LocaService? _locaService;
    private IconService? _iconService;

    public LangInfo[] Languages => Loc.Instance.AvailableLanguages;

    // Tab visual state
    public IBrush PatcherTabBrush => ActiveTab == "Patcher" ? Themes.ThemeBrushes.Accent : Brushes.Transparent;
    public IBrush PatcherTabForeground => ActiveTab == "Patcher" ? Themes.ThemeBrushes.TextPrimary : Themes.ThemeBrushes.TextMuted;
    public IBrush ConstructorTabBrush => ActiveTab == "Constructor" ? Themes.ThemeBrushes.Accent : Brushes.Transparent;
    public IBrush ConstructorTabForeground => ActiveTab == "Constructor" ? Themes.ThemeBrushes.TextPrimary : Themes.ThemeBrushes.TextMuted;

    public string AppVersion
    {
        get
        {
            var ver = typeof(MainWindowViewModel).Assembly.GetName().Version;
            return ver != null ? $"v{ver.ToString(3)}" : "v0.1.0";
        }
    }

    public MainWindowViewModel()
    {
        // Restore saved language
        var savedLang = UiSettingsService.Load().Language;
        if (!string.IsNullOrEmpty(savedLang) && Languages.Any(l => l.Code == savedLang))
        {
            Loc.Instance.SetLanguage(savedLang);
            _selectedLanguage = Languages.FirstOrDefault(l => l.Code == savedLang);
        }
        else
        {
            _selectedLanguage = Languages.FirstOrDefault(l => l.Code == Loc.Instance.Lang) ?? Languages.First();
        }

        Initialize();

        // Check for updates in background (don't block UI)
        _ = CheckForUpdateAsync();
    }

    partial void OnSelectedLanguageChanged(LangInfo? value)
    {
        if (value != null)
        {
            Loc.Instance.SetLanguage(value.Code);
            var settings = UiSettingsService.Load();
            settings.Language = value.Code;
            UiSettingsService.Save(settings);
        }
    }

    partial void OnActiveTabChanged(string value)
    {
        OnPropertyChanged(nameof(PatcherTabBrush));
        OnPropertyChanged(nameof(PatcherTabForeground));
        OnPropertyChanged(nameof(ConstructorTabBrush));
        OnPropertyChanged(nameof(ConstructorTabForeground));
    }

    [RelayCommand]
    private void SwitchToPatcher()
    {
        if (_patcherView == null) return;

        // Refresh artifacts mod when switching back to patcher
        RefreshArtifactsMod();

        ActiveTab = "Patcher";
        CurrentView = _patcherView;
    }

    [RelayCommand]
    private void SwitchToConstructor()
    {
        if (_constructorView == null)
        {
            _constructorView = new ConstructorViewModel(_statsResolver, _locaService, _iconService);
            if (_patcherView != null)
            {
                // Exclude virtual artifacts mod from base items
                var realMods = _patcherView.Mods.Where(m => m.ModInfo.UUID != ArtifactsModUuid);
                _constructorView.SetBaseItems(realMods);
            }
        }
        ActiveTab = "Constructor";
        CurrentView = _constructorView;
    }

    private void Initialize()
    {
        var modsPath = ModsFolderDetector.Detect();
        if (modsPath == null)
        {
            var startup = new StartupViewModel();
            startup.SetError(Loc.Instance.ErrorModsNotFound);
            startup.FolderSelected += path => OnModsFolderSelected(path);
            CurrentView = startup;
        }
        else
        {
            StartScanning(modsPath);
        }
    }

    public void OnModsFolderSelected(string path)
    {
        if (string.IsNullOrEmpty(path)) return;
        StartScanning(path);
    }

    private async void StartScanning(string modsPath)
    {
        ShowTabBar = false;

        var scanVm = new ScanningViewModel();
        CurrentView = scanVm;

        var vanillaDb = new VanillaDatabase();
        vanillaDb.Load();

        var scanner = new ModScanner(vanillaDb);
        var progress = new Progress<ScanProgress>(p =>
        {
            // Only update counters when non-zero — later stages don't carry them
            if (p.TotalPaks > 0) scanVm.TotalPaks = p.TotalPaks;
            if (p.ScannedPaks > 0) scanVm.ScannedPaks = p.ScannedPaks;
            if (p.ModsFound > 0) scanVm.ModsFound = p.ModsFound;
            scanVm.Percent = p.Percent;
            scanVm.StageText = p.Stage switch
            {
                "ScanMods" => Localization.Loc.Instance.ScanStageMods,
                "ScanAMP" => Localization.Loc.Instance.ScanStageAMP,
                "BuildResolver" => Localization.Loc.Instance.ScanStageResolver,
                "ResolveNames" or "ResolveTemplates" => Localization.Loc.Instance.ScanStageNames,
                "ScanTemplates" => Localization.Loc.Instance.ScanStageTemplates,
                "ResolveLoca" => Localization.Loc.Instance.ScanStageLoca,
                "Done" => Localization.Loc.Instance.ScanStageDone,
                _ => p.Stage ?? ""
            };
        });

        // Run scan + minimum display time in parallel
        var scanTask = scanner.ScanAsync(modsPath, Localization.Loc.Instance.Lang, progress);
        var minDisplayTask = Task.Delay(1500);
        await Task.WhenAll(scanTask, minDisplayTask);

        var result = scanTask.Result;

        if (result.Error != null)
        {
            var startup = new StartupViewModel();
            startup.SetError(result.Error);
            startup.FolderSelected += path => OnModsFolderSelected(path);
            CurrentView = startup;
            return;
        }

        _statsResolver = result.Resolver;
        _locaMap = result.LocaMap;

        // Build loca service BEFORE creating ModVMs (they need it for dynamic name resolution)
        _locaService = new LocaService(result.PakPaths);
        _locaService.SeedCache(Loc.Instance.Lang, result.LocaMap);

        var editor = new ItemEditorViewModel
        {
            AmpPakPath = result.AmpPakPath
        };

        // Add AMP mod first if it has items
        if (result.AmpMod != null)
            editor.Mods.Add(new ModVM(result.AmpMod, _locaService));

        foreach (var mod in result.Mods)
            editor.Mods.Add(new ModVM(mod, _locaService));

        // Add saved artifacts as virtual mod
        var artifactsMod = BuildArtifactsMod();
        if (artifactsMod != null)
            editor.Mods.Add(new ModVM(artifactsMod, _locaService));

        editor.RefreshCounts();

        // Check if backup exists
        editor.CheckBackup();

        // Re-scan after AMP restore
        editor.RestoreCompleted += () => StartScanning(modsPath);

        // Restore last session selections
        try
        {
            var lastSession = ProfileService.LoadLastSession();
            if (lastSession != null)
                editor.ApplyProfileData(lastSession);
        }
        catch { /* ignore corrupted session file */ }

        _patcherView = editor;
        _constructorView = null; // reset on rescan
        // English loaded on-demand — scan LocaMap contains UI language texts only

        // Icon service for lazy DDS loading
        _iconService = new IconService(result.PakPaths);

        // Collect all known StatIds for override detection
        _existingStatIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (result.AmpMod != null)
            foreach (var item in result.AmpMod.Items) _existingStatIds.Add(item.StatId);
        foreach (var mod in result.Mods)
            foreach (var item in mod.Items) _existingStatIds.Add(item.StatId);
        ShowTabBar = true;

        // Open default tab from settings
        var defaultTab = UiSettingsService.Load().DefaultTab;
        if (defaultTab == "Constructor")
            SwitchToConstructor();
        else
        {
            ActiveTab = "Patcher";
            CurrentView = editor;
        }
    }

    private const string ArtifactsModUuid = "paratool-artifacts-virtual-mod";

    private ModInfo? BuildArtifactsMod()
    {
        var artifacts = ArtifactStore.LoadAll();
        if (artifacts.Count == 0) return null;

        // Only show NEW artifacts (not overrides of existing items)
        var newArtifacts = artifacts
            .Where(art => _existingStatIds == null || !_existingStatIds.Contains(art.StatId))
            .ToList();

        if (newArtifacts.Count == 0) return null;

        var items = newArtifacts.Select(art => new ItemEntry
        {
            StatId = art.StatId,
            StatType = art.StatType,
            DisplayName = art.DisplayName.TryGetValue(Loc.Instance.Lang, out var n) ? n
                : art.DisplayName.TryGetValue("en", out var en) ? en : null,
            DetectedPool = art.LootPool,
            DetectedRarity = art.Rarity,
            DetectedThemes = new List<string>(art.LootThemes),
            Enabled = art.AddToLoot,
        }).ToList();

        return new ModInfo
        {
            Name = "\u2728 My Artifacts",
            UUID = ArtifactsModUuid,
            Folder = "ParaTool_Artifacts",
            PakPath = "",
            Items = items
        };
    }

    private void RefreshArtifactsMod()
    {
        if (_patcherView == null) return;

        // Remove old artifacts mod
        var old = _patcherView.Mods.FirstOrDefault(m => m.ModInfo.UUID == ArtifactsModUuid);
        if (old != null)
            _patcherView.Mods.Remove(old);

        // Add fresh
        var artifactsMod = BuildArtifactsMod();
        if (artifactsMod != null)
            _patcherView.Mods.Add(new ModVM(artifactsMod, _locaService));

        // Mark patcher items that have artifact overrides
        var artifactStatIds = new HashSet<string>(
            ArtifactStore.LoadAll().Select(a => a.StatId),
            StringComparer.OrdinalIgnoreCase);

        foreach (var mod in _patcherView.Mods)
        {
            if (mod.ModInfo.UUID == ArtifactsModUuid) continue;
            foreach (var item in mod.Items)
            {
                var had = item.Entry.HasArtifactOverride;
                item.Entry.HasArtifactOverride = artifactStatIds.Contains(item.StatId);
                if (had != item.Entry.HasArtifactOverride)
                    item.NotifyArtifactOverrideChanged();
            }
        }

        _patcherView.RefreshCounts();
    }

    [RelayCommand]
    private async Task UpdateButtonClickAsync()
    {
        switch (UpdateState)
        {
            case UpdateState.Idle:
            case UpdateState.UpToDate:
            case UpdateState.Error:
                await CheckForUpdateAsync();
                break;
            case UpdateState.Available:
                await DownloadAndApplyAsync();
                break;
        }
    }

    private async Task CheckForUpdateAsync()
    {
        try
        {
            UpdateState = UpdateState.Checking;
            UpdateError = null;

            var info = await _updateService.CheckAsync(AppVersion);
            if (info != null)
            {
                _pendingUpdate = info;
                UpdateVersion = info.Version;
                UpdateState = UpdateState.Available;
            }
            else
            {
                UpdateState = UpdateState.UpToDate;
            }
        }
        catch (Exception ex)
        {
            UpdateError = ex.Message;
            UpdateState = UpdateState.Error;
        }
    }

    private async Task DownloadAndApplyAsync()
    {
        if (_pendingUpdate == null) return;

        try
        {
            UpdateState = UpdateState.Downloading;
            UpdateProgress = 0;

            var progress = new Progress<int>(p => UpdateProgress = p);
            var extractedDir = await _updateService.DownloadAndExtractAsync(
                _pendingUpdate.DownloadUrl, progress);

            var currentAppDir = AppContext.BaseDirectory.TrimEnd(
                Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            _updateService.ApplyAndRestart(extractedDir, currentAppDir);
        }
        catch (Exception ex)
        {
            UpdateError = ex.Message;
            UpdateState = UpdateState.Error;
        }
    }
}
