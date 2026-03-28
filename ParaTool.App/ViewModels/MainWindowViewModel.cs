using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Controls;
using ParaTool.App.Localization;
using ParaTool.Core.Models;
using ParaTool.Core.Services;

namespace ParaTool.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private ViewModelBase? _currentView;
    [ObservableProperty] private LangInfo? _selectedLanguage;

    // Update state
    [ObservableProperty] private UpdateState _updateState = UpdateState.Idle;
    [ObservableProperty] private string? _updateVersion;
    [ObservableProperty] private int _updateProgress;
    [ObservableProperty] private string? _updateError;

    private readonly UpdateService _updateService = new();
    private UpdateService.UpdateInfo? _pendingUpdate;

    public LangInfo[] Languages => Loc.Instance.AvailableLanguages;

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
        var defaultCode = Loc.Instance.Lang;
        _selectedLanguage = Languages.FirstOrDefault(l => l.Code == defaultCode) ?? Languages.First();

        Initialize();

        // Check for updates in background (don't block UI)
        _ = CheckForUpdateAsync();
    }

    partial void OnSelectedLanguageChanged(LangInfo? value)
    {
        if (value != null)
            Loc.Instance.SetLanguage(value.Code);
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

        var editor = new ItemEditorViewModel
        {
            AmpPakPath = result.AmpPakPath
        };

        // Add AMP mod first if it has items
        if (result.AmpMod != null)
            editor.Mods.Add(new ModVM(result.AmpMod));

        foreach (var mod in result.Mods)
            editor.Mods.Add(new ModVM(mod));
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

        CurrentView = editor;
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
