using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ParaTool.App.Themes;
using ParaTool.App.ViewModels;

namespace ParaTool.App.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var themeSelector = this.FindControl<ComboBox>("ThemeSelector");
        if (themeSelector != null)
            themeSelector.SelectionChanged += OnThemeChanged;

        var fontSelector = this.FindControl<ComboBox>("FontSizeSelector");
        if (fontSelector != null)
            fontSelector.SelectionChanged += OnFontSizeChanged;
    }

    private void OnUpdateButtonTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm)
            vm.UpdateButtonClickCommand.Execute(null);
    }

    private void OnThemeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item)
        {
            var themeName = item.Content?.ToString() ?? "Paramonov";
            var theme = ThemeManager.AllThemes.FirstOrDefault(t => t.Name == themeName)
                ?? ThemeManager.Paramonov;
            ThemeManager.ApplyTheme(Application.Current!, theme);
        }
    }

    private void OnFontSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is ComboBoxItem item
            && item.Tag is string sizeStr && double.TryParse(sizeStr, out var size))
        {
            // Scale all text by changing the window's default font size
            // This propagates to children that don't have explicit FontSize
            FontSize = size;
            // Also set on Application level for new controls
            if (Application.Current != null)
                Application.Current.Resources["DefaultFontSize"] = size;
        }
    }
}
