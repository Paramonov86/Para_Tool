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
    [ObservableProperty] private bool _isLoaded;

    // For vanilla atlas icons
    public VanillaIconAtlasService.AtlasIcon? VanillaIcon { get; init; }
    public VanillaIconAtlasService? VanillaService { get; init; }

    public string Name => Info.Name;
    public string Source => Info.Source;

    public IconEntryVM(IconInfo info, IconService service)
    {
        Info = info;
        _service = service;
    }

    public void EnsureThumbnail()
    {
        if (IsLoaded) return;
        IsLoaded = true;

        // Vanilla atlas icon → extract from atlas
        if (VanillaIcon != null && VanillaService != null)
        {
            var rgba = VanillaService.ExtractIcon(VanillaIcon);
            if (rgba != null)
            {
                var (w, h) = VanillaService.GetTileSize(VanillaIcon);
                Thumbnail = RgbaToBitmap(rgba, w, h);
            }
            return;
        }

        // AMP/mod icon → load from DDS
        var dds = _service.LoadIconData(Info);
        if (dds != null)
            Thumbnail = DdsBitmapConverter.ToAvaloniaBitmap(dds);
    }

    public static WriteableBitmap? RgbaToBitmapStatic(byte[] rgba, int w, int h) => RgbaToBitmap(rgba, w, h);

    private static WriteableBitmap? RgbaToBitmap(byte[] rgba, int w, int h)
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
/// Icon browser popup state — shows grid of icons with search.
/// </summary>
public partial class IconBrowserVM : ObservableObject
{
    private readonly IconService _iconService;
    private readonly VanillaIconAtlasService _vanillaService = new();
    private readonly List<IconEntryVM> _allEntries = [];

    public ObservableCollection<IconEntryVM> FilteredIcons { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private IconEntryVM? _selectedIcon;

    public event Action<string>? IconSelected;

    public IconBrowserVM(IconService iconService)
    {
        _iconService = iconService;
    }

    public void Open()
    {
        if (_allEntries.Count == 0)
            LoadAllIcons();
        IsOpen = true;
        ApplyFilter();
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void LoadAllIcons()
    {
        _allEntries.Clear();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // AMP/mod icons first
        foreach (var info in _iconService.GetAllIcons())
        {
            _allEntries.Add(new IconEntryVM(info, _iconService));
            seen.Add(info.Name);
        }

        // Vanilla atlas icons (embedded)
        foreach (var icon in _vanillaService.LoadIconList())
        {
            if (seen.Contains(icon.Name)) continue;
            seen.Add(icon.Name);
            _allEntries.Add(new IconEntryVM(
                new IconInfo { Name = icon.Name, Source = "Vanilla" }, _iconService)
            {
                VanillaIcon = icon,
                VanillaService = _vanillaService
            });
        }
    }

    private void ApplyFilter()
    {
        FilteredIcons.Clear();
        var query = SearchText.Trim();
        var source = string.IsNullOrEmpty(query)
            ? _allEntries
            : _allEntries.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase));

        int count = 0;
        foreach (var icon in source)
        {
            if (count >= 500) break; // Show more, lazy load thumbnails
            icon.EnsureThumbnail();
            if (icon.Thumbnail == null) continue; // Skip icons that failed to load
            FilteredIcons.Add(icon);
            count++;
        }
    }

    public void SelectIcon(IconEntryVM icon)
    {
        SelectedIcon = icon;
        IconSelected?.Invoke(icon.Name);
        Close();
    }

}
