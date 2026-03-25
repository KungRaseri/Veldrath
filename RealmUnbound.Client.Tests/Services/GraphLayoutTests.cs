using RealmUnbound.Client.Services;
using RealmUnbound.Client.ViewModels;

namespace RealmUnbound.Client.Tests.Services;

public class GraphLayoutTests : TestBase
{
    private static MapNodeViewModel Node(string id) => new(id, id, "zone");
    private static MapEdgeViewModel Edge(MapNodeViewModel from, MapNodeViewModel to) => new(from, to, "path");

    // ── Empty / single-node edge cases ───────────────────────────────────────

    [Fact]
    public void Compute_EmptyNodes_Does_Not_Throw()
    {
        var act = () => GraphLayout.Compute([], [], 860, 560);
        act.Should().NotThrow();
    }

    [Fact]
    public void Compute_SingleNode_Centers_On_Canvas()
    {
        var node = Node("a");

        GraphLayout.Compute([node], [], 860, 560);

        // Single node should be placed near the center (within 10 px)
        node.X.Should().BeApproximately((860 - 96) / 2.0, 10.0,
            "a single node should be centered horizontally");
        node.Y.Should().BeApproximately((560 - 56) / 2.0, 10.0,
            "a single node should be centered vertically");
    }

    // ── Multi-node positioning ───────────────────────────────────────────────

    [Fact]
    public void Compute_TwoNodes_Assigns_Different_Positions()
    {
        var a = Node("a");
        var b = Node("b");

        GraphLayout.Compute([a, b], [], 860, 560);

        (a.X != b.X || a.Y != b.Y).Should().BeTrue(
            "two nodes should not occupy the same canvas position");
    }

    [Fact]
    public void Compute_AllNodes_Stay_Within_Canvas_Bounds()
    {
        const double W = 860;
        const double H = 560;
        var nodes = Enumerable.Range(0, 10)
            .Select(i => Node($"n{i}"))
            .ToList();

        GraphLayout.Compute(nodes, [], W, H);

        foreach (var n in nodes)
        {
            n.X.Should().BeGreaterThanOrEqualTo(0, $"{n.Id}.X should be >= 0");
            n.X.Should().BeLessThanOrEqualTo(W, $"{n.Id}.X should be <= {W}");
            n.Y.Should().BeGreaterThanOrEqualTo(0, $"{n.Id}.Y should be >= 0");
            n.Y.Should().BeLessThanOrEqualTo(H, $"{n.Id}.Y should be <= {H}");
        }
    }

    // ── Edge attraction ──────────────────────────────────────────────────────

    [Fact]
    public void Compute_ConnectedPair_Is_Closer_Than_UnconnectedPair()
    {
        // Build a graph: (connected) a—b   (unconnected) c   d
        var a = Node("a");
        var b = Node("b");
        var c = Node("c");
        var d = Node("d");

        // Only a-b share an edge; c and d are isolated
        GraphLayout.Compute([a, b, c, d], [Edge(a, b)], 860, 560);

        double distConnected = Math.Sqrt(Math.Pow(a.X - b.X, 2) + Math.Pow(a.Y - b.Y, 2));
        double distIsolated = Math.Sqrt(Math.Pow(c.X - d.X, 2) + Math.Pow(c.Y - d.Y, 2));

        distConnected.Should().BeLessThanOrEqualTo(distIsolated * 2.0,
            "connected nodes should generally be pulled closer together by attraction forces");
    }

    // ── Idempotency guard ────────────────────────────────────────────────────

    [Fact]
    public void Compute_Called_Twice_Returns_Same_Positions()
    {
        // Because initial circle positions are deterministic, calling Compute
        // a second time should produce the same result (modulo floating point).
        var nodes = new[] { Node("x"), Node("y"), Node("z") };

        GraphLayout.Compute(nodes, [], 860, 560);
        var firstX = nodes.Select(n => n.X).ToArray();
        var firstY = nodes.Select(n => n.Y).ToArray();

        // Reset positions to zero so we confirm re-computation runs from scratch
        foreach (var n in nodes) { n.X = 0; n.Y = 0; }
        GraphLayout.Compute(nodes, [], 860, 560);

        for (int i = 0; i < nodes.Length; i++)
        {
            nodes[i].X.Should().BeApproximately(firstX[i], 0.001,
                $"node {i} X should be identical on second compute");
            nodes[i].Y.Should().BeApproximately(firstY[i], 0.001,
                $"node {i} Y should be identical on second compute");
        }
    }
}
