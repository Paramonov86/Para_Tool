using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParaTool.App.ViewModels;

/// <summary>
/// A group of items in the constructor navigator (e.g. "AMP", "ModName").
/// Can be expanded/collapsed.
/// </summary>
public partial class NavGroupVM : ObservableObject
{
    public string Name { get; }
    public bool IsAmp { get; }
    public ObservableCollection<BaseItemVM> Items { get; }
    public int Count => Items.Count;

    [ObservableProperty] private bool _isExpanded;

    public IBrush HeaderBackground => IsAmp
        ? new SolidColorBrush(Color.Parse("#33C8A96E"))
        : new SolidColorBrush(Colors.Transparent);

    public NavGroupVM(string name, bool isAmp, IEnumerable<BaseItemVM> items)
    {
        Name = name;
        IsAmp = isAmp;
        Items = new ObservableCollection<BaseItemVM>(items);
    }
}
