using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Controls;
using ParaTool.App.ViewModels;
using ParaTool.App.Localization;

namespace ParaTool.App.Views;

public partial class ConstructorView : UserControl
{
    private static SolidColorBrush ChipDefaultBg => Themes.ThemeBrushes.InputBg;
    private static SolidColorBrush ChipSelectedBg => Themes.ThemeBrushes.Accent;
    private static SolidColorBrush ChipTextDefault => Themes.ThemeBrushes.TextMuted;
    private static SolidColorBrush ChipTextSelected => Themes.ThemeBrushes.TextPrimary;
    private static SolidColorBrush ThemeSelectedBg => Themes.ThemeBrushes.CardBg;

    private static readonly Dictionary<string, SolidColorBrush> RarityBrushes = new()
    {
        ["Common"] = new(Color.Parse("#8A8494")),
        ["Uncommon"] = new(Color.Parse("#2ECC71")),
        ["Rare"] = new(Color.Parse("#3498DB")),
        ["VeryRare"] = new(Color.Parse("#9B59B6")),
        ["Legendary"] = new(Color.Parse("#C8A96E")),
    };

    // BG3 localization folder names with flag emojis
    private static readonly string[] Bg3Languages =
    [
        "English", "Russian", "German", "French", "Spanish", "LatinSpanish",
        "Italian", "Polish", "Japanese", "Korean", "Turkish", "Ukrainian",
        "Chinese", "ChineseTraditional", "BrazilianPortuguese"
    ];

    private static readonly Dictionary<string, string> Bg3ToCode = new()
    {
        ["English"] = "en", ["Russian"] = "ru", ["German"] = "de", ["French"] = "fr",
        ["Spanish"] = "es", ["LatinSpanish"] = "es", ["Italian"] = "it", ["Polish"] = "pl",
        ["Japanese"] = "ja", ["Korean"] = "ko", ["Turkish"] = "tr", ["Ukrainian"] = "uk",
        ["Chinese"] = "zh", ["ChineseTraditional"] = "zh", ["BrazilianPortuguese"] = "pt"
    };

    private TextBox? _lastFocusedLocaBox;
    private string _currentLocaLang = "en";
    private ConstructorViewModel? _subscribedVm;

    public ConstructorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        // Setup language selector
        var langSelector = this.FindControl<TumblerChipEditor>("LocaLangSelector");
        if (langSelector != null)
        {
            langSelector.Items = Bg3Languages;
            var uiCode = Loc.Instance.Lang;
            var defaultBg3 = Bg3ToCode.FirstOrDefault(x => x.Value == uiCode).Key ?? "English";
            langSelector.Text = defaultBg3;
            langSelector.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name == "Text" && s is TumblerChipEditor tc)
                    OnLocaLangChanged(tc.Text);
            };
        }

        // Toggle code/preview button
        var toggleBtn = this.FindControl<Button>("ToggleCodeViewBtn");
        if (toggleBtn != null)
            toggleBtn.Click += OnToggleCodeView;
    }

    private void OnToggleCodeView(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConstructorViewModel vm) return;
        vm.IsCodeView = !vm.IsCodeView;

        if (sender is Button btn)
        {
            btn.Content = vm.IsCodeView ? "\ud83d\udc41" : "</>";
            btn.Foreground = vm.IsCodeView
                ? Themes.ThemeBrushes.Accent
                : Themes.ThemeBrushes.TextMuted;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        // Unsubscribe from old VM
        if (_subscribedVm != null)
            _subscribedVm.PropertyChanged -= OnVmPropertyChanged;

        if (DataContext is ConstructorViewModel vm)
        {
            _subscribedVm = vm;
            vm.PropertyChanged += OnVmPropertyChanged;
        }
    }

    private void OnVmPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(ConstructorViewModel.SelectedArtifact))
            RebuildChips();
    }

    private void OnLocaLangChanged(string? bg3Lang)
    {
        if (bg3Lang == null) return;
        _currentLocaLang = Bg3ToCode.GetValueOrDefault(bg3Lang, "en");
        if (DataContext is ConstructorViewModel vm)
            vm.EditingLang = _currentLocaLang;
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

        // Boosty support link
        if (btn.Name == "BoostyLink")
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://boosty.to/paramonovgames",
                    UseShellExecute = true
                });
            }
            catch { }
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

        // Add status/spell to field
        if (btn.Name is "AddStatusBtn" or "AddSpellBtn" && DataContext is ConstructorViewModel addVm)
        {
            var statsType = btn.Name == "AddStatusBtn" ? "StatusData" : "SpellData";
            var entries = addVm.GetStatsOfType(statsType);
            // Find the TextBox sibling (first child of parent Grid)
            var parentGrid = btn.Parent as Avalonia.Controls.Grid;
            var targetTb = parentGrid?.Children.OfType<TextBox>().FirstOrDefault();
            if (targetTb != null && entries.Count > 0)
            {
                ShowFieldAutocomplete(btn, entries, targetTb);
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
        // Build entries list
        List<(string id, string display)> allEntries = [];

        // For passive/spell/status — use real parsed entries from resolver
        if (bbTag is "passive" or "spell" or "status" && DataContext is ConstructorViewModel cvm2)
        {
            var statsType = bbTag switch
            {
                "passive" => "PassiveData",
                "spell" => "SpellData",
                "status" => "StatusData",
                _ => ""
            };
            foreach (var name in cvm2.GetStatsOfType(statsType))
                allEntries.Add((name, name));
        }

        // For tip/resource — use LsTagDatabase
        if (bbTag is "tip" or "resource" || allEntries.Count == 0)
        {
            var allTags = ParaTool.Core.Localization.LsTagDatabase.Tooltips
                .Concat(ParaTool.Core.Localization.LsTagDatabase.ActionResources).ToArray();
            var filtered = bbTag == "tip" ? allTags.Where(t => t.Type == null)
                : bbTag == "resource" ? allTags.Where(t => t.Type == "ActionResource")
                : allTags.AsEnumerable();
            foreach (var t in filtered)
            {
                var label = Loc.Instance.Lang == "ru" && t.LabelRu != null ? t.LabelRu : t.Label;
                allEntries.Add((t.Tooltip, $"{label} ({t.Tooltip})"));
            }
        }

        // Build popup with search + scrollable list
        var popup = new Avalonia.Controls.Primitives.Popup
        {
            PlacementTarget = source,
            Placement = Avalonia.Controls.PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            MaxHeight = 400,
            MinWidth = 300,
        };

        var searchBox = new TextBox
        {
            Watermark = "Search...",
            FontSize = 12, Padding = new Avalonia.Thickness(8, 6),
            Background = Themes.ThemeBrushes.InputBg,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        };

        var listBox = new ListBox
        {
            MaxHeight = 300,
            Background = Avalonia.Media.Brushes.Transparent,
            BorderThickness = new Avalonia.Thickness(0),
        };

        // Populate
        void Filter(string query)
        {
            listBox.Items.Clear();
            var q = query.Trim();
            var source2 = string.IsNullOrEmpty(q)
                ? allEntries
                : allEntries.Where(e => e.display.Contains(q, StringComparison.OrdinalIgnoreCase)
                    || e.id.Contains(q, StringComparison.OrdinalIgnoreCase));

            // Custom option first
            var customBtn = new ListBoxItem { Content = "Custom (type your own)...", FontSize = 11, Foreground = Themes.ThemeBrushes.Accent };
            listBox.Items.Add(customBtn);

            foreach (var (id, display) in source2)
            {
                var item = new ListBoxItem { Content = display, Tag = id, FontSize = 11, Foreground = Themes.ThemeBrushes.TextSecondary };
                listBox.Items.Add(item);
            }
        }

        searchBox.TextChanged += (_, _) => Filter(searchBox.Text ?? "");
        Filter("");

        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is not ListBoxItem selected) return;

            popup.IsOpen = false;

            if (selected.Tag == null)
            {
                // Custom
                InsertBbCode(targetBox, bbTag);
                return;
            }

            var id = selected.Tag.ToString()!;
            var text = targetBox.Text ?? "";
            var pos = Math.Max(0, targetBox.SelectionStart);
            var insert = $"[{bbTag}={id}]{id}[/{bbTag}]";
            targetBox.Text = text[..pos] + insert + text[pos..];
            targetBox.CaretIndex = pos + insert.Length;
            targetBox.Focus();
        };

        var panel = new StackPanel
        {
            Children = { searchBox, listBox },
        };

        popup.Child = new Border
        {
            Child = panel,
            Background = Themes.ThemeBrushes.PanelBg,
            BorderBrush = Themes.ThemeBrushes.BorderSubtle,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(8),
        };

        // Must be in visual tree
        if (source.Parent is Avalonia.Controls.Panel parentPanel)
        {
            parentPanel.Children.Add(popup);
            popup.IsOpen = true;
            popup.Closed += (_, _) => parentPanel.Children.Remove(popup);
        }
        else
        {
            // Fallback: use overlay panel
            var overlayParent = this.FindControl<Avalonia.Controls.Panel>("BbToolbar")?.Parent as Avalonia.Controls.Panel;
            if (overlayParent != null)
            {
                overlayParent.Children.Add(popup);
                popup.IsOpen = true;
                popup.Closed += (_, _) => overlayParent.Children.Remove(popup);
            }
        }
    }

    private void ShowFieldAutocomplete(Button source, List<string> entries, TextBox targetBox)
    {
        var popup = new Avalonia.Controls.Primitives.Popup
        {
            PlacementTarget = source,
            Placement = Avalonia.Controls.PlacementMode.Bottom,
            IsLightDismissEnabled = true,
            MaxHeight = 350, MinWidth = 250,
        };

        var searchBox = new TextBox
        {
            Watermark = "Search...", FontSize = 12,
            Padding = new Avalonia.Thickness(8, 6),
            Background = Themes.ThemeBrushes.InputBg,
            Margin = new Avalonia.Thickness(0, 0, 0, 4),
        };

        var listBox = new ListBox
        {
            MaxHeight = 280, Background = Avalonia.Media.Brushes.Transparent, BorderThickness = new Avalonia.Thickness(0),
        };

        void Filter(string q)
        {
            listBox.Items.Clear();
            var source2 = string.IsNullOrEmpty(q?.Trim())
                ? entries
                : entries.Where(e => e.Contains(q!.Trim(), StringComparison.OrdinalIgnoreCase));
            foreach (var e in source2)
                listBox.Items.Add(new ListBoxItem { Content = e, Tag = e, FontSize = 11, Foreground = Themes.ThemeBrushes.TextSecondary });
        }

        searchBox.TextChanged += (_, _) => Filter(searchBox.Text ?? "");
        Filter("");

        listBox.SelectionChanged += (_, _) =>
        {
            if (listBox.SelectedItem is not ListBoxItem sel || sel.Tag is not string id) return;
            popup.IsOpen = false;
            var current = targetBox.Text?.Trim() ?? "";
            targetBox.Text = string.IsNullOrEmpty(current) ? id : $"{current};{id}";
        };

        popup.Child = new Border
        {
            Child = new StackPanel { Children = { searchBox, listBox } },
            Background = Themes.ThemeBrushes.PanelBg,
            BorderBrush = Themes.ThemeBrushes.BorderSubtle,
            BorderThickness = new Avalonia.Thickness(1),
            CornerRadius = new Avalonia.CornerRadius(8),
            Padding = new Avalonia.Thickness(8),
        };

        if (source.Parent is Avalonia.Controls.Panel parentPanel)
        {
            parentPanel.Children.Add(popup);
            popup.IsOpen = true;
            popup.Closed += (_, _) => parentPanel.Children.Remove(popup);
        }
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
                BorderBrush = isSelected ? ChipSelectedBg : Themes.ThemeBrushes.CardBg,
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
                BorderBrush = isSelected ? Themes.ThemeBrushes.Accent : Themes.ThemeBrushes.CardBg,
            };
            btn.Click += (_, _) => { vm.ToggleThemeCommand.Execute(btn.Tag as string); RebuildChips(); };
            panel.Children.Add(btn);
        }
    }
}
