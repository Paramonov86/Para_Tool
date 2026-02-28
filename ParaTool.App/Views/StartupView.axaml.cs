using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ParaTool.App.ViewModels;

namespace ParaTool.App.Views;

public partial class StartupView : UserControl
{
    public StartupView()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        var btn = this.FindControl<Button>("SelectFolderBtn");
        if (btn != null)
            btn.Click += OnSelectFolder;
    }

    private async void OnSelectFolder(object? sender, RoutedEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Select BG3 Mods folder",
            AllowMultiple = false
        });

        if (folders.Count > 0)
        {
            var path = folders[0].Path.LocalPath;
            if (DataContext is StartupViewModel vm)
                vm.RaiseFolderSelected(path);
        }
    }
}
