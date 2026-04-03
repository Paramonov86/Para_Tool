
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
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
                if (args.PropertyName is nameof(vm.CurrentSort) or nameof(vm.SecondarySort) or nameof(vm.SortDescending))
                    UpdateSortButtons(vm);
            };
            UpdateSortButtons(vm);
            BuildPoolThemeToggles(vm);
        }
    }

    private void UpdateSortButtons(ItemEditorViewModel vm)
    {
        var active = Application.Current!.FindResource("AccentBrush") as IBrush ?? Brushes.Purple;
        var inactive = Application.Current!.FindResource("CardBgBrush") as IBrush ?? Brushes.Gray;
        var activeFg = Brushes.White;
        var inactiveFg = Application.Current!.FindResource("TextMutedBrush") as IBrush ?? Brushes.Gray;
        var secondaryActive = new SolidColorBrush(((SolidColorBrush)active).Color, 0.5);

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

        // Secondary sort buttons
        var s2NameBtn = this.FindControl<Button>("Sort2NameBtn");
        var s2RarityBtn = this.FindControl<Button>("Sort2RarityBtn");
        var s2SlotBtn = this.FindControl<Button>("Sort2SlotBtn");

        foreach (var (btn, mode) in new[] {
            (s2NameBtn, SortMode.Name), (s2RarityBtn, SortMode.Rarity),
            (s2SlotBtn, SortMode.Slot) })
        {
            if (btn == null) continue;
            btn.Background = vm.SecondarySort == mode ? secondaryActive : inactive;
            btn.Foreground = vm.SecondarySort == mode ? activeFg : inactiveFg;
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
        else if (btn.Name == "ThemeFilterBtn")
        {
            ShowThemeFilterPopup();
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

    private void BuildPoolThemeToggles(ItemEditorViewModel vm)
    {
        var poolPanel = this.FindControl<Avalonia.Controls.WrapPanel>("PoolToggles");
        var themePanel = this.FindControl<Avalonia.Controls.WrapPanel>("ThemeToggles");
        if (poolPanel == null || themePanel == null) return;

        var accent = Application.Current!.FindResource("AccentBrush") as IBrush ?? Brushes.Purple;
        var inactive = Application.Current!.FindResource("CardBgBrush") as IBrush ?? Brushes.Gray;
        var activeFg = Brushes.White;
        var inactiveFg = Application.Current!.FindResource("TextMutedBrush") as IBrush ?? Brushes.Gray;

        // Pool toggles
        foreach (var pool in new[] { "Clothes", "Armor", "Shields", "Hats", "Cloaks",
                                      "Gloves", "Boots", "Amulets", "Rings",
                                      "Weapons", "Weapons_1H", "Weapons_2H" })
        {
            var btn = new ToggleButton
            {
                Content = Loc.Instance.PoolName(pool),
                Tag = pool,
                IsChecked = true,
                FontSize = FontScale.Of(10),
                Padding = new Avalonia.Thickness(6, 2),
                CornerRadius = new Avalonia.CornerRadius(4),
                BorderThickness = new Avalonia.Thickness(0),
                Background = accent,
                Foreground = activeFg,
                Margin = new Avalonia.Thickness(0, 0, 3, 0)
            };
            btn.IsCheckedChanged += (s, _) =>
            {
                if (s is ToggleButton tb && tb.Tag is string p)
                {
                    var on = tb.IsChecked == true;
                    vm.SetPoolEnabled(p, on);
                    tb.Background = on ? accent : inactive;
                    tb.Foreground = on ? activeFg : inactiveFg;
                }
            };
            poolPanel.Children.Add(btn);
        }

        // Theme toggles
        foreach (var theme in ItemVM.AvailableThemes)
        {
            var btn = new ToggleButton
            {
                Content = Loc.Instance.ThemeName(theme),
                Tag = theme,
                IsChecked = true,
                FontSize = FontScale.Of(10),
                Padding = new Avalonia.Thickness(6, 2),
                CornerRadius = new Avalonia.CornerRadius(4),
                BorderThickness = new Avalonia.Thickness(0),
                Background = accent,
                Foreground = activeFg,
                Margin = new Avalonia.Thickness(0, 0, 3, 0)
            };
            btn.IsCheckedChanged += (s, _) =>
            {
                if (s is ToggleButton tb && tb.Tag is string t)
                {
                    var on = tb.IsChecked == true;
                    vm.SetThemeEnabled(t, on);
                    tb.Background = on ? accent : inactive;
                    tb.Foreground = on ? activeFg : inactiveFg;
                }
            };
            themePanel.Children.Add(btn);
        }
    }

    private void ShowThemeFilterPopup()
    {
        var popup = this.FindControl<Border>("ThemeFilterPopup");
        var container = this.FindControl<ItemsControl>("ThemeFilterCheckboxes");
        if (popup == null || container == null || DataContext is not ItemEditorViewModel vm) return;

        var panel = new StackPanel { Spacing = 2 };

        // Add "No theme" option
        var themes = new[] { "None" }.Concat(ItemVM.AvailableThemes);
        foreach (var theme in themes)
        {
            var cb = new CheckBox
            {
                Content = theme == "None" ? "No theme" : Loc.Instance.ThemeName(theme),
                IsChecked = !vm.HiddenThemes.Contains(theme),
                FontSize = FontScale.Of(12),
                Tag = theme
            };
            cb.IsCheckedChanged += OnThemeFilterCheckedChanged;
            panel.Children.Add(cb);
        }

        container.ItemsSource = null;
        container.Items.Clear();
        container.Items.Add(panel);

        popup.IsVisible = !popup.IsVisible;
    }

    private void OnThemeFilterCheckedChanged(object? sender, RoutedEventArgs e)
    {
        if (sender is not CheckBox cb || cb.Tag is not string theme) return;
        if (DataContext is not ItemEditorViewModel vm) return;
        vm.ToggleThemeFilter(theme);
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
