namespace RealmUnbound.Client.Services;

/// <summary>
/// Computes 2-D positions for graph nodes using a Fruchterman–Reingold spring-layout algorithm.
/// Writes the resulting <see cref="ViewModels.MapNodeViewModel.X"/> and
/// <see cref="ViewModels.MapNodeViewModel.Y"/> values directly onto each node.
/// </summary>
public static class GraphLayout
{
    private const int Iterations = 200;
    private const double NodeRadius = 40.0;

    /// <summary>
    /// Runs the layout algorithm and assigns canvas positions to every node in <paramref name="nodes"/>.
    /// </summary>
    /// <param name="nodes">Nodes to position.</param>
    /// <param name="edges">Edges defining attraction between nodes.</param>
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
            nodes[0].X = (canvasWidth - NodeRadius * 2) / 2;
            nodes[0].Y = (canvasHeight - NodeRadius * 2) / 2;
            return;
        }

        double area = canvasWidth * canvasHeight;
        double k = Math.Sqrt(area / n);
        double temperature = canvasWidth / 10.0;
        double cooling = temperature / (Iterations + 1);

        // Seed positions on a circle so we always start from a deterministic, non-degenerate layout
        double cx = canvasWidth / 2.0;
        double cy = canvasHeight / 2.0;
        double radius = Math.Min(canvasWidth, canvasHeight) * 0.35;

        var dx = new double[n];
        var dy = new double[n];

        for (int i = 0; i < n; i++)
        {
            double angle = 2 * Math.PI * i / n;
            nodes[i].X = cx + radius * Math.Cos(angle);
            nodes[i].Y = cy + radius * Math.Sin(angle);
        }

        for (int iter = 0; iter < Iterations; iter++)
        {
            for (int i = 0; i < n; i++) { dx[i] = 0; dy[i] = 0; }

            // Repulsion: every pair
            for (int i = 0; i < n; i++)
            {
                for (int j = i + 1; j < n; j++)
                {
                    double diffX = nodes[i].X - nodes[j].X;
                    double diffY = nodes[i].Y - nodes[j].Y;
                    double dist = Math.Max(Math.Sqrt(diffX * diffX + diffY * diffY), 0.01);
                    double force = k * k / dist;
                    double ux = diffX / dist;
                    double uy = diffY / dist;
                    dx[i] += ux * force;
                    dy[i] += uy * force;
                    dx[j] -= ux * force;
                    dy[j] -= uy * force;
                }
            }

            // Attraction: connected pairs
            foreach (var edge in edges)
            {
                int si = IndexOf(nodes, edge.From);
                int ti = IndexOf(nodes, edge.To);
                if (si < 0 || ti < 0) continue;

                double diffX = nodes[si].X - nodes[ti].X;
                double diffY = nodes[si].Y - nodes[ti].Y;
                double dist = Math.Max(Math.Sqrt(diffX * diffX + diffY * diffY), 0.01);
                double force = dist * dist / k;
                double ux = diffX / dist;
                double uy = diffY / dist;
                dx[si] -= ux * force;
                dy[si] -= uy * force;
                dx[ti] += ux * force;
                dy[ti] += uy * force;
            }

            // Apply displacements capped by temperature, clamped to canvas
            for (int i = 0; i < n; i++)
            {
                double mag = Math.Max(Math.Sqrt(dx[i] * dx[i] + dy[i] * dy[i]), 0.01);
                double capped = Math.Min(mag, temperature);
                nodes[i].X = Math.Clamp(nodes[i].X + dx[i] / mag * capped, NodeRadius, canvasWidth - NodeRadius);
                nodes[i].Y = Math.Clamp(nodes[i].Y + dy[i] / mag * capped, NodeRadius, canvasHeight - NodeRadius);
            }

            temperature = Math.Max(temperature - cooling, 0.01);
        }
    }

    private static int IndexOf(IReadOnlyList<ViewModels.MapNodeViewModel> nodes, ViewModels.MapNodeViewModel node)
    {
        for (int i = 0; i < nodes.Count; i++)
            if (ReferenceEquals(nodes[i], node)) return i;
        return -1;
    }
}
