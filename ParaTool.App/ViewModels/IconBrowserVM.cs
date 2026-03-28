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

        if (VanillaIcon != null && VanillaService != null)
        {
            var rgba = VanillaService.ExtractIcon(VanillaIcon);
            if (rgba != null)
            {
                var (w, h) = VanillaService.GetTileSize(VanillaIcon);
                Thumbnail = RgbaToBitmapStatic(rgba, w, h);
            }
            return;
        }

        var dds = _service.LoadIconData(Info);
        if (dds != null)
            Thumbnail = DdsBitmapConverter.ToAvaloniaBitmap(dds);
    }

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

public partial class IconBrowserVM : ObservableObject
{
    private readonly IconService _iconService;
    private readonly VanillaIconAtlasService _vanillaService = new();
    private List<IconEntryVM> _allEntries = [];
    private List<IconEntryVM> _filteredEntries = [];

    public ObservableCollection<IconEntryVM> PageIcons { get; } = [];

    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private IconEntryVM? _selectedIcon;
    [ObservableProperty] private int _currentPage;
    [ObservableProperty] private int _totalPages;
    [ObservableProperty] private string _currentIconName = "";
    [ObservableProperty] private WriteableBitmap? _currentIconBitmap;

    private const int PageSize = 20;

    public event Action<string>? IconSelected;

    public IconBrowserVM(IconService iconService)
    {
        _iconService = iconService;
    }

    public void Open(string? currentIcon = null, WriteableBitmap? currentBitmap = null)
    {
        CurrentIconName = currentIcon ?? "";
        CurrentIconBitmap = currentBitmap;

        if (_allEntries.Count == 0)
            LoadAllIcons();

        IsOpen = true;
        CurrentPage = 0;
        ApplyFilter();
    }

    [RelayCommand]
    public void Close() => IsOpen = false;

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void NextPage()
    {
        if (CurrentPage < TotalPages - 1)
        {
            CurrentPage++;
            ShowPage();
        }
    }

    [RelayCommand]
    private void PrevPage()
    {
        if (CurrentPage > 0)
        {
            CurrentPage--;
            ShowPage();
        }
    }

    private void LoadAllIcons()
    {
        _allEntries = [];
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var info in _iconService.GetAllIcons())
        {
            _allEntries.Add(new IconEntryVM(info, _iconService));
            seen.Add(info.Name);
        }

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
        var query = SearchText.Trim();
        _filteredEntries = string.IsNullOrEmpty(query)
            ? _allEntries.ToList()
            : _allEntries.Where(i => i.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();

        TotalPages = Math.Max(1, (_filteredEntries.Count + PageSize - 1) / PageSize);

        if (!string.IsNullOrEmpty(CurrentIconName) && CurrentPage == 0 && string.IsNullOrEmpty(query))
        {
            var idx = _filteredEntries.FindIndex(i => i.Name.Equals(CurrentIconName, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
                CurrentPage = idx / PageSize;
        }

        CurrentPage = Math.Min(CurrentPage, TotalPages - 1);
        ShowPage();
    }

    private void ShowPage()
    {
        PageIcons.Clear();
        var skip = CurrentPage * PageSize;
        // Take more than PageSize to account for decode failures, then trim to PageSize
        var candidates = _filteredEntries.Skip(skip).Take(PageSize).ToList();

        foreach (var icon in candidates)
        {
            // Only decode thumbnails for current page
            if (!icon.IsLoaded)
                icon.EnsureThumbnail();
            if (icon.Thumbnail != null)
                PageIcons.Add(icon);
        }
    }

    public void SelectIcon(IconEntryVM icon)
    {
        SelectedIcon = icon;
        IconSelected?.Invoke(icon.Name);
        Close();
    }
}
