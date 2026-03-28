using System.Collections.ObjectModel;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Converters;
using ParaTool.Core.Services;

namespace ParaTool.App.ViewModels;

public partial class IconEntryVM : ObservableObject
{
    public IconInfo Info { get; }
    private readonly IconService _service;

    [ObservableProperty] private WriteableBitmap? _thumbnail;

    public VanillaIconAtlasService.AtlasIcon? VanillaIcon { get; init; }
    public VanillaIconAtlasService? VanillaService { get; init; }

    public string Name => Info.Name;
    public string Source => Info.Source;

    public IconEntryVM(IconInfo info, IconService service)
    {
        Info = info;
        _service = service;
    }

    public bool TryLoadThumbnail()
    {
        if (Thumbnail != null) return true;

        if (VanillaIcon != null && VanillaService != null)
        {
            var rgba = VanillaService.ExtractIcon(VanillaIcon);
            if (rgba != null)
            {
                var (w, h) = VanillaService.GetTileSize(VanillaIcon);
                Thumbnail = RgbaToBitmapStatic(rgba, w, h);
            }
        }
        else
        {
            var dds = _service.LoadIconData(Info);
            if (dds != null)
                Thumbnail = DdsBitmapConverter.ToAvaloniaBitmap(dds);
        }
        return Thumbnail != null;
    }

    public void EnsureThumbnail() => TryLoadThumbnail();

    public static WriteableBitmap? RgbaToBitmapStatic(byte[] rgba, int w, int h)
    {
        if (w <= 0 || h <= 0 || rgba.Length < w * h * 4) return null;
        try
        {
            var bitmap = new WriteableBitmap(
                new PixelSize(w, h), new Vector(96, 96),
                PixelFormats.Rgba8888, AlphaFormat.Unpremul);
            using var fb = bitmap.Lock();
            Marshal.Copy(rgba, 0, fb.Address, Math.Min(rgba.Length, fb.RowBytes * h));
            return bitmap;
        }
        catch { return null; }
    }
}

/// <summary>
/// An atlas tab in the icon browser (e.g. "AMP", "Icons_Items", "Icons_Items_2").
/// </summary>
public partial class AtlasTabVM : ObservableObject
{
    public string Name { get; }
    public List<IconEntryVM> Icons { get; }
    [ObservableProperty] private bool _isLoaded;

    public AtlasTabVM(string name, List<IconEntryVM> icons)
    {
        Name = name;
        Icons = icons;
    }
}

public partial class IconBrowserVM : ObservableObject
{
    private readonly IconService _iconService;
    private readonly VanillaIconAtlasService _vanillaService = new();

    public ObservableCollection<AtlasTabVM> Tabs { get; } = [];
    public ObservableCollection<IconEntryVM> DisplayIcons { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private AtlasTabVM? _selectedTab;
    [ObservableProperty] private string _currentIconName = "";
    [ObservableProperty] private WriteableBitmap? _currentIconBitmap;

    public event Action<string>? IconSelected;

    public IconBrowserVM(IconService iconService)
    {
        _iconService = iconService;
    }

    public void Open(string? currentIcon = null, WriteableBitmap? currentBitmap = null)
    {
        CurrentIconName = currentIcon ?? "";
        CurrentIconBitmap = currentBitmap;

        if (Tabs.Count == 0)
            BuildTabs();

        IsOpen = true;

        // Auto-select tab containing current icon
        if (!string.IsNullOrEmpty(currentIcon))
        {
            var tab = Tabs.FirstOrDefault(t => t.Icons.Any(i => i.Name.Equals(currentIcon, StringComparison.OrdinalIgnoreCase)));
            if (tab != null) { SelectedTab = tab; return; }
        }

        SelectedTab ??= Tabs.FirstOrDefault();
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    partial void OnSearchTextChanged(string value) => RefreshDisplay();
    partial void OnSelectedTabChanged(AtlasTabVM? value) => RefreshDisplay();

    [RelayCommand]
    private void SelectTab(AtlasTabVM? tab) => SelectedTab = tab;

    private void BuildTabs()
    {
        Tabs.Clear();

        // AMP/mod icons
        var ampIcons = new List<IconEntryVM>();
        foreach (var info in _iconService.GetAllIcons())
            ampIcons.Add(new IconEntryVM(info, _iconService));
        if (ampIcons.Count > 0)
            Tabs.Add(new AtlasTabVM("AMP", ampIcons));

        // Vanilla atlas icons — grouped by atlas name
        var vanillaIcons = _vanillaService.LoadIconList();
        var seen = new HashSet<string>(ampIcons.Select(i => i.Name), StringComparer.OrdinalIgnoreCase);
        var byAtlas = new Dictionary<string, List<IconEntryVM>>(StringComparer.OrdinalIgnoreCase);

        foreach (var icon in vanillaIcons)
        {
            if (seen.Contains(icon.Name)) continue;
            seen.Add(icon.Name);

            if (!byAtlas.TryGetValue(icon.AtlasName, out var list))
            {
                list = [];
                byAtlas[icon.AtlasName] = list;
            }
            list.Add(new IconEntryVM(
                new IconInfo { Name = icon.Name, Source = icon.AtlasName }, _iconService)
            {
                VanillaIcon = icon,
                VanillaService = _vanillaService
            });
        }

        foreach (var (atlasName, icons) in byAtlas.OrderBy(kv => kv.Key))
            Tabs.Add(new AtlasTabVM(atlasName, icons));
    }

    private void RefreshDisplay()
    {
        DisplayIcons.Clear();
        if (SelectedTab == null) return;

        var query = SearchText.Trim();
        var source = string.IsNullOrEmpty(query)
            ? SelectedTab.Icons
            : SelectedTab.Icons.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        foreach (var icon in source)
        {
            icon.TryLoadThumbnail();
            if (icon.Thumbnail != null)
                DisplayIcons.Add(icon);
        }

        SelectedTab.IsLoaded = true;
    }

    public void SelectIcon(IconEntryVM icon)
    {
        IconSelected?.Invoke(icon.Name);
        Close();
    }
}
