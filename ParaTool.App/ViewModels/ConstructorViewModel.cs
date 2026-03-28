using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Localization;
using ParaTool.Core.Artifacts;
using ParaTool.Core.Models;
using ParaTool.Core.Parsing;
using ParaTool.App.Converters;
using ParaTool.Core.Localization;
using ParaTool.Core.Services;
using VanillaLoca = ParaTool.Core.Services.VanillaLocaService;

namespace ParaTool.App.ViewModels;

public partial class BaseItemVM : ObservableObject
{
    public ItemEntry Entry { get; }
    public string Label => Entry.DisplayName
        ?? VanillaLocaService.GetDisplayName(Entry.StatId, Localization.Loc.Instance.Lang)
        ?? Entry.StatId;
    public string FullLabel => Entry.DisplayName != null ? $"{Entry.DisplayName} ({Entry.StatId})" : Entry.StatId;
    public string StatId => Entry.StatId;
    public string StatType => Entry.StatType;
    public string Rarity => Entry.DetectedRarity ?? "Uncommon";
    public string ModName { get; }

    public IBrush RarityColor => Rarity switch
    {
        "Common" => new SolidColorBrush(Avalonia.Media.Color.Parse("#8A8494")),
        "Uncommon" => new SolidColorBrush(Avalonia.Media.Color.Parse("#2ECC71")),
        "Rare" => new SolidColorBrush(Avalonia.Media.Color.Parse("#3498DB")),
        "VeryRare" => new SolidColorBrush(Avalonia.Media.Color.Parse("#9B59B6")),
        "Legendary" => new SolidColorBrush(Avalonia.Media.Color.Parse("#C8A96E")),
        _ => new SolidColorBrush(Avalonia.Media.Color.Parse("#8A8494")),
    };

    public BaseItemVM(ItemEntry entry, string modName)
    {
        Entry = entry;
        ModName = modName;
    }
}

public partial class ConstructorViewModel : ViewModelBase
{
    public ObservableCollection<ArtifactItemVM> SavedArtifacts { get; } = [];
    public ObservableCollection<BaseItemVM> FilteredBaseItems { get; } = [];
    public ObservableCollection<NavGroupVM> NavGroups { get; } = [];
    private readonly List<BaseItemVM> _allBaseItems = [];

    [ObservableProperty] private ArtifactItemVM? _selectedArtifact;
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _editingLang = Localization.Loc.Instance.Lang;

    private readonly StatsResolver? _resolver;
    private readonly LocaService? _locaService;
    private readonly IconService? _iconService;

    public bool HasSavedArtifacts => SavedArtifacts.Count > 0;
    public bool IsArtifactSelected => SelectedArtifact != null;
    public bool HasNoSelection => SelectedArtifact == null;

    [ObservableProperty] private string _locaWarning = "";

    public IconBrowserVM? IconBrowser { get; private set; }

    public ConstructorViewModel(StatsResolver? resolver = null, LocaService? locaService = null, IconService? iconService = null)
    {
        _resolver = resolver;
        _locaService = locaService;
        _iconService = iconService;

        if (iconService != null)
        {
            IconBrowser = new IconBrowserVM(iconService);
            IconBrowser.IconSelected += OnIconSelected;
        }
        LoadSavedArtifacts();
        SavedArtifacts.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasSavedArtifacts));
    }

    public void SetBaseItems(IEnumerable<ModVM> mods)
    {
        _allBaseItems.Clear();
        NavGroups.Clear();

        foreach (var mod in mods)
        {
            var items = new List<BaseItemVM>();
            foreach (var item in mod.Items)
            {
                var bvm = new BaseItemVM(item.Entry, mod.Name);
                items.Add(bvm);
                _allBaseItems.Add(bvm);
            }

            if (items.Count > 0)
            {
                var group = new NavGroupVM(mod.Name, mod.IsAmp, items);
                NavGroups.Add(group);
            }
        }

        ApplyFilter();
    }

    private void LoadSavedArtifacts()
    {
        SavedArtifacts.Clear();
        foreach (var art in ArtifactStore.LoadAll())
        {
            // Backfill Weight from resolver for older .art files that don't have it
            if (art.Weight < 0 && _resolver != null && !string.IsNullOrEmpty(art.UsingBase))
            {
                var wStr = _resolver.Resolve(art.UsingBase, "Weight");
                if (wStr != null && double.TryParse(wStr, System.Globalization.NumberStyles.Float,
                        System.Globalization.CultureInfo.InvariantCulture, out var wd))
                    art.Weight = wd;
            }

            var vm = new ArtifactItemVM(art) { IsPersisted = true, SourceStatId = art.UsingBase, GetEditingLang = () => EditingLang };
            vm.LoadPassivesFromArtifact();
            SavedArtifacts.Add(vm);
        }
    }

    public bool IsSearching => !string.IsNullOrEmpty(SearchText);
    public bool IsNotSearching => string.IsNullOrEmpty(SearchText);

    partial void OnSearchTextChanged(string value)
    {
        OnPropertyChanged(nameof(IsSearching));
        OnPropertyChanged(nameof(IsNotSearching));
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        FilteredBaseItems.Clear();
        var query = SearchText.Trim();
        var source = string.IsNullOrEmpty(query)
            ? _allBaseItems
            : _allBaseItems.Where(i =>
                i.Label.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                i.StatId.Contains(query, StringComparison.OrdinalIgnoreCase));
        foreach (var item in source)
            FilteredBaseItems.Add(item);
    }

    partial void OnEditingLangChanged(string value)
    {
        LocaWarning = "";

        // Try to load loca for the new language
        if (_locaService != null)
        {
            var map = _locaService.GetLocaMap(value);
            if (map == null)
            {
                var bg3Name = LocaService.CodeToBg3.GetValueOrDefault(value, value);
                LocaWarning = $"Localization for {bg3Name} not found in paks";
            }
        }

        // Re-populate text fields from the new language's loca data
        if (SelectedArtifact != null && _resolver != null && _locaService != null)
            ReloadLocaForCurrentLang(SelectedArtifact);

        SelectedArtifact?.RefreshAll();
    }

    /// <summary>
    /// Reload localized texts for the artifact from loca data for the current editing language.
    /// Only fills empty fields — doesn't overwrite user edits.
    /// </summary>
    private void ReloadLocaForCurrentLang(ArtifactItemVM artVm)
    {
        var lang = EditingLang;
        var art = artVm.Artifact;

        // Item DisplayName — resolve via stored handle for new language
        if ((!art.DisplayName.ContainsKey(lang) || string.IsNullOrEmpty(art.DisplayName.GetValueOrDefault(lang)))
            && !string.IsNullOrEmpty(art.DisplayNameHandle))
        {
            var text = _locaService?.ResolveHandle(art.DisplayNameHandle, lang);
            if (text != null) art.DisplayName[lang] = BbCode.FromBg3Xml(text);
        }

        // Item Description
        if ((!art.Description.ContainsKey(lang) || string.IsNullOrEmpty(art.Description.GetValueOrDefault(lang)))
            && !string.IsNullOrEmpty(art.DescriptionHandle))
        {
            var text = _locaService?.ResolveHandle(art.DescriptionHandle, lang);
            if (text != null) art.Description[lang] = BbCode.FromBg3Xml(text);
        }

        // Passives — load descriptions for the new language
        foreach (var passive in art.Passives)
        {
            if (passive.DisplayName.ContainsKey(lang) && !string.IsNullOrEmpty(passive.DisplayName[lang]))
                continue; // Already has text for this lang

            var pFields = _resolver?.ResolveAll(passive.Name);
            if (pFields == null) continue;

            if (pFields.TryGetValue("DisplayName", out var dnHandle))
            {
                var text = _locaService?.ResolveHandle(dnHandle, lang);
                if (text != null) passive.DisplayName[lang] = BbCode.FromBg3Xml(text);
            }
            if (pFields.TryGetValue("Description", out var descHandle))
            {
                var text = _locaService?.ResolveHandle(descHandle, lang);
                if (text != null) passive.Description[lang] = BbCode.FromBg3Xml(text);
            }
        }
    }

    partial void OnSelectedArtifactChanged(ArtifactItemVM? oldValue, ArtifactItemVM? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        OnPropertyChanged(nameof(IsArtifactSelected));
        OnPropertyChanged(nameof(HasNoSelection));
    }

    // === Open item (NO file creation) ===

    [RelayCommand]
    private void OpenBaseItem(BaseItemVM? baseItem)
    {
        if (baseItem == null) return;

        // Check if already saved
        var existing = SavedArtifacts.FirstOrDefault(a => a.Artifact.UsingBase == baseItem.StatId);
        if (existing != null)
        {
            SelectedArtifact = existing;
            return;
        }

        // Create in-memory working copy (NOT saved to disk)
        var artifact = BuildArtifactFromBase(baseItem);
        var vm = new ArtifactItemVM(artifact) { SourceStatId = baseItem.StatId, IsPersisted = false, GetEditingLang = () => EditingLang };
        vm.LoadPassivesFromArtifact();
        vm.IsDirty = false;

        // Load icon
        LoadIconForArtifact(vm);

        SelectedArtifact = vm;
    }

    [RelayCommand]
    private void SelectSavedArtifact(ArtifactItemVM? item)
    {
        if (item != null && item.IconBitmap == null)
            LoadIconForArtifact(item);
        SelectedArtifact = item;
    }

    // === Save (green button) ===

    [RelayCommand]
    private void SaveArtifact()
    {
        if (SelectedArtifact == null) return;

        // Sync passives from VMs back to artifact
        SelectedArtifact.Artifact.Passives = SelectedArtifact.PassiveVMs
            .Select(p => p.Passive).ToList();

        ArtifactStore.Save(SelectedArtifact.Artifact);

        if (!SelectedArtifact.IsPersisted)
        {
            SelectedArtifact.IsPersisted = true;
            SavedArtifacts.Add(SelectedArtifact);
        }

        SelectedArtifact.IsDirty = false;
    }

    // === Reset (red button) ===

    [RelayCommand]
    private void ResetArtifact()
    {
        if (SelectedArtifact == null) return;

        var sourceStatId = SelectedArtifact.SourceStatId;
        var baseItem = _allBaseItems.FirstOrDefault(b => b.StatId == sourceStatId);

        if (baseItem != null)
        {
            // Reload from resolver
            var fresh = BuildArtifactFromBase(baseItem);

            // Copy fresh data into existing artifact (keep same ArtifactId/TemplateUuid)
            var art = SelectedArtifact.Artifact;
            art.Rarity = fresh.Rarity;
            art.Boosts = fresh.Boosts;
            art.PassivesOnEquip = fresh.PassivesOnEquip;
            art.StatusOnEquip = fresh.StatusOnEquip;
            art.SpellsOnEquip = fresh.SpellsOnEquip;
            art.DefaultBoosts = fresh.DefaultBoosts;
            art.Damage = fresh.Damage;
            art.VersatileDamage = fresh.VersatileDamage;
            art.WeaponProperties = fresh.WeaponProperties;
            art.ArmorClass = fresh.ArmorClass;
            art.ArmorType = fresh.ArmorType;
            art.ValueOverride = fresh.ValueOverride;
            art.Weight = fresh.Weight;
            art.Unique = fresh.Unique;
            art.LootPool = fresh.LootPool;
            art.LootThemes = fresh.LootThemes;
            art.DisplayName = fresh.DisplayName;
            art.Description = fresh.Description;
            art.Passives = fresh.Passives;

            SelectedArtifact.RefreshAll();
            SelectedArtifact.IsDirty = false;
        }
    }

    // === Delete saved artifact ===

    [RelayCommand]
    private void DeleteArtifact(ArtifactItemVM? item)
    {
        if (item == null) return;
        if (item.IsPersisted)
            ArtifactStore.Delete(item.Artifact.ArtifactId);
        SavedArtifacts.Remove(item);
        if (SelectedArtifact == item)
            SelectedArtifact = null;
    }

    [RelayCommand]
    private void DuplicateArtifact(ArtifactItemVM? item)
    {
        if (item == null) return;
        var json = System.Text.Json.JsonSerializer.Serialize(item.Artifact);
        var clone = System.Text.Json.JsonSerializer.Deserialize<ArtifactDefinition>(json);
        if (clone == null) return;
        clone.ArtifactId = Guid.NewGuid().ToString();
        clone.TemplateUuid = Guid.NewGuid().ToString();
        clone.StatId += "_Copy";
        clone.DisplayNameHandle = "";
        clone.DescriptionHandle = "";
        ArtifactStore.Save(clone);
        var vm = new ArtifactItemVM(clone) { IsPersisted = true, SourceStatId = clone.UsingBase, GetEditingLang = () => EditingLang };
        vm.LoadPassivesFromArtifact();
        SavedArtifacts.Add(vm);
        SelectedArtifact = vm;
    }

    // === Chip commands ===

    [RelayCommand]
    private void SetRarity(string rarity)
    {
        if (SelectedArtifact == null) return;
        SelectedArtifact.EditRarity = rarity;
    }

    [RelayCommand]
    private void SetPool(string pool)
    {
        if (SelectedArtifact == null) return;
        SelectedArtifact.EditPool = pool;
    }

    [RelayCommand]
    private void ToggleTheme(string theme)
    {
        if (SelectedArtifact == null) return;
        SelectedArtifact.ToggleTheme(theme);
    }

    // === Build artifact from base item using resolver ===

    private ArtifactDefinition BuildArtifactFromBase(BaseItemVM baseItem)
    {
        var scanLang = Localization.Loc.Instance.Lang; // Language used during scan (UI lang)

        var artifact = new ArtifactDefinition
        {
            StatId = baseItem.StatId,
            StatType = baseItem.StatType,
            UsingBase = baseItem.StatId,
            Rarity = baseItem.Rarity
        };

        if (_resolver != null)
        {
            var fields = _resolver.ResolveAll(baseItem.StatId);

            if (fields.TryGetValue("Boosts", out var boosts)) artifact.Boosts = boosts;
            if (fields.TryGetValue("PassivesOnEquip", out var passives)) artifact.PassivesOnEquip = passives;
            if (fields.TryGetValue("StatusOnEquip", out var statuses)) artifact.StatusOnEquip = statuses;
            if (fields.TryGetValue("DefaultBoosts", out var defBoosts)) artifact.DefaultBoosts = defBoosts;
            if (fields.TryGetValue("Rarity", out var rarity)) artifact.Rarity = MapRarity(rarity);
            if (fields.TryGetValue("ValueOverride", out var val) && int.TryParse(val, out var vi)) artifact.ValueOverride = vi;
            if (fields.TryGetValue("Unique", out var unique)) artifact.Unique = unique == "1";
            if (fields.TryGetValue("Weight", out var w) && double.TryParse(w, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var wd)) artifact.Weight = wd;
            if (fields.TryGetValue("ArmorClass", out var ac) && int.TryParse(ac, out var aci)) artifact.ArmorClass = aci;
            if (fields.TryGetValue("ArmorType", out var at)) artifact.ArmorType = at;
            if (fields.TryGetValue("Proficiency Group", out var pg)) artifact.ProficiencyGroup = pg;
            if (fields.TryGetValue("ComboCategory", out var cc) && !string.IsNullOrEmpty(cc)) artifact.ComboCategory = cc;
            if (fields.TryGetValue("Damage", out var dmg)) artifact.Damage = dmg;
            if (fields.TryGetValue("VersatileDamage", out var vd)) artifact.VersatileDamage = vd;
            if (fields.TryGetValue("Weapon Properties", out var wp)) artifact.WeaponProperties = wp;
            if (fields.TryGetValue("Spells", out var spells)) artifact.SpellsOnEquip = spells;

            // Resolve passives
            if (!string.IsNullOrEmpty(artifact.PassivesOnEquip))
            {
                var names = artifact.PassivesOnEquip.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                foreach (var pName in names)
                {
                    var pFields = _resolver.ResolveAll(pName);
                    if (pFields.Count == 0) continue;

                    var passive = new PassiveDefinition { Name = pName };
                    if (pFields.TryGetValue("DisplayName", out var dn))
                    {
                        // Load for editing language
                        var t = ResolveLoca(dn, EditingLang);
                        if (t != null) passive.DisplayName[EditingLang] = t;
                        // Also load scan language and English
                        if (EditingLang != scanLang) { var st = ResolveLoca(dn, scanLang); if (st != null) passive.DisplayName[scanLang] = st; }
                        if (EditingLang != "en" && scanLang != "en") { var en = ResolveLoca(dn, "en"); if (en != null) passive.DisplayName["en"] = en; }
                    }
                    if (pFields.TryGetValue("Description", out var dd))
                    {
                        var t = ResolveLoca(dd, EditingLang);
                        if (t != null) passive.Description[EditingLang] = t;
                        if (EditingLang != scanLang) { var st = ResolveLoca(dd, scanLang); if (st != null) passive.Description[scanLang] = st; }
                        if (EditingLang != "en" && scanLang != "en") { var en = ResolveLoca(dd, "en"); if (en != null) passive.Description["en"] = en; }
                    }
                    if (pFields.TryGetValue("DescriptionParams", out var dp)) passive.DescriptionParams = dp;
                    if (pFields.TryGetValue("Icon", out var icon)) passive.Icon = icon;
                    if (pFields.TryGetValue("Boosts", out var pb)) passive.Boosts = pb;
                    if (pFields.TryGetValue("BoostContext", out var bc)) passive.BoostContext = bc;
                    if (pFields.TryGetValue("BoostConditions", out var bco)) passive.BoostConditions = bco;
                    if (pFields.TryGetValue("StatsFunctorContext", out var sfc)) passive.StatsFunctorContext = sfc;
                    if (pFields.TryGetValue("Conditions", out var cond)) passive.Conditions = cond;
                    if (pFields.TryGetValue("StatsFunctors", out var sf)) passive.StatsFunctors = sf;

                    artifact.Passives.Add(passive);
                }
            }
        }

        // Save handles for on-demand multi-lang resolution
        artifact.DisplayNameHandle = baseItem.Entry.DisplayNameHandle ?? "";
        artifact.DescriptionHandle = baseItem.Entry.DescriptionHandle ?? "";

        // Pre-load English if available and UI is not English
        if (EditingLang != "en" && _locaService != null)
        {
            if (!string.IsNullOrEmpty(artifact.DisplayNameHandle))
            {
                var enName = _locaService.ResolveHandle(artifact.DisplayNameHandle, "en");
                if (enName != null) artifact.DisplayName["en"] = BbCode.FromBg3Xml(enName);
            }
            if (!string.IsNullOrEmpty(artifact.DescriptionHandle))
            {
                var enDesc = _locaService.ResolveHandle(artifact.DescriptionHandle, "en");
                if (enDesc != null) artifact.Description["en"] = BbCode.FromBg3Xml(enDesc);
            }
        }

        // DisplayName — Entry.DisplayName is in scan language (UI lang at scan time)
        if (!string.IsNullOrEmpty(baseItem.Entry.DisplayName))
            artifact.DisplayName[scanLang] = BbCode.FromBg3Xml(baseItem.Entry.DisplayName);
        if (!string.IsNullOrEmpty(baseItem.Entry.Description))
            artifact.Description[scanLang] = BbCode.FromBg3Xml(baseItem.Entry.Description);

        // Load for EditingLang via LocaService (if different from scan lang)
        if (EditingLang != scanLang && _locaService != null)
        {
            if (!string.IsNullOrEmpty(artifact.DisplayNameHandle))
            {
                var text = _locaService.ResolveHandle(artifact.DisplayNameHandle, EditingLang);
                if (text != null) artifact.DisplayName[EditingLang] = BbCode.FromBg3Xml(text);
            }
            if (!string.IsNullOrEmpty(artifact.DescriptionHandle))
            {
                var text = _locaService.ResolveHandle(artifact.DescriptionHandle, EditingLang);
                if (text != null) artifact.Description[EditingLang] = BbCode.FromBg3Xml(text);
            }
        }

        // Also pre-load English if not already covered
        if (scanLang != "en" && EditingLang != "en" && _locaService != null)
        {
            if (!string.IsNullOrEmpty(artifact.DisplayNameHandle))
            {
                var enText = _locaService.ResolveHandle(artifact.DisplayNameHandle, "en");
                if (enText != null) artifact.DisplayName["en"] = BbCode.FromBg3Xml(enText);
            }
            if (!string.IsNullOrEmpty(artifact.DescriptionHandle))
            {
                var enText = _locaService.ResolveHandle(artifact.DescriptionHandle, "en");
                if (enText != null) artifact.Description["en"] = BbCode.FromBg3Xml(enText);
            }
        }

        // Fallback: vanilla embedded localization
        FillFromVanillaLoca(artifact);

        artifact.LootPool = baseItem.Entry.DetectedPool;
        if (baseItem.Entry.DetectedThemes.Count > 0)
            artifact.LootThemes = new List<string>(baseItem.Entry.DetectedThemes);

        return artifact;
    }

    /// <summary>
    /// Fill missing localization from embedded vanilla TSV data.
    /// </summary>
    private static void FillFromVanillaLoca(ArtifactDefinition artifact)
    {
        var statId = artifact.UsingBase ?? artifact.StatId;

        // Item name/description
        foreach (var lang in new[] { "en", "ru" })
        {
            if (!artifact.DisplayName.ContainsKey(lang) || string.IsNullOrEmpty(artifact.DisplayName.GetValueOrDefault(lang)))
            {
                var name = VanillaLoca.GetDisplayName(statId, lang);
                if (name != null) artifact.DisplayName[lang] = BbCode.FromBg3Xml(name);
            }
            if (!artifact.Description.ContainsKey(lang) || string.IsNullOrEmpty(artifact.Description.GetValueOrDefault(lang)))
            {
                var desc = VanillaLoca.GetDescription(statId, lang);
                if (desc != null) artifact.Description[lang] = BbCode.FromBg3Xml(desc);
            }
        }

        // Passive names/descriptions
        foreach (var passive in artifact.Passives)
        {
            foreach (var lang in new[] { "en", "ru" })
            {
                if (!passive.DisplayName.ContainsKey(lang) || string.IsNullOrEmpty(passive.DisplayName.GetValueOrDefault(lang)))
                {
                    var name = VanillaLoca.GetDisplayName(passive.Name, lang);
                    if (name != null) passive.DisplayName[lang] = BbCode.FromBg3Xml(name);
                }
                if (!passive.Description.ContainsKey(lang) || string.IsNullOrEmpty(passive.Description.GetValueOrDefault(lang)))
                {
                    var desc = VanillaLoca.GetDescription(passive.Name, lang);
                    if (desc != null) passive.Description[lang] = BbCode.FromBg3Xml(desc);
                }
            }
        }
    }

    /// <summary>
    /// Resolve handle → text, then convert BG3 XML to BB-code for editor display.
    /// </summary>
    private string? ResolveLoca(string handleField, string? langOverride = null)
    {
        var raw = _locaService?.ResolveHandle(handleField, langOverride ?? EditingLang);
        if (raw == null) return null;
        // Convert BG3 XML-escaped → BB-code for human-readable editing
        return BbCode.FromBg3Xml(raw);
    }

    /// <summary>
    /// Create a new artifact from current item with a custom name.
    /// Generates StatId from human name, copies current data.
    /// </summary>
    [RelayCommand]
    private void CreateNewArtifact(string? humanName)
    {
        if (SelectedArtifact == null || string.IsNullOrWhiteSpace(humanName)) return;

        // Generate StatId: "My Cool Sword" → "AMP_My_Cool_Sword"
        var cleaned = System.Text.RegularExpressions.Regex.Replace(humanName.Trim(), @"[^a-zA-Z0-9\s]", "");
        var parts = cleaned.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var statId = "AMP_" + string.Join("_", parts.Select(p => char.ToUpper(p[0]) + (p.Length > 1 ? p[1..] : "")));

        // Ensure unique
        var baseId = statId;
        int idx = 2;
        while (SavedArtifacts.Any(a => a.Artifact.StatId.Equals(statId, StringComparison.OrdinalIgnoreCase)))
            statId = $"{baseId}_{idx++}";

        // Deep clone current artifact
        var json = System.Text.Json.JsonSerializer.Serialize(SelectedArtifact.Artifact);
        var clone = System.Text.Json.JsonSerializer.Deserialize<ArtifactDefinition>(json);
        if (clone == null) return;

        clone.ArtifactId = Guid.NewGuid().ToString();
        clone.TemplateUuid = Guid.NewGuid().ToString();
        clone.StatId = statId;
        clone.UsingBase = SelectedArtifact.Artifact.StatId; // inherit from current
        clone.DisplayNameHandle = ""; // new handles will be generated on save
        clone.DescriptionHandle = "";

        // Set display name from human input
        clone.DisplayName[EditingLang] = humanName.Trim();

        ArtifactStore.Save(clone);

        var vm = new ArtifactItemVM(clone) { IsPersisted = true, SourceStatId = clone.UsingBase, GetEditingLang = () => EditingLang };
        vm.LoadPassivesFromArtifact();
        LoadIconForArtifact(vm);
        SavedArtifacts.Add(vm);
        SelectedArtifact = vm;
    }

    [RelayCommand]
    private void OpenIconBrowser()
    {
        var currentName = SelectedArtifact?.Artifact.AtlasIconMapKey;
        var currentBitmap = SelectedArtifact?.IconBitmap;
        IconBrowser?.Open(currentName, currentBitmap);
    }

    private void OnIconSelected(string iconName)
    {
        if (SelectedArtifact == null || _iconService == null) return;
        SelectedArtifact.Artifact.AtlasIconMapKey = iconName;
        SelectedArtifact.IsDirty = true;

        // Load and display the new icon
        var dds = _iconService.GetIconDds(iconName);
        if (dds != null)
        {
            var bitmap = DdsBitmapConverter.ToAvaloniaBitmap(dds);
            if (bitmap != null)
                SelectedArtifact.IconBitmap = bitmap;
        }
    }

    private static readonly Lazy<VanillaIconAtlasService> _vanillaAtlasLazy = new(() =>
    {
        var svc = new VanillaIconAtlasService();
        svc.LoadIconList();
        return svc;
    });

    private void LoadIconForArtifact(ArtifactItemVM vm)
    {
        if (_iconService == null) return;

        var _vanillaAtlas = _vanillaAtlasLazy.Value;

        // 1. Try AMP DDS by StatId / using-chain
        var iconName = _iconService.FindIconName(vm.Artifact.StatId, _resolver);
        if (iconName == null && !string.IsNullOrEmpty(vm.Artifact.UsingBase)
            && vm.Artifact.UsingBase != vm.Artifact.StatId)
            iconName = _iconService.FindIconName(vm.Artifact.UsingBase, _resolver);

        if (iconName != null)
        {
            vm.Artifact.AtlasIconMapKey ??= iconName;
            var dds = _iconService.GetIconDds(iconName);
            if (dds != null)
            {
                vm.IconBitmap = DdsBitmapConverter.ToAvaloniaBitmap(dds);
                if (vm.IconBitmap != null) return;
            }
        }

        // 2. Try IconName from RootTemplate (resolved during scan) or vanilla UUID mapping or vanilla loca
        var baseItem = _allBaseItems.FirstOrDefault(b => b.StatId == vm.Artifact.StatId
            || b.StatId == vm.Artifact.UsingBase);
        var rtIconName = baseItem?.Entry.IconName
            ?? VanillaLoca.GetIconName(vm.Artifact.StatId)
            ?? VanillaLoca.GetIconName(vm.Artifact.UsingBase ?? "");

        // 2b. Try vanilla UUID→Icon mapping if RootTemplate UUID is known
        if (rtIconName == null && _resolver != null)
        {
            var rtUuid = _resolver.Resolve(vm.Artifact.UsingBase ?? vm.Artifact.StatId, "RootTemplate");
            if (rtUuid != null)
            {
                // Try from scan (AMP parsed via LSFReader)
                // Then from embedded vanilla mapping
                rtIconName = VanillaIconAtlasService.GetIconNameByUuid(rtUuid);
            }
        }

        // Also walk using-chain to find IconName from parent
        if (rtIconName == null && _resolver != null)
        {
            var current = vm.Artifact.UsingBase ?? vm.Artifact.StatId;
            int depth = 0;
            while (current != null && depth < 20)
            {
                var parentItem = _allBaseItems.FirstOrDefault(b => b.StatId == current);
                if (parentItem?.Entry.IconName != null)
                {
                    rtIconName = parentItem.Entry.IconName;
                    break;
                }
                var entry = _resolver.Get(current);
                current = entry?.Using;
                depth++;
            }
        }

        if (rtIconName != null)
        {
            vm.Artifact.AtlasIconMapKey ??= rtIconName;

            // Try AMP DDS first
            var dds = _iconService.GetIconDds(rtIconName);
            if (dds != null)
            {
                vm.IconBitmap = DdsBitmapConverter.ToAvaloniaBitmap(dds);
                if (vm.IconBitmap != null) return;
            }

            // Try vanilla atlas
            var vanillaIcon = _vanillaAtlas.LoadIconList()
                .FirstOrDefault(i => i.Name.Equals(rtIconName, StringComparison.OrdinalIgnoreCase));
            if (vanillaIcon != null)
            {
                var rgba = _vanillaAtlas.ExtractIcon(vanillaIcon);
                if (rgba != null)
                {
                    var (w, h) = _vanillaAtlas.GetTileSize(vanillaIcon);
                    vm.IconBitmap = IconEntryVM.RgbaToBitmapStatic(rgba, w, h);
                }
            }
        }
    }

    /// <summary>Get all stats entries of a given type for autocomplete.</summary>
    public List<string> GetStatsOfType(string type)
    {
        if (_resolver == null) return [];
        return _resolver.AllEntries.Values
            .Where(e => e.Type.Equals(type, StringComparison.OrdinalIgnoreCase))
            .Select(e => e.Name)
            .OrderBy(n => n)
            .ToList();
    }

    private static string MapRarity(string raw) => raw switch
    {
        "Common" or "common" => "Common",
        "Uncommon" or "uncommon" => "Uncommon",
        "Rare" or "rare" => "Rare",
        "VeryRare" or "veryrare" or "Epic" => "VeryRare",
        "Legendary" or "legendary" => "Legendary",
        _ => raw
    };
}
