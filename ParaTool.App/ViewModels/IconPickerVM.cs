using System.Collections.ObjectModel;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParaTool.App.Localization;
using ParaTool.App.Services;
using ParaTool.Core.Icons;

namespace ParaTool.App.ViewModels;

/// <summary>What the picked icon is for — it decides which icons fit.</summary>
public enum IconPickTarget
{
    /// <summary>An item: inventory icons.</summary>
    Item,

    /// <summary>A spell shows a tooltip picture and a hotbar tile, so only icons that have both.</summary>
    Spell,

    /// <summary>A status shows only the small tile: status, spell and item icons all fit.</summary>
    Status,
}

/// <summary>One icon in the grid. The picture loads the first time the cell is drawn.</summary>
public sealed partial class IconCellVM : ObservableObject
{
    public IconEntry Entry { get; }
    public string Name => Entry.Name;
    public string Tip => $"{Entry.Name}\n{Entry.Source}";

    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private bool _isCurrent;

    public IconCellVM(IconEntry entry) => Entry = entry;

    private WriteableBitmap? _thumb;
    private bool _requested;

    public WriteableBitmap? Thumb
    {
        get
        {
            if (_thumb == null && !_requested) Load();
            return _thumb;
        }
    }

    private void Load()
    {
        _requested = true;
        var svc = IconLibraryService.Current;
        if (svc == null) return;
        if (svc.HasThumbCached(Name))
        {
            _thumb = svc.Thumb(Name);
            return;
        }
        Task.Run(() => svc.Thumb(Name)).ContinueWith(t =>
        {
            if (t.IsCompletedSuccessfully)
                Dispatcher.UIThread.Post(() => { _thumb = t.Result; OnPropertyChanged(nameof(Thumb)); });
        });
    }
}

/// <summary>A row of the grid: the grid is a virtualized list of rows so only visible cells exist.</summary>
public sealed class IconRowVM(IReadOnlyList<IconCellVM> cells)
{
    public IReadOnlyList<IconCellVM> Cells { get; } = cells;
}

public sealed partial class IconKindTabVM(IconKind? kind, string label) : ObservableObject
{
    /// <summary>Null = every kind the target accepts.</summary>
    public IconKind? Kind { get; } = kind;
    public string Label { get; } = label;
    [ObservableProperty] private bool _isActive;
}

/// <summary>
/// The icon library window: every icon the game and the installed mods ship, sorted by what it
/// fits, searchable by name or by the spells and statuses that use it.
/// </summary>
public sealed partial class IconPickerVM : ObservableObject
{
    [ObservableProperty] private bool _isOpen;
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private string _hint = "";
    [ObservableProperty] private string _searchText = "";
    [ObservableProperty] private string _selectedSource = "";
    [ObservableProperty] private IconCellVM? _selected;
    [ObservableProperty] private int _count;
    [ObservableProperty] private bool _canInherit;
    [ObservableProperty] private string _gameFolderError = "";

    [ObservableProperty] private WriteableBitmap? _detailLarge;
    [ObservableProperty] private WriteableBitmap? _detailTile;
    [ObservableProperty] private string _detailName = "";
    [ObservableProperty] private string _detailInfo = "";
    [ObservableProperty] private string _detailUsedBy = "";
    [ObservableProperty] private string _detailWarning = "";

    public ObservableCollection<IconKindTabVM> KindTabs { get; } = [];
    public ObservableCollection<string> Sources { get; } = [];
    public ObservableCollection<IconRowVM> Rows { get; } = [];

    public bool IsLoading => IconLibraryService.Current == null;
    public bool GameMissing => IconLibraryService.Current is { } s && s.Library.GameDataDir == null;
    public bool HasSelection => Selected != null;
    public bool HasDetailWarning => DetailWarning.Length > 0;
    public bool HasNoResults => !IsLoading && Count == 0;

    /// <summary>The icon the thing being edited has now (highlighted in the grid).</summary>
    public string CurrentName { get; private set; } = "";

    private IconPickTarget _target;
    private Action<string>? _onPick;
    private Action? _onInherit;
    private int _columns = 8;
    private List<IconCellVM> _filtered = [];
    private readonly Dictionary<string, IconCellVM> _cells = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, string>? _searchKeys;
    private bool _buildingSearchKeys;

    /// <summary>Set by the Constructor: resolves a stats entry name to its display name.</summary>
    public Func<string, string?>? DisplayNameOf { get; set; }

    public IconPickerVM()
    {
        IconLibraryService.Changed += OnLibraryChanged;
    }

    private void OnLibraryChanged()
    {
        GameFolderError = "";
        _cells.Clear();
        _searchKeys = null;
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(GameMissing));
        if (IsOpen) Refilter();
    }

    public static IReadOnlyList<IconKind> KindsFor(IconPickTarget target) => target switch
    {
        IconPickTarget.Spell => [IconKind.Spell],
        IconPickTarget.Status => [IconKind.Status, IconKind.Spell, IconKind.Item],
        _ => [IconKind.Item],
    };

    public static string KindLabel(IconKind kind) => kind switch
    {
        IconKind.Spell => Loc.Instance["LblIconsSpells"],
        IconKind.Status => Loc.Instance["LblIconsStatuses"],
        _ => Loc.Instance["LblIconsItems"],
    };

    /// <summary>
    /// Opens the library for a spell, status or item. <paramref name="onInherit"/> offers going back
    /// to the icon the original has (a card's icon is an override).
    /// </summary>
    public void Open(IconPickTarget target, string? current, Action<string> onPick, Action? onInherit = null)
    {
        _target = target;
        _onPick = onPick;
        _onInherit = onInherit;
        CanInherit = onInherit != null;
        CurrentName = current?.Trim() ?? "";
        Title = target switch
        {
            IconPickTarget.Spell => Loc.Instance["LblIconLibrarySpell"],
            IconPickTarget.Status => Loc.Instance["LblIconLibraryStatus"],
            _ => Loc.Instance["LblIconLibraryItem"],
        };
        Hint = target switch
        {
            IconPickTarget.Spell => Loc.Instance["TipIconsForSpell"],
            IconPickTarget.Status => Loc.Instance["TipIconsForStatus"],
            _ => "",
        };

        var kinds = KindsFor(target);
        var currentKind = IconLibraryService.Current?.Find(CurrentName)?.Kind;
        KindTabs.Clear();
        if (kinds.Count > 1) KindTabs.Add(new IconKindTabVM(null, Loc.Instance["LblIconsAll"]));
        foreach (var k in kinds) KindTabs.Add(new IconKindTabVM(k, KindLabel(k)));
        var active = KindTabs.FirstOrDefault(t => t.Kind == currentKind && currentKind != null) ?? KindTabs[0];
        active.IsActive = true;

        SearchText = "";
        SelectedSource = Loc.Instance["LblIconSourceAll"];
        IsOpen = true;
        Refilter();
        Selected = _filtered.FirstOrDefault(c => c.IsCurrent);
    }

    partial void OnSearchTextChanged(string value) { if (IsOpen) Refilter(); }
    partial void OnSelectedSourceChanged(string value) { if (IsOpen) Refilter(); }

    [RelayCommand]
    private void SelectKind(IconKindTabVM? tab)
    {
        if (tab == null) return;
        foreach (var t in KindTabs) t.IsActive = t == tab;
        Refilter();
    }

    /// <summary>The grid's width fits this many cells per row.</summary>
    public void SetColumns(int columns)
    {
        columns = Math.Max(1, columns);
        if (columns == _columns) return;
        _columns = columns;
        RebuildRows();
    }

    private IconCellVM Cell(IconEntry e)
    {
        if (!_cells.TryGetValue(e.Name, out var cell)) _cells[e.Name] = cell = new IconCellVM(e);
        cell.IsCurrent = e.Name.Equals(CurrentName, StringComparison.OrdinalIgnoreCase);
        return cell;
    }

    private static int SourceRank(IconEntry e) => e.IsVanilla ? 0 : e.IsAmp ? 1 : 2;

    private void Refilter()
    {
        var svc = IconLibraryService.Current;
        OnPropertyChanged(nameof(IsLoading));
        OnPropertyChanged(nameof(GameMissing));
        if (svc == null)
        {
            _filtered = [];
            RebuildRows();
            return;
        }

        var allowed = KindsFor(_target);
        var tabKind = KindTabs.FirstOrDefault(t => t.IsActive)?.Kind;
        var candidates = svc.Library.All.Where(e => allowed.Contains(e.Kind) && (tabKind == null || e.Kind == tabKind)).ToList();

        // Sources present among what the tab shows, vanilla and AMP first.
        var sources = candidates.OrderBy(SourceRank).Select(e => e.Source).Distinct(StringComparer.OrdinalIgnoreCase)
            .Prepend(Loc.Instance["LblIconSourceAll"]).ToList();
        if (!sources.SequenceEqual(Sources))
        {
            var keep = SelectedSource;
            Sources.Clear();
            foreach (var s in sources) Sources.Add(s);
            if (!Sources.Contains(keep)) { SelectedSource = Sources[0]; return; }
            SelectedSource = keep;
        }

        var source = SelectedSource;
        if (!string.IsNullOrEmpty(source) && source != Loc.Instance["LblIconSourceAll"])
            candidates = candidates.Where(e => e.Source.Equals(source, StringComparison.OrdinalIgnoreCase)).ToList();

        var query = SearchText.Trim();
        if (query.Length > 0)
        {
            EnsureSearchKeys(svc);
            var terms = query.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            candidates = candidates.Where(e =>
            {
                var key = _searchKeys?.GetValueOrDefault(e.Name) ?? e.Name;
                return terms.All(t => key.Contains(t, StringComparison.OrdinalIgnoreCase));
            }).ToList();
        }

        _filtered = candidates
            .OrderBy(SourceRank)
            .ThenBy(e => e.Source, StringComparer.OrdinalIgnoreCase)
            .ThenBy(e => e.Name, StringComparer.OrdinalIgnoreCase)
            .Select(Cell)
            .ToList();
        Count = _filtered.Count;
        OnPropertyChanged(nameof(HasNoResults));
        RebuildRows();
    }

    private void RebuildRows()
    {
        Rows.Clear();
        for (int i = 0; i < _filtered.Count; i += _columns)
            Rows.Add(new IconRowVM(_filtered.GetRange(i, Math.Min(_columns, _filtered.Count - i))));
    }

    /// <summary>
    /// Search text per icon: its name and the names of the spells, statuses and passives that use
    /// it, so "fireball" finds the icon Fireball uses. Built once, in the background.
    /// </summary>
    private void EnsureSearchKeys(IconLibraryService svc)
    {
        if (_searchKeys != null || _buildingSearchKeys) return;
        _buildingSearchKeys = true;
        var displayName = DisplayNameOf;
        Task.Run(() =>
        {
            var keys = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var e in svc.Library.All)
            {
                if (!svc.UsedBy.TryGetValue(e.Name, out var users)) { keys[e.Name] = e.Name; continue; }
                var parts = new List<string> { e.Name };
                foreach (var (stat, _) in users.Take(12))
                {
                    parts.Add(stat);
                    if (displayName?.Invoke(stat) is { Length: > 0 } dn) parts.Add(dn);
                }
                keys[e.Name] = string.Join(" ", parts);
            }
            return keys;
        }).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            _buildingSearchKeys = false;
            if (!t.IsCompletedSuccessfully) return;
            _searchKeys = t.Result;
            if (IsOpen && SearchText.Trim().Length > 0) Refilter();
        }));
    }

    partial void OnSelectedChanged(IconCellVM? oldValue, IconCellVM? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null) newValue.IsSelected = true;
        OnPropertyChanged(nameof(HasSelection));
        DetailLarge = null;
        DetailTile = null;
        DetailWarning = "";
        if (newValue == null || IconLibraryService.Current is not { } svc)
        {
            DetailName = DetailInfo = DetailUsedBy = "";
            OnPropertyChanged(nameof(HasDetailWarning));
            return;
        }

        var e = newValue.Entry;
        DetailName = e.Name;
        var sizes = new List<string>();
        if (e.HasLarge) sizes.Add(Loc.Instance["LblIconTooltipPicture"]);
        if (e.HasTile) sizes.Add(Loc.Instance["LblIconHotbarTile"]);
        DetailInfo = $"{KindLabel(e.Kind)} · {e.Source}" + (sizes.Count > 0 ? "\n" + string.Join(" · ", sizes) : "");
        DetailUsedBy = svc.UsedBy.TryGetValue(e.Name, out var users)
            ? string.Join("\n", users.Take(8).Select(u => DisplayNameOf?.Invoke(u.stat) is { Length: > 0 } dn ? $"{dn}  ({u.stat})" : u.stat))
              + (users.Count > 8 ? $"\n… +{users.Count - 8}" : "")
            : "";
        if (!e.IsVanilla && !e.IsAmp)
            DetailWarning = string.Format(Loc.Instance["WarnIconFromMod"], e.Source);
        OnPropertyChanged(nameof(HasDetailWarning));

        Task.Run(() => (svc.Large(e), svc.Tile(e))).ContinueWith(t => Dispatcher.UIThread.Post(() =>
        {
            if (Selected != newValue || !t.IsCompletedSuccessfully) return;
            (DetailLarge, DetailTile) = t.Result;
        }));
    }

    public void Select(IconCellVM cell) => Selected = cell;

    /// <summary>The folder the user picked holds no game paks.</summary>
    public void GameFolderRejected(string? path) =>
        GameFolderError = string.Format(Loc.Instance["WarnNotGameFolder"], path ?? "");

    [RelayCommand]
    private void Choose()
    {
        if (Selected == null) return;
        var name = Selected.Name;
        var pick = _onPick;
        Close();
        pick?.Invoke(name);
    }

    public void ChooseCell(IconCellVM cell)
    {
        Selected = cell;
        Choose();
    }

    [RelayCommand]
    private void Inherit()
    {
        var inherit = _onInherit;
        Close();
        inherit?.Invoke();
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        _onPick = null;
        _onInherit = null;
    }
}
