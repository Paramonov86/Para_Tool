using System.Globalization;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using ParaTool.App.Localization;
using ParaTool.Core.Models;
using ParaTool.Core.Services;

namespace ParaTool.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private ViewModelBase? _currentView;
    [ObservableProperty] private LangInfo? _selectedLanguage;

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
            scanVm.TotalPaks = p.TotalPaks;
            scanVm.ScannedPaks = p.ScannedPaks;
            scanVm.ModsFound = p.ModsFound;
        });

        // Run scan + minimum display time in parallel
        var scanTask = scanner.ScanAsync(modsPath, progress);
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
}
