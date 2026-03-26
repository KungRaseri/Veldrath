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

    // ── BFS layering ───────────────────────────────────────────────────────────────

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

    // ── Hierarchical cluster layout ───────────────────────────────────────

    /// <summary>
    /// Computes 2-D positions for a two-tier world graph: region nodes form a horizontal row
    /// at the top of the canvas; zone nodes cluster below their parent region in a 2-column grid.
    /// Region-region connections and region-zone membership are encoded in <paramref name="edges"/>.
    /// Falls back to <see cref="Compute"/> when no region/zone distinction is present.
    /// </summary>
    /// <param name="nodes">All nodes (regions + zones).</param>
    /// <param name="edges">All edges (region_exit + zone_membership).</param>
    /// <param name="canvasWidth">Width of the drawing area in pixels.</param>
    /// <param name="canvasHeight">Height of the drawing area in pixels.</param>
    public static void ComputeHierarchical(
        IReadOnlyList<ViewModels.MapNodeViewModel> nodes,
        IReadOnlyList<ViewModels.MapEdgeViewModel> edges,
        double canvasWidth,
        double canvasHeight)
    {
        var regionNodes = nodes.Where(n => n.NodeType == "region").ToList();
        var zoneNodes   = nodes.Where(n => n.NodeType == "zone").ToList();

        if (regionNodes.Count == 0)
        {
            Compute(nodes, edges, canvasWidth, canvasHeight);
            return;
        }

        // Map zone ID → parent region from zone_membership edges.
        var zoneParent = new Dictionary<string, ViewModels.MapNodeViewModel>();
        foreach (var e in edges)
            if (e.EdgeType == "zone_membership")
                zoneParent[e.To.Id] = e.From;

        // Group zone nodes by parent region ID.
        var zonesByRegion = new Dictionary<string, List<ViewModels.MapNodeViewModel>>();
        foreach (var z in zoneNodes)
        {
            if (!zoneParent.TryGetValue(z.Id, out var parent)) continue;
            if (!zonesByRegion.TryGetValue(parent.Id, out var list))
                zonesByRegion[parent.Id] = list = [];
            list.Add(z);
        }

        // Build undirected region adjacency from region_exit edges.
        var regionAdj = regionNodes.ToDictionary(r => r.Id, _ => new List<ViewModels.MapNodeViewModel>());
        foreach (var e in edges)
        {
            if (e.EdgeType != "region_exit") continue;
            if (regionAdj.ContainsKey(e.From.Id)) regionAdj[e.From.Id].Add(e.To);
            if (regionAdj.ContainsKey(e.To.Id))   regionAdj[e.To.Id].Add(e.From);
        }

        // BFS-ordered list of regions (root = current region or first region).
        var root           = regionNodes.FirstOrDefault(r => r.IsCurrent) ?? regionNodes[0];
        var orderedRegions = HierarchicalBfsOrder(regionNodes, regionAdj, root);

        // Layout constants.
        const int    ZoneCols        = 2;
        const double RegionToZoneGap = 52.0;
        const double ZoneRowGap      = 20.0;
        const double InterRegionGap  = 48.0;

        // Slot width = min width needed to lay out this region's zone cluster (2 per row).
        double SlotWidth(ViewModels.MapNodeViewModel r)
        {
            if (!zonesByRegion.TryGetValue(r.Id, out var zz) || zz.Count == 0)
                return NodeWidth;
            int cols = Math.Min(zz.Count, ZoneCols);
            return cols * NodeWidth + (cols - 1) * HGap;
        }

        double totalNeeded = orderedRegions.Sum(SlotWidth) + (orderedRegions.Count - 1) * InterRegionGap;
        double available   = canvasWidth - 2 * MarginX;
        double scale       = totalNeeded <= available ? 1.0 : available / totalNeeded;

        double regionY  = MarginY;
        double cursorX  = MarginX;

        foreach (var region in orderedRegions)
        {
            double slotW = SlotWidth(region) * scale;

            // Centre region node over its zone cluster.
            region.X = cursorX + (slotW - NodeWidth) / 2.0;
            region.Y = regionY;

            // Position zone nodes in a 2-column grid below the region.
            if (zonesByRegion.TryGetValue(region.Id, out var zones))
            {
                double gridLeft = cursorX;
                for (int i = 0; i < zones.Count; i++)
                {
                    int col = i % ZoneCols;
                    int row = i / ZoneCols;
                    zones[i].X = gridLeft + col * (NodeWidth + HGap) * scale;
                    zones[i].Y = regionY + NodeHeight + RegionToZoneGap + row * (NodeHeight + ZoneRowGap);
                }
            }

            cursorX += slotW + InterRegionGap * scale;
        }
    }

    private static List<ViewModels.MapNodeViewModel> HierarchicalBfsOrder(
        List<ViewModels.MapNodeViewModel> all,
        Dictionary<string, List<ViewModels.MapNodeViewModel>> adj,
        ViewModels.MapNodeViewModel root)
    {
        var visited = new HashSet<string> { root.Id };
        var queue   = new Queue<ViewModels.MapNodeViewModel>();
        var result  = new List<ViewModels.MapNodeViewModel> { root };
        queue.Enqueue(root);

        while (queue.Count > 0)
        {
            var cur = queue.Dequeue();
            if (!adj.TryGetValue(cur.Id, out var neighbours)) continue;
            foreach (var nb in neighbours)
            {
                if (visited.Add(nb.Id))
                {
                    result.Add(nb);
                    queue.Enqueue(nb);
                }
            }
        }

        // Orphan regions (not reachable from root via region_exit edges) appended at the end.
        foreach (var n in all)
            if (visited.Add(n.Id))
                result.Add(n);

        return result;
    }

    // ── Zone-grouped layout ───────────────────────────────────────────────

    /// <summary>
    /// Computes 2-D positions for a zone-centric world graph: non-interactive <c>region_header</c>
    /// label nodes are positioned above clusters of zone nodes grouped by
    /// <see cref="ViewModels.MapNodeViewModel.RegionId"/>.  Zone-to-zone edges render as direct
    /// lines between zone nodes; region header nodes carry no edges.
    /// Falls back to <see cref="Compute"/> when no zone nodes carry a <c>RegionId</c>.
    /// </summary>
    /// <param name="nodes">All nodes (<c>region_header</c> labels and <c>zone</c> nodes).</param>
    /// <param name="edges">Zone-to-zone traversal edges used to update reactive line endpoints.</param>
    /// <param name="canvasWidth">Width of the drawing area in pixels.</param>
    /// <param name="canvasHeight">Height of the drawing area in pixels.</param>
    public static void ComputeGroupedZones(
        IReadOnlyList<ViewModels.MapNodeViewModel> nodes,
        IReadOnlyList<ViewModels.MapEdgeViewModel> edges,
        double canvasWidth,
        double canvasHeight)
    {
        var headerNodes = nodes.Where(n => n.NodeType == "region_header").ToList();
        var zoneNodes   = nodes.Where(n => n.NodeType == "zone").ToList();

        if (headerNodes.Count == 0)
        {
            Compute(nodes, edges, canvasWidth, canvasHeight);
            return;
        }

        // Group zone nodes by RegionId, preserving the order regions appear in headerNodes.
        var zonesByRegion = new Dictionary<string, List<ViewModels.MapNodeViewModel>>();
        foreach (var z in zoneNodes)
        {
            if (string.IsNullOrEmpty(z.RegionId)) continue;
            if (!zonesByRegion.TryGetValue(z.RegionId, out var list))
                zonesByRegion[z.RegionId] = list = [];
            list.Add(z);
        }

        const int    ZoneCols        = 2;
        const double RegionToZoneGap = 52.0;
        const double ZoneRowGap      = 20.0;
        const double InterRegionGap  = 48.0;

        double SlotWidth(ViewModels.MapNodeViewModel header)
        {
            if (!zonesByRegion.TryGetValue(header.Id, out var zz) || zz.Count == 0)
                return NodeWidth;
            int cols = Math.Min(zz.Count, ZoneCols);
            return cols * NodeWidth + (cols - 1) * HGap;
        }

        double totalNeeded = headerNodes.Sum(SlotWidth) + (headerNodes.Count - 1) * InterRegionGap;
        double available   = canvasWidth - 2 * MarginX;
        double scale       = totalNeeded <= available ? 1.0 : available / totalNeeded;

        double regionY = MarginY;
        double cursorX = MarginX;

        foreach (var header in headerNodes)
        {
            double slotW = SlotWidth(header) * scale;

            // Centre the header label horizontally over its zone cluster.
            header.X = cursorX + (slotW - NodeWidth) / 2.0;
            header.Y = regionY;

            // Position zone nodes in a 2-column grid below the header.
            if (zonesByRegion.TryGetValue(header.Id, out var zones))
            {
                double gridLeft = cursorX;
                for (int i = 0; i < zones.Count; i++)
                {
                    int col = i % ZoneCols;
                    int row = i / ZoneCols;
                    zones[i].X = gridLeft + col * (NodeWidth + HGap) * scale;
                    zones[i].Y = regionY + NodeHeight + RegionToZoneGap + row * (NodeHeight + ZoneRowGap);
                }
            }

            cursorX += slotW + InterRegionGap * scale;
        }
    }
}