using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Views;

/// <summary>Code-behind for <see cref="MapCanvasView"/>. Routes pointer gestures to <see cref="MapViewModel"/> commands.</summary>
public partial class MapCanvasView : UserControl
{
    /// <summary>Initializes a new instance of <see cref="MapCanvasView"/>.</summary>
    public MapCanvasView() => InitializeComponent();

    private MapViewModel? Vm => DataContext as MapViewModel;

    /// <summary>Pointer press — select the node.</summary>
    internal void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        var node = GetNodeFromSender(sender);
        if (node is not null)
            Vm?.SelectNodeCommand.Execute(node).Subscribe();
    }

    /// <summary>Double tap — also select the node (drill-down removed in flat graph view).</summary>
    internal void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        var node = GetNodeFromSender(sender);
        if (node is not null)
            Vm?.SelectNodeCommand.Execute(node).Subscribe();
    }

    private static MapNodeViewModel? GetNodeFromSender(object? sender) =>
        (sender as StyledElement)?.DataContext as MapNodeViewModel;
}
