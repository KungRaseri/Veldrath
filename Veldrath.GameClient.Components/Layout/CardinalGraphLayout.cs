using Veldrath.Contracts.Map;

namespace Veldrath.GameClient.Components.Layout;

/// <summary>A node position produced by <see cref="CardinalGraphLayout"/>.</summary>
/// <param name="NodeId">Stable node identifier.</param>
/// <param name="X">Grid column.</param>
/// <param name="Y">Grid row.</param>
/// <param name="IsAuthored">Whether the node supplied an authored position.</param>
public sealed record CardinalNodePlacement(string NodeId, int X, int Y, bool IsAuthored);

/// <summary>
/// Deterministic grid layout for text map graphs. Authored <c>LayoutX/LayoutY</c> positions are
/// preferred; remaining nodes are placed cardinally from already-placed neighbours. Every grid
/// cell is unique, and computed neighbours are always aligned on a shared row or column.
/// </summary>
public static class CardinalGraphLayout
{
    /// <summary>Computes node placements for a map graph.</summary>
    /// <param name="nodes">Nodes to place.</param>
    /// <param name="edges">Optional edges used to place connected nodes cardinally.</param>
    /// <returns>One placement per node, in deterministic order.</returns>
    public static IReadOnlyList<CardinalNodePlacement> Compute(
        IReadOnlyList<MapNodeDto> nodes,
        IReadOnlyList<MapEdgeDto>? edges = null)
    {
        ArgumentNullException.ThrowIfNull(nodes);

        var orderedNodes = nodes
            .Where(node => !string.IsNullOrWhiteSpace(node.Id))
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.Id, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (orderedNodes.Count == 0)
            return [];

        var nodeIds = new HashSet<string>(
            orderedNodes.Select(node => node.Id),
            StringComparer.OrdinalIgnoreCase);

        var orderedEdges = (edges ?? [])
            .Where(edge =>
                edge.IsTraversable &&
                nodeIds.Contains(edge.FromNodeId) &&
                nodeIds.Contains(edge.ToNodeId) &&
                !string.Equals(edge.FromNodeId, edge.ToNodeId, StringComparison.OrdinalIgnoreCase))
            .OrderBy(edge => edge.FromNodeId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.ToNodeId, StringComparer.OrdinalIgnoreCase)
            .ThenBy(edge => edge.Direction, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var positions = new Dictionary<string, (int X, int Y)>(StringComparer.OrdinalIgnoreCase);
        var occupied = new HashSet<(int X, int Y)>();
        var authored = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Authored positions originated as tile-grid coordinates. Compact their distinct
        // X/Y values so large tile-era gaps do not create a huge, mostly empty text canvas
        // while preserving relative cardinal order.
        var authoredNodes = orderedNodes
            .Where(node => node.LayoutX is not null && node.LayoutY is not null)
            .ToList();

        // TMX-era authored coordinates are frequently sparse: a handful of nodes spread across
        // a 60x40 tile rectangle. Keeping those relative gaps produces a mostly empty text map.
        // Only honour authored positions when their bounding grid is reasonably dense;
        // otherwise fall back to a compact edge-driven layout.
        if (authoredNodes.Count > 0 && IsDenseAuthoredLayout(authoredNodes))
        {
            var xIndex = BuildCompactIndex(authoredNodes.Select(node => node.LayoutX!.Value));
            var yIndex = BuildCompactIndex(authoredNodes.Select(node => node.LayoutY!.Value));

            foreach (var node in authoredNodes)
            {
                var position = Reserve(xIndex[node.LayoutX!.Value], yIndex[node.LayoutY!.Value], occupied);
                positions[node.Id] = position;
                authored.Add(node.Id);
            }
        }
        else
        {
            // Seed the compact layout at the entry point so connected nodes radiate from arrival.
            var root = orderedNodes.FirstOrDefault(node => node.IsEntryPoint) ?? orderedNodes[0];
            positions[root.Id] = Reserve(0, 0, occupied);
        }

        var outgoing = orderedEdges
            .GroupBy(edge => edge.FromNodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var incoming = orderedEdges
            .GroupBy(edge => edge.ToNodeId, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(
                group => group.Key,
                group => group.ToList(),
                StringComparer.OrdinalIgnoreCase);

        var queue = new Queue<string>(
            positions.Keys.OrderBy(id => id, StringComparer.OrdinalIgnoreCase));
        var processed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        while (queue.Count > 0)
        {
            var nodeId = queue.Dequeue();
            if (!processed.Add(nodeId))
                continue;

            if (outgoing.TryGetValue(nodeId, out var outgoingEdges))
            {
                foreach (var edge in outgoingEdges)
                {
                    if (positions.ContainsKey(edge.ToNodeId))
                        continue;

                    var preferred = Offset(positions[nodeId], edge.Direction, forward: true);
                    positions[edge.ToNodeId] = Reserve(preferred.X, preferred.Y, occupied);
                    queue.Enqueue(edge.ToNodeId);
                }
            }

            if (incoming.TryGetValue(nodeId, out var incomingEdges))
            {
                foreach (var edge in incomingEdges)
                {
                    if (positions.ContainsKey(edge.FromNodeId))
                        continue;

                    var preferred = Offset(positions[nodeId], edge.Direction, forward: false);
                    positions[edge.FromNodeId] = Reserve(preferred.X, preferred.Y, occupied);
                    queue.Enqueue(edge.FromNodeId);
                }
            }
        }

        foreach (var node in orderedNodes)
        {
            if (!positions.ContainsKey(node.Id))
                positions[node.Id] = Reserve(0, 0, occupied);
        }

        AlignComputedEdges(orderedEdges, positions, occupied, authored);

        return orderedNodes
            .Select(node =>
            {
                var position = positions[node.Id];
                return new CardinalNodePlacement(
                    node.Id,
                    position.X,
                    position.Y,
                    authored.Contains(node.Id));
            })
            .ToList();
    }

    private static bool IsDenseAuthoredLayout(IReadOnlyList<MapNodeDto> authoredNodes)
    {
        var columns = authoredNodes.Select(node => node.LayoutX!.Value).Distinct().Count();
        var rows = authoredNodes.Select(node => node.LayoutY!.Value).Distinct().Count();
        var cells = columns * rows;
        return cells <= authoredNodes.Count * 2;
    }

    private static Dictionary<int, int> BuildCompactIndex(IEnumerable<int> values) =>
        values
            .Distinct()
            .OrderBy(value => value)
            .Select((value, index) => (value, index))
            .ToDictionary(pair => pair.value, pair => pair.index);

    /// <summary>Returns whether two placements share a row or column.</summary>
    /// <param name="first">First placement.</param>
    /// <param name="second">Second placement.</param>
    public static bool AreCardinal(CardinalNodePlacement first, CardinalNodePlacement second) =>
        first.X == second.X || first.Y == second.Y;

    private static void AlignComputedEdges(
        IReadOnlyList<MapEdgeDto> edges,
        Dictionary<string, (int X, int Y)> positions,
        HashSet<(int X, int Y)> occupied,
        HashSet<string> authored)
    {
        foreach (var edge in edges)
        {
            var from = positions[edge.FromNodeId];
            var to = positions[edge.ToNodeId];

            if (from.X == to.X || from.Y == to.Y)
                continue;

            var vertical = IsVertical(edge.Direction);

            if (!authored.Contains(edge.ToNodeId))
            {
                var target = vertical ? (from.X, to.Y) : (to.X, from.Y);
                MoveNode(edge.ToNodeId, target, positions, occupied);
            }
            else if (!authored.Contains(edge.FromNodeId))
            {
                var target = vertical ? (to.X, from.Y) : (from.X, to.Y);
                MoveNode(edge.FromNodeId, target, positions, occupied);
            }
        }
    }

    private static void MoveNode(
        string nodeId,
        (int X, int Y) target,
        Dictionary<string, (int X, int Y)> positions,
        HashSet<(int X, int Y)> occupied)
    {
        occupied.Remove(positions[nodeId]);
        positions[nodeId] = Reserve(target.X, target.Y, occupied);
    }

    private static bool IsVertical(string direction) =>
        direction.Equals("North", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("South", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("Up", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("Down", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("In", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("Out", StringComparison.OrdinalIgnoreCase);

    private static (int X, int Y) Offset(
        (int X, int Y) origin,
        string direction,
        bool forward)
    {
        var (dx, dy) = direction.ToLowerInvariant() switch
        {
            "north" => (0, -1),
            "south" => (0, 1),
            "east" => (1, 0),
            "west" => (-1, 0),
            "up" => (0, -1),
            "down" => (0, 1),
            "in" => (0, -1),
            "out" => (0, 1),
            _ => (0, -1),
        };

        return forward
            ? (origin.X + dx, origin.Y + dy)
            : (origin.X - dx, origin.Y - dy);
    }

    private static (int X, int Y) Reserve(
        int preferredX,
        int preferredY,
        HashSet<(int X, int Y)> occupied)
    {
        if (occupied.Add((preferredX, preferredY)))
            return (preferredX, preferredY);

        for (var radius = 1; radius <= 1024; radius++)
        {
            for (var dx = -radius; dx <= radius; dx++)
            {
                var dy = radius - Math.Abs(dx);

                if (occupied.Add((preferredX + dx, preferredY + dy)))
                    return (preferredX + dx, preferredY + dy);

                if (dy > 0 && occupied.Add((preferredX + dx, preferredY - dy)))
                    return (preferredX + dx, preferredY - dy);
            }
        }

        throw new InvalidOperationException("Unable to find a free layout cell.");
    }
}
