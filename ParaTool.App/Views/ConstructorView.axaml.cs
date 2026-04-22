
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using ParaTool.App.Controls;
using ParaTool.App.ViewModels;
using ParaTool.App.Localization;
using ParaTool.App.Services;
using Avalonia.VisualTree;

namespace ParaTool.App.Views;

public partial class ConstructorView : UserControl
{
    private static SolidColorBrush ChipDefaultBg => Themes.ThemeBrushes.InputBg;
    private static SolidColorBrush ChipSelectedBg => Themes.ThemeBrushes.Accent;
    private static SolidColorBrush ChipTextDefault => Themes.ThemeBrushes.TextMuted;
    private static SolidColorBrush ChipTextSelected => Themes.ThemeBrushes.TextPrimary;
    private static SolidColorBrush ThemeSelectedBg => Themes.ThemeBrushes.CardBg;

    private static SolidColorBrush GetRarityBrush(string rarity) => Themes.ThemeBrushes.GetRarity(rarity);

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
    private int _lastSelStart, _lastSelEnd;
    private string _currentLocaLang = "en";
    private ConstructorViewModel? _subscribedVm;

    private readonly Dictionary<TextBlock, System.Threading.CancellationTokenSource> _marqueeTokens = new();

    public ConstructorView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
        Loc.Instance.PropertyChanged += (_, _) => Avalonia.Threading.Dispatcher.UIThread.Post(RebuildChips);
        FontScale.ScaleChanged += RebuildChips;
        AddHandler(GotFocusEvent, OnGotFocus, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        AddHandler(Avalonia.Input.InputElement.PointerPressedEvent, OnPointerPressedTunnel, RoutingStrategies.Tunnel);
        AddHandler(PointerEnteredEvent, OnIconPointerEntered, RoutingStrategies.Tunnel);
        AddHandler(PointerExitedEvent, OnIconPointerExited, RoutingStrategies.Tunnel);

        // Passive picker — add existing passive when selected
        var passivePicker = this.FindControl<SearchPickerChip>("PassivePickerChip");
        if (passivePicker != null)
        {
            passivePicker.PropertyChanged += (s, e) =>
            {
                if (e.Property.Name != "Text" || s is not SearchPickerChip picker) return;
                var name = picker.Text?.Trim();
                if (string.IsNullOrEmpty(name)) return;
                if (DataContext is ConstructorViewModel vm && vm.SelectedArtifact != null)
                {
                    vm.SelectedArtifact.AddExistingPassive(name, vm.StatsResolver, vm.LocaService);
                    picker.Text = "";
                }
            };
        }
        // Wire up resolver/loca for passive picker when DataContext is set
        DataContextChanged += (_, _) =>
        {
            if (passivePicker != null && DataContext is ConstructorViewModel cvm)
            {
                passivePicker.Resolver = cvm.StatsResolver;
                passivePicker.LocaService = cvm.LocaService;
            }
        };

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

        // Spell/Status rename via context menu (global events from any chip editor)
        ChipListEditor.GlobalRenameRequested += statId => OnRenameChip(statId, isSpell: DetectIsSpell(statId));
        BoostBlocksEditor.GlobalRenameRequested += statId => OnRenameChip(statId, isSpell: DetectIsSpell(statId));

        // Toggle code/preview button
        var toggleBtn = this.FindControl<Button>("ToggleCodeViewBtn");
        if (toggleBtn != null)
        {
            toggleBtn.Content = "\ud83d\udc41"; // eye = preview mode (default)
            toggleBtn.Click += OnToggleCodeView;
        }
    }

    private void OnToggleCodeView(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConstructorViewModel vm) return;
        vm.IsCodeView = !vm.IsCodeView;

        if (sender is Button btn)
        {
            // Eye = preview mode, </> = code mode
            btn.Content = vm.IsCodeView ? "</>" : "\ud83d\udc41";
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

    private bool DetectIsSpell(string statId)
    {
        if (DataContext is not ConstructorViewModel vm || vm.StatsResolver == null) return false;
        var entry = vm.StatsResolver.Get(statId);
        return entry?.Type is "SpellData";
    }

    private async void OnRenameChip(string statId, bool isSpell)
    {
        if (DataContext is not ConstructorViewModel vm || vm.SelectedArtifact == null) return;
        var art = vm.SelectedArtifact.Artifact;
        var renames = isSpell ? art.SpellRenames : art.StatusRenames;
        var editLang = vm.EditingLang; // use constructor's editing language, not UI lang

        // Get current rename or resolve existing name
        var currentName = "";
        if (renames.TryGetValue(statId, out var existing))
            currentName = existing.GetValueOrDefault(editLang) ?? "";
        if (string.IsNullOrEmpty(currentName))
        {
            currentName = Controls.SearchPickerChip.ResolveStatDisplayName(statId, editLang,
                vm.StatsResolver, vm.LocaService) ?? statId;
        }

        // Show inline rename dialog
        var dialog = new Window
        {
            Title = Loc.Instance.DlgRenameTitle,
            Width = 400, Height = 120,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            CanResize = false,
            Background = Themes.ThemeBrushes.PanelBg,
        };
        var input = new TextBox
        {
            Text = currentName,
            FontSize = 14,
            Margin = new Thickness(16, 16, 16, 8),
            Watermark = Loc.Instance.WmNewName,
        };
        var okBtn = new Button
        {
            Content = "OK", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right,
            Margin = new Thickness(0, 0, 16, 8), Padding = new Thickness(20, 4),
            Background = Themes.ThemeBrushes.Accent,
        };
        var stack = new StackPanel();
        stack.Children.Add(input);
        stack.Children.Add(okBtn);
        dialog.Content = stack;

        string? result = null;
        okBtn.Click += (_, _) => { result = input.Text; dialog.Close(); };
        input.KeyUp += (_, e) => { if (e.Key == Avalonia.Input.Key.Enter) { result = input.Text; dialog.Close(); } };

        var owner = TopLevel.GetTopLevel(this) as Window;
        if (owner != null)
            await dialog.ShowDialog(owner);
        else
            return;

        if (result == null) return; // cancelled

        var newName = result.Trim();
        if (string.IsNullOrEmpty(newName) || newName == statId)
        {
            // Clear rename
            renames.Remove(statId);
        }
        else
        {
            // Save for editing language
            if (!renames.ContainsKey(statId))
                renames[statId] = new Dictionary<string, string>();
            renames[statId][editLang] = newName;
            // If no EN, copy to EN too as fallback
            if (editLang != "en" && !renames[statId].ContainsKey("en"))
                renames[statId]["en"] = newName;
        }

        vm.SelectedArtifact.IsDirty = true;
        // Update active renames reference (may be first rename on this artifact)
        Controls.BoostBlocksEditor.ActiveSpellRenames = art.SpellRenames;
        Controls.BoostBlocksEditor.ActiveStatusRenames = art.StatusRenames;
        vm.SelectedArtifact.RefreshAll();
        Controls.BoostBlocksEditor.InvokeGlobalForceRebuild();
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
        // Enter in single-line TextBox → lose focus (but not inside ChipListEditor — it uses Enter to add chips)
        if (e.Key == Key.Enter && e.Source is TextBox tb && !tb.AcceptsReturn)
        {
            // Skip if TextBox is inside ChipListEditor (Enter adds chip there)
            var parent = (tb as Avalonia.Visual)?.GetVisualParent();
            while (parent != null)
            {
                if (parent is ChipListEditor) return; // let ChipListEditor handle Enter
                parent = parent.GetVisualParent();
            }
            (TopLevel.GetTopLevel(this) as Window)?.FocusManager?.ClearFocus();
            e.Handled = true;
        }
    }

    private void OnPointerPressedTunnel(object? sender, Avalonia.Input.PointerPressedEventArgs e)
    {
        // When clicking a BB button, save current TextBox selection before focus changes
        if (_lastFocusedLocaBox != null && _lastFocusedLocaBox.IsFocused)
        {
            _lastSelStart = _lastFocusedLocaBox.SelectionStart;
            _lastSelEnd = _lastFocusedLocaBox.SelectionEnd;
        }

        // Click on tab border → switch to that item (but not on close button)
        if (e.Source is Avalonia.Visual visual)
        {
            // Skip if clicking the close button
            var btn = visual as Button ?? visual.FindAncestorOfType<Button>();
            if (btn?.Name == "CloseTabBtn") return;

            var border = visual as Border ?? visual.FindAncestorOfType<Border>();
            if (border?.Name == "TabBorder" && border.DataContext is ArtifactItemVM tabItem
                && DataContext is ConstructorViewModel vm)
            {
                vm.SelectedArtifact = tabItem;
                e.Handled = true;
            }
        }
    }

    private void OnGotFocus(object? sender, GotFocusEventArgs e)
    {
        // Track last focused loca TextBox for BB-code toolbar
        if (e.Source is TextBox tb)
        {
            var name = tb.Name;
            // Named loca boxes
            if (name is "LocaDisplayName" or "LocaDescription")
            {
                _lastFocusedLocaBox = tb;
                return;
            }
            // Unnamed TextBox inside preview/loca section (passive name/desc in DataTemplate)
            if (string.IsNullOrEmpty(name) && tb.AcceptsReturn == false
                && tb.Background == Avalonia.Media.Brushes.Transparent && tb.BorderThickness == new Thickness(0))
            {
                _lastFocusedLocaBox = tb;
                return;
            }
            // AcceptsReturn TextBox = description editor
            if (tb.AcceptsReturn)
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

        // Open artifacts folder in system file explorer
        if (btn.Name == "OpenArtifactsFolderBtn")
        {
            try
            {
                var dir = ParaTool.Core.Artifacts.ArtifactStore.GetArtifactsDir();
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                ParaTool.Core.Services.AppLogger.Warn($"OpenArtifactsFolder failed: {ex.Message}");
            }
            return;
        }

        // Close recent tab
        if (btn.Name == "CloseTabBtn" && btn.Tag is ArtifactItemVM tabItem
            && DataContext is ConstructorViewModel tabVm)
        {
            tabVm.CloseTab(tabItem);
            return;
        }

        // Remove passive
        if (btn.Name == "RemovePassiveBtn" && btn.Tag is PassiveVM removePvm
            && DataContext is ConstructorViewModel rmVm && rmVm.SelectedArtifact != null)
        {
            rmVm.SelectedArtifact.RemovePassive(removePvm);
            return;
        }

        // Add new empty passive
        if (btn.Name == "AddPassiveBtn" && DataContext is ConstructorViewModel addPVm
            && addPVm.SelectedArtifact != null)
        {
            addPVm.SelectedArtifact.AddNewPassive();
            return;
        }

        // Passive picker — add existing passive by name
        if (btn.Name == "PassivePickerChip") return; // handled via PropertyChanged below

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

        // Upload custom PNG icon
        if (btn.Name == "UploadPngBtn" && DataContext is ConstructorViewModel uploadVm)
        {
            _ = UploadPngIconAsync(uploadVm);
            return;
        }

        // Icon grid click — identify by Tag type (Name doesn't propagate in DataTemplates)
        if (btn.Tag is IconEntryVM iconVm
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

    private async Task UploadPngIconAsync(ConstructorViewModel vm)
    {
        if (vm.SelectedArtifact == null) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(
            new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = Loc.Instance.DlgSelectPng,
                AllowMultiple = false,
                FileTypeFilter =
                [
                    new Avalonia.Platform.Storage.FilePickerFileType("PNG images") { Patterns = ["*.png"] }
                ]
            });

        if (files.Count == 0) return;

        try
        {
            await using var stream = await files[0].OpenReadAsync();
            using var ms = new System.IO.MemoryStream();
            await stream.CopyToAsync(ms);
            var pngData = ms.ToArray();

            var iconSet = ParaTool.Core.Textures.IconConverter.ConvertPng(pngData);

            var art = vm.SelectedArtifact.Artifact;
            art.IconMainDdsBase64 = Convert.ToBase64String(iconSet.MainDds);
            art.IconConsoleDdsBase64 = Convert.ToBase64String(iconSet.ConsoleDds);

            // Add 144×144 icon to persistent atlas store
            var atlasMapKey = art.StatId;
            ParaTool.Core.Textures.AtlasStore.AddIcon(atlasMapKey, iconSet.AtlasRgba);
            art.AtlasIconMapKey = atlasMapKey;

            // Update preview bitmap from the 380×380 DDS
            var decoded = ParaTool.Core.Textures.DdsReader.Decode(iconSet.MainDds);
            var bitmap = new Avalonia.Media.Imaging.WriteableBitmap(
                new Avalonia.PixelSize(decoded.width, decoded.height),
                new Avalonia.Vector(96, 96),
                Avalonia.Platform.PixelFormats.Rgba8888,
                Avalonia.Platform.AlphaFormat.Unpremul);

            using (var buf = bitmap.Lock())
            {
                System.Runtime.InteropServices.Marshal.Copy(
                    decoded.rgba, 0, buf.Address, decoded.rgba.Length);
            }

            vm.SelectedArtifact.IconBitmap = bitmap;
            vm.SelectedArtifact.IsDirty = true;
        }
        catch (Exception ex)
        {
            Core.Services.AppLogger.Warn($"PNG upload error: {ex}");
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
            PlacementAnchor = Avalonia.Controls.Primitives.PopupPositioning.PopupAnchor.Bottom,
            PlacementGravity = Avalonia.Controls.Primitives.PopupPositioning.PopupGravity.Bottom,
            IsLightDismissEnabled = true,
            MaxHeight = 400,
            MinWidth = 300,
        };

        var searchBox = new TextBox
        {
            Watermark = Loc.Instance.WmSearch,
            FontSize = FontScale.Of(12), Padding = new Avalonia.Thickness(8, 6),
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
            var customBtn = new ListBoxItem { Content = Loc.Instance.LblCustomOption, FontSize = FontScale.Of(11), Foreground = Themes.ThemeBrushes.Accent };
            listBox.Items.Add(customBtn);

            foreach (var (id, display) in source2)
            {
                var item = new ListBoxItem { Content = display, Tag = id, FontSize = FontScale.Of(11), Foreground = Themes.ThemeBrushes.TextSecondary };
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
            var start = Math.Min(_lastSelStart, _lastSelEnd);
            var end = Math.Max(_lastSelStart, _lastSelEnd);
            if (start < 0) start = 0;
            if (end > text.Length) end = text.Length;
            var selText = start != end ? text[start..end] : id;
            var insert = $"[{bbTag}={id}]{selText}[/{bbTag}]";
            targetBox.Text = start != end
                ? text[..start] + insert + text[end..]
                : text[..start] + insert + text[start..];
            targetBox.CaretIndex = start + insert.Length;
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
            Watermark = Loc.Instance.WmSearch, FontSize = FontScale.Of(12),
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
                listBox.Items.Add(new ListBoxItem { Content = e, Tag = e, FontSize = FontScale.Of(11), Foreground = Themes.ThemeBrushes.TextSecondary });
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
        // Use saved selection (live selection resets when BB button clicked)
        var selStart = _lastSelStart;
        var selEnd = _lastSelEnd;
        var text = tb.Text ?? "";
        var selected = "";

        if (selStart != selEnd && selStart >= 0 && selEnd >= 0
            && Math.Min(selStart, selEnd) >= 0 && Math.Max(selStart, selEnd) <= text.Length)
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
            var brush = GetRarityBrush(rarity);
            var btn = new Button
            {
                Content = Loc.Instance.RarityName(rarity), Tag = rarity,
                FontSize = FontScale.Of(12), FontWeight = FontWeight.SemiBold,
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
                FontSize = FontScale.Of(11), Padding = new Thickness(10, 4),
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
                FontSize = FontScale.Of(11), Padding = new Thickness(10, 4),
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

    // === Icon name marquee scroll on hover ===

    private void OnIconPointerEntered(object? sender, PointerEventArgs e)
    {
        if (e.Source is not Visual v) return;
        var btn = v.FindAncestorOfType<Button>();
        if (btn?.Name != "IconGridBtn") return;

        var tb = FindDescendant<TextBlock>(btn);
        if (tb == null || tb.RenderTransform is not TranslateTransform tt) return;

        tb.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        var textWidth = tb.DesiredSize.Width;
        var containerWidth = 100.0;
        if (textWidth <= containerWidth) return;

        var cts = new System.Threading.CancellationTokenSource();
        _marqueeTokens[tb] = cts;
        _ = RunMarquee(tb, tt, textWidth, containerWidth, cts.Token);
    }

    private void OnIconPointerExited(object? sender, PointerEventArgs e)
    {
        if (e.Source is not Visual v) return;
        var btn = v.FindAncestorOfType<Button>();
        if (btn?.Name != "IconGridBtn") return;

        var tb = FindDescendant<TextBlock>(btn);
        if (tb == null) return;

        if (_marqueeTokens.Remove(tb, out var cts))
            cts.Cancel();
        if (tb.RenderTransform is TranslateTransform tt)
            tt.X = 0;
    }

    private static async System.Threading.Tasks.Task RunMarquee(
        TextBlock tb, TranslateTransform tt, double textWidth, double containerWidth,
        System.Threading.CancellationToken ct)
    {
        var offset = textWidth - containerWidth;
        var durationMs = (int)(offset * 20); // ~20ms per pixel
        var steps = Math.Max(1, durationMs / 16);
        var dx = offset / steps;

        await System.Threading.Tasks.Task.Delay(300, ct); // pause before scroll

        // Scroll left
        for (int i = 0; i < steps && !ct.IsCancellationRequested; i++)
        {
            tt.X = -(i + 1) * dx;
            await System.Threading.Tasks.Task.Delay(16, ct);
        }

        if (!ct.IsCancellationRequested)
            await System.Threading.Tasks.Task.Delay(800, ct); // pause at end

        // Scroll back
        for (int i = steps - 1; i >= 0 && !ct.IsCancellationRequested; i--)
        {
            tt.X = -i * dx;
            await System.Threading.Tasks.Task.Delay(16, ct);
        }
    }

    private static T? FindDescendant<T>(Visual parent) where T : Visual
    {
        foreach (var child in parent.GetVisualChildren())
        {
            if (child is T match) return match;
            var found = FindDescendant<T>(child as Visual ?? (Visual)child);
            if (found != null) return found;
        }
        return null;
    }
}
