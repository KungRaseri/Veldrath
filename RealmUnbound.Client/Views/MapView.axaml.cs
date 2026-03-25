using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using RealmUnbound.Client.ViewModels;
using System.Collections.Specialized;

namespace RealmUnbound.Client.Views;

/// <summary>Code-behind for <see cref="MapView"/>. Handles edge-line rendering on <c>EdgeCanvas</c>
/// and gesture routing to <see cref="MapViewModel"/> commands.</summary>
public partial class MapView : UserControl
{
    private MapViewModel? _vm;
    private Canvas? _edgeCanvas;

    /// <summary>Initializes a new instance of <see cref="MapView"/>.</summary>
    public MapView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_vm is not null)
        {
            _vm.Nodes.CollectionChanged -= OnGraphChanged;
            _vm.Edges.CollectionChanged -= OnGraphChanged;
        }

        _vm = DataContext as MapViewModel;

        if (_vm is not null)
        {
            _vm.Nodes.CollectionChanged += OnGraphChanged;
            _vm.Edges.CollectionChanged += OnGraphChanged;
        }
    }

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _edgeCanvas = this.FindControl<Canvas>("EdgeCanvas");
        RedrawEdges();
    }

    private void OnGraphChanged(object? sender, NotifyCollectionChangedEventArgs e) =>
        Dispatcher.UIThread.Post(RedrawEdges, DispatcherPriority.Render);

    private void RedrawEdges()
    {
        if (_edgeCanvas is null || _vm is null) return;

        _edgeCanvas.Children.Clear();

        foreach (var edge in _vm.Edges)
        {
            // Centre of each node (node border is 96×56, anchored at top-left)
            double x1 = edge.From.X + 48;
            double y1 = edge.From.Y + 28;
            double x2 = edge.To.X + 48;
            double y2 = edge.To.Y + 28;

            var (stroke, dash) = EdgeStyle(edge.EdgeType, edge.IsTraversable);

            var line = new Avalonia.Controls.Shapes.Line
            {
                StartPoint = new Point(x1, y1),
                EndPoint = new Point(x2, y2),
                Stroke = stroke,
                StrokeThickness = edge.IsTraversable ? 1.5 : 1.0,
                StrokeDashArray = dash,
                Opacity = edge.IsTraversable ? 0.7 : 0.35,
            };

            _edgeCanvas.Children.Add(line);
        }
    }

    private (IBrush stroke, Avalonia.Collections.AvaloniaList<double>? dash) EdgeStyle(string edgeType, bool traversable)
    {
        if (!traversable)
            return (Brushes.Gray, [4.0, 4.0]);

        return edgeType switch
        {
            "portal" => (new SolidColorBrush(Color.Parse("#8B5CF6")), null),
            "secret_passage" => (new SolidColorBrush(Color.Parse("#C9A84C")), [6.0, 3.0]),
            "dungeon_entrance" => (new SolidColorBrush(Color.Parse("#EF4444")), null),
            _ => (new SolidColorBrush(Color.Parse("#6B7280")), null),
        };
    }

    // ── Gesture handlers ────────────────────────────────────────────────────

    /// <summary>Pointer press — select the node.</summary>
    internal void OnNodePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_vm is null) return;
        var node = GetNodeFromSender(sender);
        if (node is not null)
            _vm.SelectNodeCommand.Execute(node).Subscribe();
    }

    /// <summary>Double tap — drill into the node.</summary>
    internal void OnNodeDoubleTapped(object? sender, TappedEventArgs e)
    {
        if (_vm is null) return;
        var node = GetNodeFromSender(sender);
        if (node is not null)
            _vm.DrillIntoCommand.Execute(node).Subscribe();
    }

    private static MapNodeViewModel? GetNodeFromSender(object? sender) =>
        (sender as StyledElement)?.DataContext as MapNodeViewModel;
}
