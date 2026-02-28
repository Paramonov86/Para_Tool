using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Localization;
using ParaTool.Core.Services;

namespace ParaTool.App.ViewModels;

public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty] private ViewModelBase? _currentView;
    [ObservableProperty] private string _selectedLanguage;

    public Loc Loc => Loc.Instance;

    public string[] Languages => new[] { "Русский", "English" };

    public MainWindowViewModel()
    {
        // Auto-detect language
        var culture = CultureInfo.CurrentUICulture;
        _selectedLanguage = culture.TwoLetterISOLanguageName == "ru" ? "Русский" : "English";
        Loc.SetLanguage(_selectedLanguage == "Русский" ? "ru" : "en");

        Initialize();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        Loc.SetLanguage(value == "Русский" ? "ru" : "en");
        OnPropertyChanged(nameof(Loc));
    }

    private void Initialize()
    {
        var modsPath = ModsFolderDetector.Detect();
        if (modsPath == null)
        {
            var startup = new StartupViewModel();
            startup.SetError(Loc.ErrorModsNotFound);
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

        var result = await scanner.ScanAsync(modsPath, progress);

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

        foreach (var mod in result.Mods)
            editor.Mods.Add(new ModVM(mod));
        editor.RefreshCounts();

        CurrentView = editor;
    }
}
