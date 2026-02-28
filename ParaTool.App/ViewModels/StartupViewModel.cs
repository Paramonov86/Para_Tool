using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ParaTool.App.ViewModels;

public partial class StartupViewModel : ViewModelBase
{
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _hasError;

    public event Action<string>? FolderSelected;

    public void RaiseFolderSelected(string path)
    {
        FolderSelected?.Invoke(path);
    }

    [RelayCommand]
    private void SelectFolder()
    {
        // Will be handled by View code-behind to open folder picker
        FolderSelected?.Invoke("");
    }

    public void SetError(string message)
    {
        ErrorMessage = message;
        HasError = true;
    }
}
