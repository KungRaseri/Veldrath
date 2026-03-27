using Avalonia.Collections;
using Avalonia.Media;
using ReactiveUI;
using System.Reactive.Linq;

namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a directed edge between two <see cref="MapNodeViewModel"/> instances on the traversal-graph map canvas.</summary>
public class MapEdgeViewModel : ViewModelBase
{
    private const double NodeHalfW         = 48.0;  // 96 / 2
    private const double NodeHalfH         = 28.0;  // 56 / 2
    /// <summary>Horizontal distance beyond which the edge routes orthogonally around the node grid.</summary>
    private const double ArcThreshold      = 280.0;
    /// <summary>Half of the inter-region gap (48 px total) — places the vertical segment at the mid-point between two region panels.</summary>
    private const double InterRegionHalfGap = 24.0;
    /// <summary>Extra pixels below the lower node centre to route the horizontal "bottom" segment, clearing the region panel border.</summary>
    private const double BelowPanelOffset   = 56.0;

    private string _pathData = string.Empty;

    /// <summary>Initializes a new instance of <see cref="MapEdgeViewModel"/>.</summary>
    public MapEdgeViewModel(MapNodeViewModel from, MapNodeViewModel to, string edgeType, bool isTraversable = true)
    {
        From = from;
        To = to;
        EdgeType = edgeType;
        IsTraversable = isTraversable;

        (Stroke, StrokeDash, StrokeThickness, Opacity) = ComputeStyle(edgeType, isTraversable);

        // Keep the path geometry in sync with node canvas positions.
        from.WhenAnyValue(n => n.X, n => n.Y, (x, y) => (x, y))
            .CombineLatest(to.WhenAnyValue(n => n.X, n => n.Y, (x, y) => (x, y)))
            .Subscribe(p =>
            {
                double x1 = p.First.x  + NodeHalfW;
                double y1 = p.First.y  + NodeHalfH;
                double x2 = p.Second.x + NodeHalfW;
                double y2 = p.Second.y + NodeHalfH;

                double dx = Math.Abs(x2 - x1);
                if (dx > ArcThreshold)
                {
                    // Orthogonal routing: down → right → up → right
                    // 1. Drop below both region panels from the source node.
                    // 2. Run right (or left) to the mid-point of the inter-region gap.
                    // 3. Rise to the destination node's vertical centre.
                    // 4. Enter the destination node horizontally.
                    double gapX   = x2 > x1
                        ? x2 - NodeHalfW - InterRegionHalfGap   // gap midpoint to the left of the dest panel
                        : x2 + NodeHalfW + InterRegionHalfGap;  // mirror for left-going edges
                    double belowY = Math.Max(y1, y2) + BelowPanelOffset;
                    PathData = FormattableString.Invariant(
                        $"M {x1:F1},{y1:F1} L {x1:F1},{belowY:F1} L {gapX:F1},{belowY:F1} L {gapX:F1},{y2:F1} L {x2:F1},{y2:F1}");
                }
                else
                {
                    PathData = FormattableString.Invariant($"M {x1:F1},{y1:F1} L {x2:F1},{y2:F1}");
                }
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

    /// <summary>Avalonia path markup data for rendering this edge — a straight <c>L</c> segment for
    /// short edges, or a five-point orthogonal route (<c>down → right → up → right</c>) for
    /// long cross-region edges, with the vertical segment placed at the mid-point of the
    /// inter-region gap between the two region panels.</summary>
    public string PathData
    {
        get => _pathData;
        private set => this.RaiseAndSetIfChanged(ref _pathData, value);
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
