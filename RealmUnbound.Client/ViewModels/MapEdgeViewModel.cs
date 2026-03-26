using Avalonia;
using Avalonia.Collections;
using Avalonia.Media;
using ReactiveUI;
using System.Reactive.Linq;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a directed edge between two <see cref="MapNodeViewModel"/> instances on the traversal-graph map canvas.</summary>
public class MapEdgeViewModel : ViewModelBase
{
    private const double NodeHalfW = 48.0; // 96 / 2
    private const double NodeHalfH = 28.0; // 56 / 2

    private Point _startPoint;
    private Point _endPoint;

    /// <summary>Initializes a new instance of <see cref="MapEdgeViewModel"/>.</summary>
    public MapEdgeViewModel(MapNodeViewModel from, MapNodeViewModel to, string edgeType, bool isTraversable = true)
    {
        From = from;
        To = to;
        EdgeType = edgeType;
        IsTraversable = isTraversable;

        (Stroke, StrokeDash, StrokeThickness, Opacity) = ComputeStyle(edgeType, isTraversable);

        // Keep line endpoints in sync with node canvas positions.
        from.WhenAnyValue(n => n.X, n => n.Y, (x, y) => (x, y))
            .CombineLatest(to.WhenAnyValue(n => n.X, n => n.Y, (x, y) => (x, y)))
            .Subscribe(p =>
            {
                StartPoint = new Point(p.First.x + NodeHalfW,  p.First.y  + NodeHalfH);
                EndPoint   = new Point(p.Second.x + NodeHalfW, p.Second.y + NodeHalfH);
            });
    }

    /// <summary>Origin node of this edge.</summary>
    public MapNodeViewModel From { get; }

    /// <summary>Destination node of this edge.</summary>
    public MapNodeViewModel To { get; }

    /// <summary>
    /// Classification of this travel edge.
    /// Values: <c>"path"</c>, <c>"portal"</c>, <c>"dungeon_entrance"</c>, <c>"secret_passage"</c>,
    /// <c>"zone_exit"</c>, <c>"region_exit"</c>.
    /// </summary>
    public string EdgeType { get; }

    /// <summary>Whether the player can currently traverse this edge.</summary>
    public bool IsTraversable { get; }

    /// <summary>Canvas position of the origin node centre; updates automatically when the node moves.</summary>
    public Point StartPoint
    {
        get => _startPoint;
        private set => this.RaiseAndSetIfChanged(ref _startPoint, value);
    }

    /// <summary>Canvas position of the destination node centre; updates automatically when the node moves.</summary>
    public Point EndPoint
    {
        get => _endPoint;
        private set => this.RaiseAndSetIfChanged(ref _endPoint, value);
    }

    /// <summary>Stroke brush for the edge line.</summary>
    public IBrush Stroke { get; }

    /// <summary>Stroke thickness for the edge line in pixels.</summary>
    public double StrokeThickness { get; }

    /// <summary>Dash pattern for the edge line, or <see langword="null"/> for a solid line.</summary>
    public AvaloniaList<double>? StrokeDash { get; }

    /// <summary>Opacity of the edge line.</summary>
    public double Opacity { get; }

    private static (IBrush stroke, AvaloniaList<double>? dash, double thickness, double opacity)
        ComputeStyle(string edgeType, bool traversable)
    {
        if (!traversable)
            return (Brushes.Gray, new AvaloniaList<double> { 4.0, 4.0 }, 1.0, 0.35);

        return edgeType switch
        {
            "portal"           => (new SolidColorBrush(Color.Parse("#8B5CF6")), null,                             2.0, 0.8),
            "secret_passage"   => (new SolidColorBrush(Color.Parse("#C9A84C")), new AvaloniaList<double> { 6.0, 3.0 }, 2.0, 0.8),
            "dungeon_entrance" => (new SolidColorBrush(Color.Parse("#EF4444")), null,                             2.0, 0.8),
            _                  => (new SolidColorBrush(Color.Parse("#6B7280")), null,                             1.5, 0.65),
        };
    }
}
