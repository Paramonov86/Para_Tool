using CommunityToolkit.Mvvm.ComponentModel;

namespace ParaTool.App.ViewModels;

public partial class ScanningViewModel : ViewModelBase
{
    [ObservableProperty] private int _totalPaks;
    [ObservableProperty] private int _scannedPaks;
    [ObservableProperty] private int _modsFound;
}
