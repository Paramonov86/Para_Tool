using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace ParaTool.App.ViewModels;

/// <summary>
/// ObservableCollection that can be refilled wholesale with a single Reset
/// notification instead of one event per element. Used for list projections that
/// are recomputed as a whole (filter/sort passes), where N add/remove events would
/// make the bound panel do N times the work.
/// </summary>
public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void Reset(IEnumerable<T> items)
    {
        Items.Clear();
        foreach (var item in items)
            Items.Add(item);

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}
