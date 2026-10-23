using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Veldrath.Contracts.Map;
using Veldrath.GameClient.Components.Layout;

namespace Veldrath.GameClient.Components.Components.Shared;

/// <summary>
/// Text-first cardinal map. Nodes are compact CSS cards on an authored/auto-computed grid,
/// connected by orthogonal lines with cardinal direction labels. Arrow keys prefer a
/// traversable graph edge in that direction, falling back to the nearest spatial node;
/// Enter requests travel to the selected node.
/// </summary>
public partial class CardinalMap
{
    // Compact text-map geometry: a 24px gutter between 160x64 node cards.
    private const int CellWidth = 184;
    private const int CellHeight = 96;
    private const int NodeWidth = 160;
    private const int NodeHeight = 64;
    private const int CanvasPadding = 24;
    private const int ConnectorThickness = 2;

    private IReadOnlyList<CardinalNodePlacement> _placements = [];
    private IReadOnlyList<MapConnector> _connectors = [];
    private int _minX;
    private int _minY;

    /// <summary>The map graph to render.</summary>
    [Parameter] public MapGraphDto? Map { get; set; }

    /// <summary>The node the character currently occupies.</summary>
    [Parameter] public string? CurrentNodeId { get; set; }

    /// <summary>The currently selected node, if different from the current node.</summary>
    [Parameter] public string? SelectedNodeId { get; set; }

    /// <summary>Raised when a node is selected by click or keyboard.</summary>
    [Parameter] public EventCallback<MapNodeDto> OnSelect { get; set; }

    /// <summary>Raised when travel is requested to the selected node.</summary>
    [Parameter] public EventCallback<MapNodeDto> OnTravel { get; set; }

    private string MapSubtitle =>
        $"{Map?.MapType} map · {Map?.Nodes.Count ?? 0} nodes · {Map?.Edges.Count ?? 0} routes";

    /// <inheritdoc />
    protected override void OnParametersSet() => RebuildLayout();

    private void RebuildLayout()
    {
        if (Map is null || Map.Nodes.Count == 0)
        {
            _placements = [];
            _connectors = [];
            return;
        }

        _placements = CardinalGraphLayout.Compute(Map.Nodes, Map.Edges);
        _minX = _placements.Min(p => p.X);
        _minY = _placements.Min(p => p.Y);

        var positions = _placements.ToDictionary(
            p => p.NodeId,
            p => new NodePixel(
                (p.X - _minX) * CellWidth + CanvasPadding,
                (p.Y - _minY) * CellHeight + CanvasPadding),
            StringComparer.OrdinalIgnoreCase);

        var connectors = new List<MapConnector>();
        var drawnPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var edge in Map.Edges)
        {
            if (!positions.TryGetValue(edge.FromNodeId, out var from) ||
                !positions.TryGetValue(edge.ToNodeId, out var to))
            {
                continue;
            }

            var pairKey = string.CompareOrdinal(edge.FromNodeId, edge.ToNodeId) < 0
                ? $"{edge.FromNodeId}|{edge.ToNodeId}"
                : $"{edge.ToNodeId}|{edge.FromNodeId}";

            if (!drawnPairs.Add(pairKey))
                continue;

            AddConnector(connectors, from, to, edge);
        }

        _connectors = connectors;
    }

    private static void AddConnector(
        ICollection<MapConnector> connectors,
        NodePixel from,
        NodePixel to,
        MapEdgeDto edge)
    {
        var fromCenterX = from.Left + NodeWidth / 2;
        var fromCenterY = from.Top + NodeHeight / 2;
        var toCenterX = to.Left + NodeWidth / 2;
        var toCenterY = to.Top + NodeHeight / 2;

        if (fromCenterY == toCenterY)
        {
            // Horizontal run between the facing card edges.
            var startX = toCenterX > fromCenterX ? from.Left + NodeWidth : from.Left;
            var endX = toCenterX > fromCenterX ? to.Left : to.Left + NodeWidth;
            var left = Math.Min(startX, endX);
            var width = Math.Abs(endX - startX);

            if (width < ConnectorThickness)
            {
                left = Math.Min(fromCenterX, toCenterX);
                width = Math.Abs(toCenterX - fromCenterX);
            }

            connectors.Add(new MapConnector(
                left,
                fromCenterY - ConnectorThickness / 2,
                Math.Max(ConnectorThickness, width),
                ConnectorThickness,
                IsHorizontal: true,
                edge.Direction,
                edge.Label,
                edge.IsTraversable,
                edge.IsHidden,
                left + width / 2 - 7,
                fromCenterY - 18));
            return;
        }

        if (fromCenterX == toCenterX)
        {
            // Vertical run between the facing card edges.
            var startY = toCenterY > fromCenterY ? from.Top + NodeHeight : from.Top;
            var endY = toCenterY > fromCenterY ? to.Top : to.Top + NodeHeight;
            var top = Math.Min(startY, endY);
            var height = Math.Abs(endY - startY);

            if (height < ConnectorThickness)
            {
                top = Math.Min(fromCenterY, toCenterY);
                height = Math.Abs(toCenterY - fromCenterY);
            }

            connectors.Add(new MapConnector(
                fromCenterX - ConnectorThickness / 2,
                top,
                ConnectorThickness,
                Math.Max(ConnectorThickness, height),
                IsHorizontal: false,
                edge.Direction,
                edge.Label,
                edge.IsTraversable,
                edge.IsHidden,
                fromCenterX + 8,
                top + height / 2 - 8));
            return;
        }

        // Rare L-shaped route for authored positions that are not cardinally aligned.
        // Horizontal segment: source card edge to the destination column at the source row.
        var horizontalStart = toCenterX > fromCenterX ? from.Left + NodeWidth : from.Left;
        var horizontalLeft = Math.Min(horizontalStart, toCenterX);
        var horizontalWidth = Math.Abs(toCenterX - horizontalStart);

        if (horizontalWidth >= ConnectorThickness)
        {
            connectors.Add(new MapConnector(
                horizontalLeft,
                fromCenterY - ConnectorThickness / 2,
                horizontalWidth,
                ConnectorThickness,
                IsHorizontal: true,
                edge.Direction,
                edge.Label,
                edge.IsTraversable,
                edge.IsHidden,
                horizontalLeft + horizontalWidth / 2 - 7,
                fromCenterY - 18));
        }

        // Vertical segment: source row to the facing edge of the destination card.
        var verticalEnd = toCenterY > fromCenterY ? to.Top : to.Top + NodeHeight;
        var verticalTop = Math.Min(fromCenterY, verticalEnd);
        var verticalHeight = Math.Abs(verticalEnd - fromCenterY);

        if (verticalHeight >= ConnectorThickness)
        {
            connectors.Add(new MapConnector(
                toCenterX - ConnectorThickness / 2,
                verticalTop,
                ConnectorThickness,
                verticalHeight,
                IsHorizontal: false,
                edge.Direction,
                edge.Label,
                edge.IsTraversable,
                edge.IsHidden,
                toCenterX + 8,
                verticalTop + verticalHeight / 2 - 8));
        }
    }

    private MapNodeDto? FindNode(string? nodeId) =>
        nodeId is null
            ? null
            : Map?.Nodes.FirstOrDefault(node =>
                string.Equals(node.Id, nodeId, StringComparison.OrdinalIgnoreCase));

    private async Task SelectNodeAsync(MapNodeDto node) =>
        await OnSelect.InvokeAsync(node);

    private async Task HandleKeyDown(KeyboardEventArgs args)
    {
        if (Map is null || _placements.Count == 0)
            return;

        var activeId = SelectedNodeId ?? CurrentNodeId;
        var active = _placements.FirstOrDefault(p =>
            string.Equals(p.NodeId, activeId, StringComparison.OrdinalIgnoreCase));

        if (active is null)
        {
            var first = FindNode(_placements[0].NodeId);
            if (first is not null)
                await SelectNodeAsync(first);
            return;
        }

        if (args.Key is "Enter" or " ")
        {
            var selected = FindNode(SelectedNodeId ?? activeId);
            if (selected is not null &&
                !string.Equals(selected.Id, CurrentNodeId, StringComparison.OrdinalIgnoreCase))
            {
                await OnTravel.InvokeAsync(selected);
            }
            return;
        }

        var direction = args.Key switch
        {
            "ArrowUp" => "north",
            "ArrowDown" => "south",
            "ArrowLeft" => "west",
            "ArrowRight" => "east",
            _ => null,
        };

        if (direction is not null)
        {
            // Prefer a real traversable edge so keyboard navigation follows the graph,
            // not just the visual grid.
            var edgeTarget = FindEdgeTarget(active.NodeId, direction);
            if (edgeTarget is not null)
            {
                var edgeNode = FindNode(edgeTarget);
                if (edgeNode is not null)
                {
                    await SelectNodeAsync(edgeNode);
                    return;
                }
            }
        }

        var target = args.Key switch
        {
            "ArrowUp" => FindNearest(active, dx: 0, dy: -1),
            "ArrowDown" => FindNearest(active, dx: 0, dy: 1),
            "ArrowLeft" => FindNearest(active, dx: -1, dy: 0),
            "ArrowRight" => FindNearest(active, dx: 1, dy: 0),
            _ => null,
        };

        if (target is not null)
        {
            var node = FindNode(target.NodeId);
            if (node is not null)
                await SelectNodeAsync(node);
        }
    }

    private string? FindEdgeTarget(string nodeId, string direction)
    {
        if (Map is null)
            return null;

        foreach (var edge in Map.Edges.Where(edge => edge.IsTraversable && !edge.IsHidden))
        {
            if (string.Equals(edge.FromNodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
                DirectionMatches(edge.Direction, direction))
            {
                return edge.ToNodeId;
            }

            if (string.Equals(edge.ToNodeId, nodeId, StringComparison.OrdinalIgnoreCase) &&
                DirectionMatches(edge.Direction, Opposite(direction)))
            {
                return edge.FromNodeId;
            }
        }

        return null;
    }

    private CardinalNodePlacement? FindNearest(CardinalNodePlacement origin, int dx, int dy)
    {
        return _placements
            .Where(p => !string.Equals(p.NodeId, origin.NodeId, StringComparison.OrdinalIgnoreCase))
            .Where(p => dx switch
            {
                < 0 => p.X < origin.X,
                > 0 => p.X > origin.X,
                _ => p.X == origin.X,
            })
            .Where(p => dy switch
            {
                < 0 => p.Y < origin.Y,
                > 0 => p.Y > origin.Y,
                _ => p.Y == origin.Y,
            })
            .OrderBy(p => Math.Abs(p.X - origin.X) + Math.Abs(p.Y - origin.Y))
            .ThenBy(p => p.NodeId, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static bool DirectionMatches(string edgeDirection, string requestedDirection) =>
        Normalize(edgeDirection) == Normalize(requestedDirection);

    private static string Opposite(string direction) => direction switch
    {
        "north" => "south",
        "south" => "north",
        "east" => "west",
        "west" => "east",
        _ => direction,
    };

    private static string Normalize(string direction) => direction.ToLowerInvariant() switch
    {
        "up" => "north",
        "down" => "south",
        "in" => "north",
        "out" => "south",
        _ => direction.ToLowerInvariant(),
    };

    private static string DirectionShort(string direction) => direction.ToLowerInvariant() switch
    {
        "north" or "up" => "N",
        "south" or "down" => "S",
        "east" => "E",
        "west" => "W",
        "in" => "In",
        "out" => "Out",
        _ => "•",
    };

    private static string IconFor(MapNodeDto node) => node.IconKey?.ToLowerInvariant() switch
    {
        "login" => Icons.Material.Filled.Login,
        "map" => Icons.Material.Filled.Map,
        "place" => Icons.Material.Filled.Place,
        "exit_to_app" => Icons.Material.Filled.ExitToApp,
        "public" => Icons.Material.Filled.Public,
        "stairs" => Icons.Material.Filled.Stairs,
        "flag" => Icons.Material.Filled.Flag,
        _ => Icons.Material.Filled.Circle,
    };

    private static string NodeClass(MapNodeDto node, string? currentNodeId, string? selectedNodeId)
    {
        var classes = "map-node-card";

        if (string.Equals(node.Id, currentNodeId, StringComparison.OrdinalIgnoreCase))
            classes += " map-node-current";
        if (string.Equals(node.Id, selectedNodeId, StringComparison.OrdinalIgnoreCase))
            classes += " map-node-selected";
        if (node.IsEntryPoint)
            classes += " map-node-entry";
        if (node.IsExitPoint)
            classes += " map-node-exit";
        if (node.IsHidden)
            classes += " map-node-hidden";

        return classes;
    }

    private static string ConnectorClass(MapConnector connector)
    {
        var classes = "map-connector";
        if (!connector.IsHorizontal)
            classes += " map-connector-vertical";
        if (!connector.IsTraversable)
            classes += " map-connector-blocked";
        if (connector.IsHidden)
            classes += " map-connector-hidden";
        return classes;
    }

    private static string ConnectorStyle(MapConnector connector) =>
        $"left:{connector.Left}px;top:{connector.Top}px;" +
        $"width:{Math.Max(1, connector.Width)}px;height:{Math.Max(1, connector.Height)}px;";

    private static string ConnectorLabelStyle(MapConnector connector) =>
        $"left:{connector.LabelLeft}px;top:{connector.LabelTop}px;";

    private static string ConnectorTitle(MapConnector connector)
    {
        var parts = new List<string> { connector.Direction };
        if (!string.IsNullOrWhiteSpace(connector.Label))
            parts.Add(connector.Label!);
        if (!connector.IsTraversable)
            parts.Add("blocked");
        if (connector.IsHidden)
            parts.Add("hidden");
        return string.Join(" · ", parts);
    }

    private string CanvasStyle
    {
        get
        {
            var columns = _placements.Count == 0 ? 1 : _placements.Max(p => p.X) - _minX + 1;
            var rows = _placements.Count == 0 ? 1 : _placements.Max(p => p.Y) - _minY + 1;
            return $"width:{columns * CellWidth + CanvasPadding * 2}px;" +
                   $"height:{rows * CellHeight + CanvasPadding * 2}px;";
        }
    }

    private sealed record NodePixel(int Left, int Top);

    private sealed record MapConnector(
        int Left,
        int Top,
        int Width,
        int Height,
        bool IsHorizontal,
        string Direction,
        string? Label,
        bool IsTraversable,
        bool IsHidden,
        int LabelLeft,
        int LabelTop);
}
