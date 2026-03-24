namespace RealmUnbound.Client.ViewModels;

/// <summary>Represents a directed edge between two <see cref="MapNodeViewModel"/> instances on the traversal-graph map.</summary>
public class MapEdgeViewModel : ViewModelBase
{
    /// <summary>Initializes a new instance of <see cref="MapEdgeViewModel"/>.</summary>
    public MapEdgeViewModel(MapNodeViewModel from, MapNodeViewModel to, string edgeType, bool isTraversable = true)
    {
        From = from;
        To = to;
        EdgeType = edgeType;
        IsTraversable = isTraversable;
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
}
