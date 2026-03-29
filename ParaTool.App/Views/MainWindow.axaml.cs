using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ParaTool.App.Services;
using ParaTool.App.Themes;
using ParaTool.App.ViewModels;

namespace ParaTool.App.Views;

public partial class MainWindow : Window
{
    private UiSettings _uiSettings = new();
    private bool _suppressSave; // prevent saving during initial load

    private WindowState _stateBeforeFullscreen;

    public MainWindow()
    {
        InitializeComponent();

        var themeSelector = this.FindControl<ComboBox>("ThemeSelector");
        if (themeSelector != null)
            themeSelector.SelectionChanged += OnThemeChanged;

        var fontSelector = this.FindControl<ComboBox>("FontSizeSelector");
        if (fontSelector != null)
            fontSelector.SelectionChanged += OnFontSizeChanged;

        var pinPatcher = this.FindControl<TextBlock>("PinPatcherIcon");
        var pinConstructor = this.FindControl<TextBlock>("PinConstructorIcon");
        if (pinPatcher != null) pinPatcher.PointerPressed += (_, e) => { SetDefaultTab("Patcher"); e.Handled = true; };
        if (pinConstructor != null) pinConstructor.PointerPressed += (_, e) => { SetDefaultTab("Constructor"); e.Handled = true; };

        // Show which tab is pinned
        Loaded += (_, _) => UpdatePinIcons();

        RestoreUiSettings(themeSelector, fontSelector);

        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.F11) { ToggleFullscreen(); e.Handled = true; }
    }

    private void OnFullscreenClick(object? sender, RoutedEventArgs e) => ToggleFullscreen();

    private void SetDefaultTab(string tab)
    {
        _uiSettings.DefaultTab = tab;
        UiSettingsService.Save(_uiSettings);
        UpdatePinIcons();
    }

    private void UpdatePinIcons()
    {
        var pinPatcher = this.FindControl<TextBlock>("PinPatcherIcon");
        var pinConstructor = this.FindControl<TextBlock>("PinConstructorIcon");
        if (pinPatcher != null) pinPatcher.Opacity = _uiSettings.DefaultTab == "Patcher" ? 1.0 : 0.25;
        if (pinConstructor != null) pinConstructor.Opacity = _uiSettings.DefaultTab == "Constructor" ? 1.0 : 0.25;
    }

    private void ToggleFullscreen()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = _stateBeforeFullscreen;
        }
        else
        {
            _stateBeforeFullscreen = WindowState;
            WindowState = WindowState.Maximized;
        }
    }

    private void RestoreUiSettings(ComboBox? themeSelector, ComboBox? fontSelector)
    {
        _suppressSave = true;
        _uiSettings = UiSettingsService.Load();

        // Restore theme
        if (themeSelector != null)
        {
            var theme = ThemeManager.AllThemes.FirstOrDefault(t => t.Name == _uiSettings.Theme)
                ?? ThemeManager.Paramonov;
            // Find matching ComboBoxItem
            for (int i = 0; i < themeSelector.Items.Count; i++)
            {
                if (themeSelector.Items[i] is ComboBoxItem item
                    && (item.Content?.ToString()?.Contains(theme.Name) ?? false))
                {
                    themeSelector.SelectedIndex = i;
                    break;
                }
            }
            ThemeManager.ApplyTheme(Application.Current!, theme);
        }

        // Restore font size
        if (fontSelector != null)
        {
            var idx = Math.Clamp(_uiSettings.FontSizeIndex, 0, fontSelector.Items.Count - 1);
            fontSelector.SelectedIndex = idx;
            ApplyFontScale(ScaleFactors[idx]);
        }

        _suppressSave = false;
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
            var text = item.Content?.ToString() ?? "";
            var theme = ThemeManager.AllThemes.FirstOrDefault(t => text.Contains(t.Name))
                ?? ThemeManager.Paramonov;
            ThemeManager.ApplyTheme(Application.Current!, theme);

            if (!_suppressSave)
            {
                _uiSettings.Theme = theme.Name;
                UiSettingsService.Save(_uiSettings);
            }
        }
    }

    private static readonly int[] BaseFontSizes = { 10, 11, 12, 13, 14, 16, 18, 20, 22, 24 };
    private static readonly double[] ScaleFactors = { 0.7, 1.0, 1.5 };

    private void ApplyFontScale(double scale)
    {
        Services.FontScale.Factor = scale;
        FontSize = Math.Round(14 * scale);
        if (Application.Current == null) return;
        Application.Current.Resources["DefaultFontSize"] = FontSize;
        foreach (var bs in BaseFontSizes)
            Application.Current.Resources[$"FontSize{bs}"] = Math.Round(bs * scale);
        Services.FontScale.NotifyChanged();
    }

    private void OnFontSizeChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedIndex >= 0
            && cb.SelectedIndex < ScaleFactors.Length)
        {
            ApplyFontScale(ScaleFactors[cb.SelectedIndex]);

            if (!_suppressSave)
            {
                _uiSettings.FontSizeIndex = cb.SelectedIndex;
                UiSettingsService.Save(_uiSettings);
            }
        }
    }
}
