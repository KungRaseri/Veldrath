using ReactiveUI;

namespace Veldrath.Client.ViewModels;

/// <summary>Represents a single node (Region, Zone, or ZoneLocation) on the traversal-graph map canvas.</summary>
public class MapNodeViewModel : ViewModelBase
{
    private double _x;
    private double _y;
    private bool _isCurrent;
    private bool _isSelected;

    /// <summary>Initializes a new instance of <see cref="MapNodeViewModel"/>.</summary>
    public MapNodeViewModel(string id, string label, string nodeType)
    {
        Id = id;
        Label = label;
        NodeType = nodeType;
    }

    /// <summary>Unique identifier matching the entity slug or ID (Region.Id, Zone.Id, ZoneLocation.Slug).</summary>
    public string Id { get; }

    /// <summary>Display name shown on the node.</summary>
    public string Label { get; }

    /// <summary>Graph level this node belongs to: <c>"region"</c>, <c>"zone"</c>, or <c>"location"</c>.</summary>
    public string NodeType { get; }

    /// <summary>Whether the player's character is currently at this node.</summary>
    public bool IsCurrent
    {
        get => _isCurrent;
        set => this.RaiseAndSetIfChanged(ref _isCurrent, value);
    }

    /// <summary>Whether this node is currently selected by the user.</summary>
    public bool IsSelected
    {
        get => _isSelected;
        set => this.RaiseAndSetIfChanged(ref _isSelected, value);
    }

    /// <summary>Whether this node represents an undiscovered hidden location (rendered as <c>?</c>).</summary>
    public bool IsHidden { get; init; }

    /// <summary>The label displayed on the node canvas; returns <c>"???"</c> for hidden undiscovered locations.</summary>
    public string DisplayLabel => IsHidden ? "???" : Label;

    /// <summary>Whether this node represents a Region in the world hierarchy.</summary>
    public bool IsRegionNode => NodeType == "region";

    /// <summary>Optional sub-type label (e.g. zone type, region type, location type).</summary>
    public string? SubType { get; init; }

    /// <summary>Minimum recommended level for this node, or <see langword="null"/> if not applicable.</summary>
    public int? MinLevel { get; init; }

    /// <summary>Region ID this zone node belongs to; <see langword="null"/> for region header nodes and standalone nodes.</summary>
    public string? RegionId { get; init; }

    /// <summary>Display name of the parent region, used by the layout algorithm when grouping zone clusters.</summary>
    public string? RegionLabel { get; init; }

    /// <summary>Whether this node is a non-interactive region header label rendered above a zone cluster.</summary>
    public bool IsRegionHeader => NodeType == "region_header";

    /// <summary>Canvas X position assigned by the layout algorithm.</summary>
    public double X
    {
        get => _x;
        set => this.RaiseAndSetIfChanged(ref _x, value);
    }

    /// <summary>Canvas Y position assigned by the layout algorithm.</summary>
    public double Y
    {
        get => _y;
        set => this.RaiseAndSetIfChanged(ref _y, value);
    }
}
