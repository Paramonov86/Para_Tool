using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace ParaTool.App.Controls;

public partial class CustomTitleBar : UserControl
{
    public CustomTitleBar()
    {
        InitializeComponent();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        var window = TopLevel.GetTopLevel(this) as Window;
        if (window == null) return;

        var dragRegion = this.FindControl<Border>("DragRegion");
        if (dragRegion != null)
        {
            dragRegion.PointerPressed += (_, args) =>
            {
                if (args.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
                    window.BeginMoveDrag(args);
            };
        }

        var minBtn = this.FindControl<Button>("MinimizeBtn");
        var maxBtn = this.FindControl<Button>("MaximizeBtn");
        var closeBtn = this.FindControl<Button>("CloseBtn");

        if (minBtn != null) minBtn.Click += (_, _) => window.WindowState = WindowState.Minimized;
        if (maxBtn != null) maxBtn.Click += (_, _) =>
            window.WindowState = window.WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
        if (closeBtn != null) closeBtn.Click += (_, _) => window.Close();
    }
}
