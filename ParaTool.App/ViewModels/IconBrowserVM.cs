using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
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

        var dds = _service.LoadIconData(Info);
        if (dds != null)
            Thumbnail = DdsBitmapConverter.ToAvaloniaBitmap(dds);
    }
}

/// <summary>
/// Icon browser popup state — shows grid of icons with search.
/// </summary>
public partial class IconBrowserVM : ObservableObject
{
    private readonly IconService _iconService;
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
        foreach (var info in _iconService.GetAllIcons())
            _allEntries.Add(new IconEntryVM(info, _iconService));
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
