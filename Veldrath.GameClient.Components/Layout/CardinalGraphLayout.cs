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
        // Only honour authored positions when their bounding grid is dense AND cardinally
        // consistent with the edge directions; otherwise use a compact edge-driven layout so
        // arrows and node placement agree visually.
        if (authoredNodes.Count > 0 &&
            IsDenseAuthoredLayout(authoredNodes) &&
            IsCardinallyConsistent(authoredNodes, orderedEdges))
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

                    var (dx, dy) = DirectionVector(edge.Direction);
                    positions[edge.ToNodeId] = ReserveDirectional(positions[nodeId], dx, dy, occupied);
                    queue.Enqueue(edge.ToNodeId);
                }
            }

            if (incoming.TryGetValue(nodeId, out var incomingEdges))
            {
                foreach (var edge in incomingEdges)
                {
                    if (positions.ContainsKey(edge.FromNodeId))
                        continue;

                    var (dx, dy) = DirectionVector(edge.Direction);
                    positions[edge.FromNodeId] = ReserveDirectional(positions[nodeId], -dx, -dy, occupied);
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
        CompactOccupiedGrid(positions);

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

    /// <summary>
    /// Removes empty rows and columns from the final grid so connectors only span the
    /// distance between occupied rows/columns. Mapping distinct coordinates preserves
    /// same-row/same-column relationships and therefore keeps every edge cardinal.
    /// </summary>
    private static void CompactOccupiedGrid(Dictionary<string, (int X, int Y)> positions)
    {
        if (positions.Count == 0)
            return;

        var xValues = positions.Values.Select(position => position.X).Distinct().Order().ToList();
        var yValues = positions.Values.Select(position => position.Y).Distinct().Order().ToList();

        var xIndex = xValues
            .Select((value, index) => (value, index))
            .ToDictionary(pair => pair.value, pair => pair.index);
        var yIndex = yValues
            .Select((value, index) => (value, index))
            .ToDictionary(pair => pair.value, pair => pair.index);

        foreach (var nodeId in positions.Keys.ToList())
        {
            var position = positions[nodeId];
            positions[nodeId] = (xIndex[position.X], yIndex[position.Y]);
        }
    }

    private static bool IsDenseAuthoredLayout(IReadOnlyList<MapNodeDto> authoredNodes)
    {
        var columns = authoredNodes.Select(node => node.LayoutX!.Value).Distinct().Count();
        var rows = authoredNodes.Select(node => node.LayoutY!.Value).Distinct().Count();
        var cells = columns * rows;

        // Allow a little breathing room for genuine authored layouts, but reject
        // tile-era grids that leave large holes (e.g. 14 nodes across a 5x5 grid).
        var allowedCells = authoredNodes.Count + Math.Max(2, authoredNodes.Count / 4);
        return cells <= allowedCells;
    }

    /// <summary>
    /// Returns whether the authored positions place each edge's target in the direction named
    /// by the edge (at least 80% of checked edges). If not, the authored grid is visually
    /// misleading and the compact edge-driven layout should be used instead.
    /// </summary>
    private static bool IsCardinallyConsistent(
        IReadOnlyList<MapNodeDto> authoredNodes,
        IReadOnlyList<MapEdgeDto> edges)
    {
        if (edges.Count == 0)
            return true;

        var positions = authoredNodes
            .Where(node => node.LayoutX is not null && node.LayoutY is not null)
            .ToDictionary(
                node => node.Id,
                node => (X: node.LayoutX!.Value, Y: node.LayoutY!.Value),
                StringComparer.OrdinalIgnoreCase);

        var checkedEdges = 0;
        var consistentEdges = 0;

        foreach (var edge in edges)
        {
            if (!positions.TryGetValue(edge.FromNodeId, out var from) ||
                !positions.TryGetValue(edge.ToNodeId, out var to))
            {
                continue;
            }

            checkedEdges++;
            if (MatchesDirectionHalfPlane(from, to, edge.Direction))
                consistentEdges++;
        }

        return checkedEdges == 0 ||
               consistentEdges >= Math.Ceiling(checkedEdges * 0.8);
    }

    private static bool MatchesDirectionHalfPlane(
        (int X, int Y) from,
        (int X, int Y) to,
        string direction)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;

        return direction.ToLowerInvariant() switch
        {
            "north" or "up" or "in" => dy < 0,
            "south" or "down" or "out" => dy > 0,
            "east" => dx > 0,
            "west" => dx < 0,
            _ => true,
        };
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
        // If the alignment cell is occupied, keep the short L-shaped route instead of
        // pushing the node into an unrelated cell that contradicts its direction.
        if (!occupied.Add(target))
            return;

        occupied.Remove(positions[nodeId]);
        positions[nodeId] = target;
    }

    private static bool IsVertical(string direction) =>
        direction.Equals("North", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("South", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("Up", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("Down", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("In", StringComparison.OrdinalIgnoreCase) ||
        direction.Equals("Out", StringComparison.OrdinalIgnoreCase);

    private static (int Dx, int Dy) DirectionVector(string direction) =>
        direction.ToLowerInvariant() switch
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

    /// <summary>
    /// Reserves a free cell in the requested cardinal direction. The straight ray is tried
    /// first; if it is blocked, adjacent perpendicular offsets are tried so the target stays
    /// visually north/south/east/west of its origin instead of jumping to an unrelated cell.
    /// </summary>
    private static (int X, int Y) ReserveDirectional(
        (int X, int Y) origin,
        int dx,
        int dy,
        HashSet<(int X, int Y)> occupied)
    {
        // 90-degree rotation of the direction vector.
        var perpendicularX = dy;
        var perpendicularY = dx;

        for (var step = 1; step <= 128; step++)
        {
            var baseX = origin.X + dx * step;
            var baseY = origin.Y + dy * step;

            if (occupied.Add((baseX, baseY)))
                return (baseX, baseY);

            for (var offset = 1; offset <= 8; offset++)
            {
                var left = (baseX + perpendicularX * offset, baseY + perpendicularY * offset);
                if (occupied.Add(left))
                    return left;

                var right = (baseX - perpendicularX * offset, baseY - perpendicularY * offset);
                if (occupied.Add(right))
                    return right;
            }
        }

        return Reserve(origin.X + dx, origin.Y + dy, occupied);
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
