namespace RealmUnbound.Client.Services;

/// <summary>
/// Computes 2-D positions for graph nodes using a deterministic top-to-bottom BFS layered layout.
/// The root node (the character's current location, or the first node when none is marked current)
/// is placed at layer 0.  Nodes reachable from the root in BFS order fill subsequent layers.
/// Disconnected nodes are collected into a shared final layer.  All positions are clamped to the
/// drawing area.
/// Writes the resulting <see cref="ViewModels.MapNodeViewModel.X"/> and
/// <see cref="ViewModels.MapNodeViewModel.Y"/> values directly onto each node.
/// </summary>
public static class GraphLayout
{
    private const double NodeWidth  = 96.0;
    private const double NodeHeight = 56.0;
    private const double HGap       = 24.0;   // preferred horizontal gap between siblings in the same layer
    private const double VStep      = 120.0;  // preferred vertical step between consecutive layers (top-to-top)
    private const double MarginX    = 20.0;
    private const double MarginY    = 32.0;

    /// <summary>
    /// Runs the layout algorithm and assigns canvas positions to every node in <paramref name="nodes"/>.
    /// </summary>
    /// <param name="nodes">Nodes to position.</param>
    /// <param name="edges">Edges defining the connections between nodes.</param>
    /// <param name="canvasWidth">Width of the drawing area in pixels.</param>
    /// <param name="canvasHeight">Height of the drawing area in pixels.</param>
    public static void Compute(
        IReadOnlyList<ViewModels.MapNodeViewModel> nodes,
        IReadOnlyList<ViewModels.MapEdgeViewModel> edges,
        double canvasWidth,
        double canvasHeight)
    {
        int n = nodes.Count;
        if (n == 0) return;

        if (n == 1)
        {
            nodes[0].X = (canvasWidth  - NodeWidth)  / 2;
            nodes[0].Y = (canvasHeight - NodeHeight) / 2;
            return;
        }

        var root   = nodes.FirstOrDefault(nd => nd.IsCurrent) ?? nodes[0];
        var layers = BuildLayers(nodes, edges, root);
        PlaceNodes(layers, canvasWidth, canvasHeight);
    }

    // ── BFS layering ─────────────────────────────────────────────────────────

    private static List<List<ViewModels.MapNodeViewModel>> BuildLayers(
        IReadOnlyList<ViewModels.MapNodeViewModel> nodes,
        IReadOnlyList<ViewModels.MapEdgeViewModel> edges,
        ViewModels.MapNodeViewModel root)
    {
        // Build an undirected adjacency list (edges are directional for traversal but
        // treated as undirected for layout purposes so the graph looks symmetric).
        var adj = nodes.ToDictionary(
            nd => nd,
            _ => new List<ViewModels.MapNodeViewModel>());

        foreach (var e in edges)
        {
            if (adj.ContainsKey(e.From)) adj[e.From].Add(e.To);
            if (adj.ContainsKey(e.To))   adj[e.To].Add(e.From);
        }

        // BFS from root — assigns each reachable node its layer depth.
        var depth = new Dictionary<ViewModels.MapNodeViewModel, int> { [root] = 0 };
        var queue = new Queue<ViewModels.MapNodeViewModel>();
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            foreach (var nb in adj[cur])
            {
                if (!depth.ContainsKey(nb))
                {
                    depth[nb] = depth[cur] + 1;
                    queue.Enqueue(nb);
                }
            }
        }

        // Disconnected nodes (not reachable from root) are collected into one final layer
        // so they render as a row rather than all stacking on top of each other.
        int orphanLayer = depth.Count > 0 ? depth.Values.Max() + 1 : 0;
        foreach (var nd in nodes)
        {
            if (!depth.ContainsKey(nd))
                depth[nd] = orphanLayer;
        }

        int maxDepth = depth.Values.Max();
        var result   = Enumerable.Range(0, maxDepth + 1)
            .Select(_ => new List<ViewModels.MapNodeViewModel>())
            .ToList();

        foreach (var nd in nodes)
            result[depth[nd]].Add(nd);

        return result;
    }

    // ── Node placement ────────────────────────────────────────────────────────

    private static void PlaceNodes(
        List<List<ViewModels.MapNodeViewModel>> layers,
        double canvasWidth,
        double canvasHeight)
    {
        int layerCount = layers.Count;

        // Scale VStep down when there are many layers so everything still fits vertically.
        double vStep;
        if (layerCount <= 1)
        {
            vStep = 0;
        }
        else
        {
            double availH = canvasHeight - 2 * MarginY - NodeHeight;
            vStep = Math.Min(VStep, availH / (layerCount - 1));
        }

        double totalH  = NodeHeight + (layerCount - 1) * vStep;
        double originY = Math.Max(MarginY, (canvasHeight - totalH) / 2.0);

        for (int li = 0; li < layerCount; li++)
        {
            var layer = layers[li];
            int nc    = layer.Count;
            double y  = originY + li * vStep;

            double startX, nodeStep;
            if (nc == 1)
            {
                startX   = (canvasWidth - NodeWidth) / 2.0;
                nodeStep = 0;
            }
            else
            {
                double totalW = nc * NodeWidth + (nc - 1) * HGap;
                if (totalW <= canvasWidth - 2 * MarginX)
                {
                    // Fits comfortably — centre the row.
                    startX   = (canvasWidth - totalW) / 2.0;
                    nodeStep = NodeWidth + HGap;
                }
                else
                {
                    // Too many nodes — scale the step down so the row stays within the canvas.
                    startX   = MarginX;
                    nodeStep = (canvasWidth - 2 * MarginX - NodeWidth) / (nc - 1);
                }
            }

            for (int ni = 0; ni < nc; ni++)
            {
                layer[ni].X = startX + ni * nodeStep;
                layer[ni].Y = y;
            }
        }
    }
}

