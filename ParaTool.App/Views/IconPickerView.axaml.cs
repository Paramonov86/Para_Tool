using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using ParaTool.App.Localization;
using ParaTool.App.Services;
using ParaTool.App.ViewModels;

namespace ParaTool.App.Views;

public partial class IconPickerView : UserControl
{
    private const double CellWidth = 72; // 68 + margins

    public IconPickerView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick, RoutingStrategies.Bubble);
        AddHandler(DoubleTappedEvent, OnDoubleTapped, RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnKeyDown, RoutingStrategies.Tunnel);

        var scroll = this.FindControl<ScrollViewer>("GridScroll")!;
        scroll.SizeChanged += (_, e) => UpdateColumns(e.NewSize.Width);
        PropertyChanged += (_, e) =>
        {
            if (e.Property == IsVisibleProperty && IsVisible)
            {
                UpdateColumns(scroll.Bounds.Width);
                Avalonia.Threading.Dispatcher.UIThread.Post(() => this.FindControl<TextBox>("SearchBox")?.Focus(),
                    Avalonia.Threading.DispatcherPriority.Input);
            }
        };

        // A click on the backdrop outside the window closes it.
        var backdrop = this.FindControl<Border>("Backdrop")!;
        backdrop.PointerPressed += (_, e) =>
        {
            if (e.Source == backdrop && DataContext is IconPickerVM vm) vm.Close();
        };
    }

    private void UpdateColumns(double width)
    {
        if (DataContext is IconPickerVM vm && width > 0)
            vm.SetColumns((int)((width - 24) / CellWidth));
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is not Button btn || DataContext is not IconPickerVM vm) return;
        if (btn.Tag is IconCellVM cell)
        {
            vm.Select(cell);
            e.Handled = true;
        }
        else if (btn.Name == "ChooseGameFolderBtn")
        {
            _ = ChooseGameFolderAsync();
            e.Handled = true;
        }
    }

    private void OnDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is not IconPickerVM vm) return;
        if (e.Source is Visual v && FindCell(v) is { } cell)
        {
            vm.ChooseCell(cell);
            e.Handled = true;
        }
    }

    private static IconCellVM? FindCell(Visual? v)
    {
        for (; v != null; v = v.GetVisualParent())
            if (v is Button { Tag: IconCellVM cell }) return cell;
        return null;
    }

    private void OnKeyDown(object? sender, KeyEventArgs e)
    {
        if (DataContext is not IconPickerVM vm || !vm.IsOpen) return;
        if (e.Key == Key.Escape) { vm.Close(); e.Handled = true; }
        else if (e.Key == Key.Enter && vm.HasSelection) { vm.ChooseCommand.Execute(null); e.Handled = true; }
    }

    private async Task ChooseGameFolderAsync()
    {
        var top = TopLevel.GetTopLevel(this);
        if (top == null) return;
        var folders = await top.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = Loc.Instance["BtnChooseGameFolder"],
            AllowMultiple = false,
        });
        if (folders.Count == 0) return;
        var path = folders[0].TryGetLocalPath();
        var data = ParaTool.Core.Services.GameDataLocator.Normalize(path);
        if (data == null)
        {
            if (DataContext is IconPickerVM vm) vm.GameFolderRejected(path);
            return;
        }
        var settings = UiSettingsService.Load();
        settings.GameDataPath = data;
        UiSettingsService.Save(settings);
        await IconLibraryService.RebuildAsync();
    }
}
