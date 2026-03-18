using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ParaTool.App.ViewModels;

namespace ParaTool.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private void OnUpdateButtonTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.UpdateButtonClickCommand.Execute(null);
    }
}
