using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using ParaTool.App.ViewModels;

namespace ParaTool.App.Views;

public partial class ItemEditorView : UserControl
{
    public ItemEditorView()
    {
        InitializeComponent();
        AddHandler(Button.ClickEvent, OnButtonClick);
    }

    private void OnButtonClick(object? sender, RoutedEventArgs e)
    {
        if (e.Source is Button btn && btn.Name == "ExpandBtn" && btn.Tag is ModVM mod)
        {
            mod.IsExpanded = !mod.IsExpanded;
            btn.Content = mod.IsExpanded ? "v" : ">";
        }
    }
}
