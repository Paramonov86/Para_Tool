using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.ViewModels;
using ParaTool.App.Localization;

namespace ParaTool.App.Views;

public partial class ConstructorView : UserControl
{
    private static readonly SolidColorBrush ChipDefaultBg = new(Color.Parse("#252330"));
    private static readonly SolidColorBrush ChipSelectedBg = new(Color.Parse("#6C5CE7"));
    private static readonly SolidColorBrush ChipTextDefault = new(Color.Parse("#8A8494"));
    private static readonly SolidColorBrush ChipTextSelected = new(Color.Parse("#E0DDE6"));
    private static readonly SolidColorBrush ThemeSelectedBg = new(Color.Parse("#3D3A4D"));

    private static readonly Dictionary<string, SolidColorBrush> RarityBrushes = new()
    {
        ["Common"] = new(Color.Parse("#8A8494")),
        ["Uncommon"] = new(Color.Parse("#2ECC71")),
        ["Rare"] = new(Color.Parse("#3498DB")),
        ["VeryRare"] = new(Color.Parse("#9B59B6")),
        ["Legendary"] = new(Color.Parse("#C8A96E")),
    };

    // BG3 localization folder names
    private static readonly string[] Bg3Languages =
    [
        "English", "Russian", "German", "French", "Spanish", "LatinSpanish",
        "Italian", "Polish", "Japanese", "Korean", "Turkish", "Ukrainian",
        "Chinese", "ChineseTraditional", "BrazilianPortuguese"
    ];

    // BG3 folder name → ParaTool lang code
    private static readonly Dictionary<string, string> Bg3ToCode = new()
    {
        ["English"] = "en", ["Russian"] = "ru", ["German"] = "de", ["French"] = "fr",
        ["Spanish"] = "es", ["LatinSpanish"] = "es", ["Italian"] = "it", ["Polish"] = "pl",
        ["Japanese"] = "ja", ["Korean"] = "ko", ["Turkish"] = "tr", ["Ukrainian"] = "uk",
        ["Chinese"] = "zh", ["ChineseTraditional"] = "zh", ["BrazilianPortuguese"] = "pt"
    };

    private TextBox? _lastFocusedLocaBox;
    private string _currentLocaLang = "en";

    public ConstructorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        // Setup language selector
        var langSelector = this.FindControl<ComboBox>("LocaLangSelector");
        if (langSelector != null)
        {
            langSelector.ItemsSource = Bg3Languages;
            // Default to UI language
            var uiCode = Loc.Instance.Lang;
            var defaultBg3 = Bg3ToCode.FirstOrDefault(x => x.Value == uiCode).Key ?? "English";
            langSelector.SelectedItem = defaultBg3;
            langSelector.SelectionChanged += OnLocaLangChanged;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (DataContext is ConstructorViewModel vm)
        {
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(vm.SelectedArtifact))
                    RebuildChips();
            };
        }
    }

    private void OnLocaLangChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (sender is ComboBox cb && cb.SelectedItem is string bg3Lang)
        {
            _currentLocaLang = Bg3ToCode.GetValueOrDefault(bg3Lang, "en");
            // Only change editing language, NOT the app UI language
            if (DataContext is ConstructorViewModel vm)
                vm.EditingLang = _currentLocaLang;
        }
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        // Enter in single-line TextBox → lose focus
        if (e.Key == Key.Enter && e.Source is TextBox tb && !tb.AcceptsReturn)
        {
            this.Focus();
            e.Handled = true;
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        // Track last focused loca TextBox for BB-code toolbar
        if (e.Source is TextBox tb)
        {
            var name = tb.Name;
            if (name is "LocaDisplayName" or "LocaDescription" or "LocaPassiveName" or "LocaPassiveDesc")
                _lastFocusedLocaBox = tb;
        }
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn) return;

        // Nav group toggle
        if (btn.Name == "NavGroupToggle" && btn.Tag is NavGroupVM gvm)
        {
            gvm.IsExpanded = !gvm.IsExpanded;
            return;
        }

        // Passive toggle
        if (btn.Name == "PassiveToggleBtn" && btn.Tag is PassiveVM pvm)
        {
            pvm.IsExpanded = !pvm.IsExpanded;
            return;
        }

        // Create new artifact
        if (btn.Name == "CreateNewArtifactBtn" && DataContext is ConstructorViewModel cvm)
        {
            var nameInput = this.FindControl<TextBox>("NewArtifactNameInput");
            if (nameInput != null && !string.IsNullOrWhiteSpace(nameInput.Text))
            {
                cvm.CreateNewArtifactCommand.Execute(nameInput.Text);
                nameInput.Text = "";
            }
            return;
        }

        // Icon grid click
        if (btn.Name == "IconGridBtn" && btn.Tag is IconEntryVM iconVm
            && DataContext is ConstructorViewModel ctorVm && ctorVm.IconBrowser != null)
        {
            ctorVm.IconBrowser.SelectIcon(iconVm);
            return;
        }

        // BB-code toolbar buttons
        if (btn.Classes.Contains("BbBtn") && btn.Tag is string tag && _lastFocusedLocaBox != null)
        {
            // Tags that need autocomplete popup
            if (tag is "status" or "spell" or "passive" or "resource" or "tip")
            {
                ShowBbAutocomplete(btn, tag, _lastFocusedLocaBox);
                return;
            }
            InsertBbCode(_lastFocusedLocaBox, tag);
            return;
        }
    }

    private void ShowBbAutocomplete(Button source, string bbTag, TextBox targetBox)
    {
        // For passive/spell/status — show real entries from resolver
        if (bbTag is "passive" or "spell" or "status" && DataContext is ConstructorViewModel cvm2)
        {
            var statsType = bbTag switch
            {
                "passive" => "PassiveData",
                "spell" => "SpellData",
                "status" => "StatusData",
                _ => ""
            };
            var entries = cvm2.GetStatsOfType(statsType);
            if (entries.Count > 0)
            {
                ShowStatsAutocomplete(source, bbTag, targetBox, entries);
                return;
            }
        }

        var allTags = ParaTool.Core.Localization.LsTagDatabase.Tooltips
            .Concat(ParaTool.Core.Localization.LsTagDatabase.ActionResources).ToArray();
        var items = bbTag switch
        {
            "tip" => allTags.Where(t => t.Type == null).ToArray(),
            "resource" => ParaTool.Core.Localization.LsTagDatabase.ActionResources,
            _ => allTags,
        };

        // Also allow custom input
        var menu = new ContextMenu();

        // Add "Custom..." option
        var customItem = new MenuItem { Header = "Custom (type your own)..." };
        customItem.Click += (_, _) =>
        {
            InsertBbCode(targetBox, bbTag);
        };
        menu.Items.Add(customItem);
        menu.Items.Add(new Separator());

        foreach (var tag in items.Take(50)) // Limit for performance
        {
            var label = Loc.Instance.Lang == "ru" && tag.LabelRu != null ? tag.LabelRu : tag.Label;
            var item = new MenuItem { Header = $"{label} ({tag.Tooltip})", Tag = tag };
            item.Click += (_, _) =>
            {
                var t = (ParaTool.Core.Localization.LsTagDatabase.TagInfo)item.Tag!;
                var bbTagName = ParaTool.Core.Localization.LsTagDatabase.BbTagForType(t.Type);
                var displayText = Loc.Instance.Lang == "ru" && t.LabelRu != null ? t.LabelRu : t.Label;

                var selStart = targetBox.SelectionStart;
                var text = targetBox.Text ?? "";
                var selected = "";
                if (targetBox.SelectionStart != targetBox.SelectionEnd)
                {
                    var s = Math.Min(targetBox.SelectionStart, targetBox.SelectionEnd);
                    var e2 = Math.Max(targetBox.SelectionStart, targetBox.SelectionEnd);
                    selected = text[s..e2];
                }
                var content = selected.Length > 0 ? selected : displayText;
                var insert = $"[{bbTagName}={t.Tooltip}]{content}[/{bbTagName}]";

                var pos = Math.Max(0, selStart);
                if (selected.Length > 0)
                {
                    var s = Math.Min(targetBox.SelectionStart, targetBox.SelectionEnd);
                    var e2 = Math.Max(targetBox.SelectionStart, targetBox.SelectionEnd);
                    targetBox.Text = text[..s] + insert + text[e2..];
                }
                else
                {
                    targetBox.Text = text[..pos] + insert + text[pos..];
                }
                targetBox.CaretIndex = pos + insert.Length;
                targetBox.Focus();
            };
            menu.Items.Add(item);
        }

        menu.Open(source);
    }

    private void ShowStatsAutocomplete(Button source, string bbTag, TextBox targetBox, List<string> entries)
    {
        var menu = new ContextMenu();

        var customItem = new MenuItem { Header = "Custom..." };
        customItem.Click += (_, _) => InsertBbCode(targetBox, bbTag);
        menu.Items.Add(customItem);
        menu.Items.Add(new Separator());

        foreach (var entry in entries.Take(50))
        {
            var item = new MenuItem { Header = entry, Tag = entry };
            item.Click += (_, _) =>
            {
                var id = item.Tag?.ToString() ?? "";
                var text = targetBox.Text ?? "";
                var pos = Math.Max(0, targetBox.SelectionStart);
                var insert = $"[{bbTag}={id}]{id}[/{bbTag}]";
                targetBox.Text = text[..pos] + insert + text[pos..];
                targetBox.CaretIndex = pos + insert.Length;
                targetBox.Focus();
            };
            menu.Items.Add(item);
        }

        menu.Open(source);
    }

    private void InsertBbCode(TextBox tb, string tag)
    {
        var selStart = tb.SelectionStart;
        var selEnd = tb.SelectionEnd;
        var text = tb.Text ?? "";
        var selected = "";

        if (selStart != selEnd && selStart >= 0 && selEnd >= 0)
        {
            var start = Math.Min(selStart, selEnd);
            var end = Math.Max(selStart, selEnd);
            selected = text[start..end];
        }

        string insert;
        int cursorOffset;

        switch (tag)
        {
            case "b":
                insert = $"[b]{selected}[/b]";
                cursorOffset = selected.Length > 0 ? insert.Length : 3;
                break;
            case "i":
                insert = $"[i]{selected}[/i]";
                cursorOffset = selected.Length > 0 ? insert.Length : 3;
                break;
            case "br":
                insert = "[br]";
                cursorOffset = 4;
                break;
            case "p":
                insert = "[p1]";
                cursorOffset = 4;
                break;
            case "status":
                insert = $"[status=STATUS_ID]{selected}[/status]";
                cursorOffset = 8; // select STATUS_ID
                break;
            case "tip":
                insert = $"[tip=TooltipName]{selected}[/tip]";
                cursorOffset = 5; // select TooltipName
                break;
            case "spell":
                insert = $"[spell=SpellId]{selected}[/spell]";
                cursorOffset = 7; // select SpellId
                break;
            case "passive":
                insert = $"[passive=PassiveId]{selected}[/passive]";
                cursorOffset = 9; // select PassiveId
                break;
            case "resource":
                insert = $"[resource=ResourceId]{selected}[/resource]";
                cursorOffset = 10; // select ResourceId
                break;
            default:
                return;
        }

        if (selected.Length > 0)
        {
            var start = Math.Min(selStart, selEnd);
            var end = Math.Max(selStart, selEnd);
            tb.Text = text[..start] + insert + text[end..];
            tb.CaretIndex = start + insert.Length;
        }
        else
        {
            var pos = Math.Max(0, selStart);
            tb.Text = text[..pos] + insert + text[pos..];
            tb.CaretIndex = pos + cursorOffset;
        }

        tb.Focus();
    }

    // === Chips ===

    private void RebuildChips()
    {
        var vm = DataContext as ConstructorViewModel;
        var art = vm?.SelectedArtifact;
        if (art == null || vm == null) return;

        BuildRarityChips(art, vm);
        BuildPoolChips(art, vm);
        BuildThemeChips(art, vm);
    }

    private void BuildRarityChips(ArtifactItemVM art, ConstructorViewModel vm)
    {
        var panel = this.FindControl<WrapPanel>("RarityChips");
        if (panel == null) return;
        panel.Children.Clear();
        foreach (var rarity in ArtifactItemVM.RarityOptions)
        {
            var isSelected = art.EditRarity == rarity;
            var brush = RarityBrushes.GetValueOrDefault(rarity, ChipTextDefault);
            var btn = new Button
            {
                Content = Loc.Instance.RarityName(rarity), Tag = rarity,
                FontSize = 12, FontWeight = FontWeight.SemiBold,
                Padding = new Thickness(12, 5), Margin = new Thickness(0, 0, 4, 4),
                CornerRadius = new CornerRadius(12),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = isSelected ? brush : ChipDefaultBg,
                Foreground = isSelected ? ChipTextSelected : brush,
                BorderThickness = new Thickness(1.5), BorderBrush = brush,
            };
            btn.Click += (_, _) => { vm.SetRarityCommand.Execute(btn.Tag as string); RebuildChips(); };
            panel.Children.Add(btn);
        }
    }

    private void BuildPoolChips(ArtifactItemVM art, ConstructorViewModel vm)
    {
        var panel = this.FindControl<WrapPanel>("PoolChips");
        if (panel == null) return;
        panel.Children.Clear();
        foreach (var pool in ArtifactItemVM.PoolOptions)
        {
            var isSelected = art.EditPool == pool;
            var btn = new Button
            {
                Content = Loc.Instance.PoolName(pool), Tag = pool,
                FontSize = 11, Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(10),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = isSelected ? ChipSelectedBg : ChipDefaultBg,
                Foreground = isSelected ? ChipTextSelected : ChipTextDefault,
                BorderThickness = new Thickness(isSelected ? 1.5 : 1),
                BorderBrush = isSelected ? ChipSelectedBg : new SolidColorBrush(Color.Parse("#3D3A4D")),
            };
            btn.Click += (_, _) => { vm.SetPoolCommand.Execute(btn.Tag as string); RebuildChips(); };
            panel.Children.Add(btn);
        }
    }

    private void BuildThemeChips(ArtifactItemVM art, ConstructorViewModel vm)
    {
        var panel = this.FindControl<WrapPanel>("ThemeChips");
        if (panel == null) return;
        panel.Children.Clear();
        foreach (var theme in ArtifactItemVM.ThemeOptions)
        {
            var isSelected = art.IsThemeSelected(theme);
            var btn = new Button
            {
                Content = Loc.Instance.ThemeName(theme), Tag = theme,
                FontSize = 11, Padding = new Thickness(10, 4),
                Margin = new Thickness(0, 0, 4, 4), CornerRadius = new CornerRadius(10),
                Cursor = new Cursor(StandardCursorType.Hand),
                Background = isSelected ? ThemeSelectedBg : ChipDefaultBg,
                Foreground = isSelected ? ChipTextSelected : ChipTextDefault,
                BorderThickness = new Thickness(isSelected ? 1.5 : 1),
                BorderBrush = isSelected ? new SolidColorBrush(Color.Parse("#6C5CE7")) : new SolidColorBrush(Color.Parse("#3D3A4D")),
            };
            btn.Click += (_, _) => { vm.ToggleThemeCommand.Execute(btn.Tag as string); RebuildChips(); };
            panel.Children.Add(btn);
        }
    }
}
