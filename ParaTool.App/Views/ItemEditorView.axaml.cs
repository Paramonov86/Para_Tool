
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using ParaTool.App.Localization;
using ParaTool.App.ViewModels;
using ParaTool.App.Services;

namespace ParaTool.App.Views;

public partial class ItemEditorView : UserControl
{
    private ItemVM? _currentThemeItem;
    private ModVM? _currentThemeMod;
    private double _savedScrollOffset;

    public ItemEditorView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);

        // Preserve scroll position on focus loss / visibility change
        var scroll = this.FindControl<ScrollViewer>("ModListScroll");
        if (scroll != null)
        {
            scroll.ScrollChanged += (_, _) => _savedScrollOffset = scroll.Offset.Y;
            scroll.AttachedToVisualTree += (_, _) => scroll.Offset = new Vector(0, _savedScrollOffset);
        }
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is ItemEditorViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName is nameof(vm.CurrentSort) or nameof(vm.SortDescending))
                    UpdateSortButtons(vm);
            };
            UpdateSortButtons(vm);
        }
    }

    private void UpdateSortButtons(ItemEditorViewModel vm)
    {
        var active = Application.Current!.FindResource("AccentBrush") as IBrush ?? Brushes.Purple;
        var inactive = Application.Current!.FindResource("CardBgBrush") as IBrush ?? Brushes.Gray;
        var activeFg = Brushes.White;
        var inactiveFg = Application.Current!.FindResource("TextMutedBrush") as IBrush ?? Brushes.Gray;

        var nameBtn = this.FindControl<Button>("SortNameBtn");
        var rarityBtn = this.FindControl<Button>("SortRarityBtn");
        var themeBtn = this.FindControl<Button>("SortThemeBtn");
        var slotBtn = this.FindControl<Button>("SortSlotBtn");
        var dirBtn = this.FindControl<Button>("SortDirBtn");

        foreach (var (btn, mode) in new[] {
            (nameBtn, SortMode.Name), (rarityBtn, SortMode.Rarity),
            (themeBtn, SortMode.Theme), (slotBtn, SortMode.Slot) })
        {
            if (btn == null) continue;
            btn.Background = vm.CurrentSort == mode ? active : inactive;
            btn.Foreground = vm.CurrentSort == mode ? activeFg : inactiveFg;
        }

        if (dirBtn != null)
            dirBtn.Content = vm.SortDescending ? "\u25B2" : "\u25BC"; // ▲ / ▼
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        if (btn.Name == "ExpandBtn" && btn.Tag is ModVM mod)
        {
            mod.IsExpanded = !mod.IsExpanded;
        }
        else if (btn.Name == "ThemeBtn" && btn.Tag is ItemVM item)
        {
            ShowItemThemePopup(item);
        }
        else if (btn.Name == "ModThemeBtn" && btn.Tag is ModVM themeMod)
        {
            ShowModThemePopup(themeMod);
        }
        else if (btn.Name == "DiscordBtn")
        {
            OpenDiscord();
        }
    }

    private void ShowItemThemePopup(ItemVM item)
    {
        var popup = this.FindControl<Border>("ThemePopup");
        var container = this.FindControl<ItemsControl>("ThemeCheckboxes");
        var title = this.FindControl<TextBlock>("ThemePopupTitle");
        if (popup == null || container == null) return;

        _currentThemeItem = item;
        _currentThemeMod = null;

        if (title != null)
            title.Text = $"{Loc.Instance.ThemesLabel}: {item.StatId}";

        BuildThemeCheckboxes(container,
            theme => item.SelectedThemes.Contains(theme));

        popup.IsVisible = true;
    }

    private void ShowModThemePopup(ModVM mod)
    {
        var popup = this.FindControl<Border>("ThemePopup");
        var container = this.FindControl<ItemsControl>("ThemeCheckboxes");
        var title = this.FindControl<TextBlock>("ThemePopupTitle");
        if (popup == null || container == null) return;

        _currentThemeItem = null;
        _currentThemeMod = mod;

        if (title != null)
            title.Text = $"{Loc.Instance.ModThemes}: {mod.Name}";

        BuildThemeCheckboxes(container,
            theme => mod.Items.All(i => i.SelectedThemes.Contains(theme)));

        popup.IsVisible = true;
    }

    private void BuildThemeCheckboxes(ItemsControl container, Func<string, bool> isChecked)
    {
        var panel = new StackPanel { Spacing = 2 };
        foreach (var theme in ItemVM.AvailableThemes)
        {
            var cb = new CheckBox
            {
                Content = Loc.Instance.ThemeName(theme),
                IsChecked = isChecked(theme),
                FontSize = FontScale.Of(12),
                Tag = theme
            };
            cb.IsCheckedChanged += OnThemeCheckedChanged;
            panel.Children.Add(cb);
        }

        container.ItemsSource = null;
        container.Items.Clear();
        container.Items.Add(panel);
    }

    private void OnThemeCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not string theme) return;

        if (_currentThemeMod != null)
        {
            foreach (var item in _currentThemeMod.Items)
            {
                if (cb.IsChecked == true && !item.SelectedThemes.Contains(theme))
                    item.SelectedThemes.Add(theme);
                else if (cb.IsChecked == false)
                    item.SelectedThemes.Remove(theme);
                item.NotifyThemesChanged();
            }
        }
        else if (_currentThemeItem != null)
        {
            if (cb.IsChecked == true && !_currentThemeItem.SelectedThemes.Contains(theme))
                _currentThemeItem.SelectedThemes.Add(theme);
            else if (cb.IsChecked == false)
                _currentThemeItem.SelectedThemes.Remove(theme);
            _currentThemeItem.NotifyThemesChanged();
        }
    }

    private static void OpenDiscord()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/X8BQxYhvNW",
                UseShellExecute = true
            });
        }
        catch { /* ignore if browser can't open */ }
    }
}
