using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;
using Veldrath.Contracts.Map;
using Veldrath.GameClient.Components.Layout;

namespace Veldrath.GameClient.Components.Components.Shared;

/// <summary>
/// Text-first cardinal map. Nodes are rendered as CSS cards on an authored/auto-computed grid,
/// connected by horizontal and vertical orthogonal lines. Arrow keys move selection to the
/// nearest node in that direction; Enter requests travel to the selected node.
/// </summary>
public partial class CardinalMap
{
    private const int CellWidth = 200;
    private const int CellHeight = 112;
    private const int NodeWidth = 168;
    private const int NodeHeight = 80;

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
                (p.X - _minX) * CellWidth + (CellWidth - NodeWidth) / 2,
                (p.Y - _minY) * CellHeight + (CellHeight - NodeHeight) / 2),
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

            AddConnector(connectors, from, to);
        }

        _connectors = connectors;
    }

    private static void AddConnector(
        ICollection<MapConnector> connectors,
        NodePixel from,
        NodePixel to)
    {
        const int thickness = 2;
        var fromX = from.Left + NodeWidth / 2;
        var fromY = from.Top + NodeHeight / 2;
        var toX = to.Left + NodeWidth / 2;
        var toY = to.Top + NodeHeight / 2;

        if (fromY == toY)
        {
            connectors.Add(new MapConnector(
                Math.Min(fromX, toX),
                fromY - thickness / 2,
                Math.Abs(toX - fromX),
                thickness,
                IsHorizontal: true));
            return;
        }

        if (fromX == toX)
        {
            connectors.Add(new MapConnector(
                fromX - thickness / 2,
                Math.Min(fromY, toY),
                thickness,
                Math.Abs(toY - fromY),
                IsHorizontal: false));
            return;
        }

        connectors.Add(new MapConnector(
            Math.Min(fromX, toX),
            fromY - thickness / 2,
            Math.Abs(toX - fromX),
            thickness,
            IsHorizontal: true));

        connectors.Add(new MapConnector(
            toX - thickness / 2,
            Math.Min(fromY, toY),
            thickness,
            Math.Abs(toY - fromY),
            IsHorizontal: false));
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
            if (selected is not null)
                await OnTravel.InvokeAsync(selected);
            return;
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
        if (node.IsExitPoint)
            classes += " map-node-exit";
        if (node.IsHidden)
            classes += " map-node-hidden";

        return classes;
    }

    private string CanvasStyle =>
        $"width: {Math.Max(1, (_placements.Count == 0 ? 1 : _placements.Max(p => p.X) - _minX + 1)) * CellWidth}px; " +
        $"height: {Math.Max(1, (_placements.Count == 0 ? 1 : _placements.Max(p => p.Y) - _minY + 1)) * CellHeight}px;";

    private sealed record NodePixel(int Left, int Top);

    private sealed record MapConnector(int Left, int Top, int Width, int Height, bool IsHorizontal);
}
