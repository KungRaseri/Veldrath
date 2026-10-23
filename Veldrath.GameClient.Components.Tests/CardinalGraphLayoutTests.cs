using Veldrath.Contracts.Map;
using Veldrath.GameClient.Components.Layout;
using Xunit;

namespace Veldrath.GameClient.Components.Tests;

/// <summary>Tests for <see cref="CardinalGraphLayout"/>.</summary>
public class CardinalGraphLayoutTests
{
    [Fact]
    public void Compute_CompactsAuthoredTileCoordinates()
    {
        var nodes = new[]
        {
            Node("a", layoutX: 4, layoutY: 7, sortOrder: 1),
            Node("b", layoutX: 5, layoutY: 7, sortOrder: 2),
            Node("c", layoutX: 5, layoutY: 19, sortOrder: 3),
        };

        var placements = CardinalGraphLayout.Compute(nodes);

        var a = placements.Single(p => p.NodeId == "a");
        var b = placements.Single(p => p.NodeId == "b");
        var c = placements.Single(p => p.NodeId == "c");

        // Relative order is preserved, but the tile-era gaps are removed.
        Assert.Equal((0, 0), (a.X, a.Y));
        Assert.Equal((1, 0), (b.X, b.Y));
        Assert.Equal((1, 1), (c.X, c.Y));
        Assert.True(a.IsAuthored);
        Assert.True(b.IsAuthored);
        Assert.True(c.IsAuthored);
    }

    [Fact]
    public void Compute_KeepsSparseZoneGraphCompact()
    {
        // Crestfall's tile-era layout spreads 8 nodes across x=0..63 / y=0..40.
        // The compact graph layout must keep the whole map within a small text grid.
        var nodes = new[]
        {
            Node("entry", layoutX: 30, layoutY: 18, sortOrder: 0, isEntry: true),
            Node("aurelian", layoutX: 42, layoutY: 24, sortOrder: 20),
            Node("square", layoutX: 30, layoutY: 22, sortOrder: 30),
            Node("hearthbound", layoutX: 17, layoutY: 28, sortOrder: 40),
            Node("low-quarter", layoutX: 30, layoutY: 40, sortOrder: 50),
            Node("varenmark", layoutX: 30, layoutY: 0, sortOrder: 60),
            Node("ashlen", layoutX: 0, layoutY: 22, sortOrder: 70),
            Node("droveway", layoutX: 63, layoutY: 22, sortOrder: 80),
        };
        var edges = new[]
        {
            Edge("entry", "square", "South"),
            Edge("square", "ashlen", "West"),
            Edge("varenmark", "square", "South"),
            Edge("low-quarter", "square", "North"),
            Edge("hearthbound", "aurelian", "East"),
            Edge("square", "droveway", "East"),
            Edge("square", "low-quarter", "South"),
            Edge("ashlen", "square", "East"),
            Edge("square", "varenmark", "North"),
            Edge("droveway", "square", "West"),
            Edge("aurelian", "square", "West"),
        };

        var placements = CardinalGraphLayout.Compute(nodes, edges);

        var xs = placements.Select(p => p.X).ToList();
        var ys = placements.Select(p => p.Y).ToList();
        Assert.True(xs.Max() - xs.Min() <= 3, "The compact zone layout should span at most 4 columns.");
        Assert.True(ys.Max() - ys.Min() <= 2, "The compact zone layout should span at most 3 rows.");
        Assert.All(placements, placement => Assert.False(placement.IsAuthored));
    }

    [Fact]
    public void Compute_IgnoresSparseTileLayout()
    {
        // Four nodes at tile positions spread across a 60x40 area: the compact edge-driven
        // layout must win so the text map does not become a giant empty grid.
        var nodes = new[]
        {
            Node("a", layoutX: 0, layoutY: 0, sortOrder: 1),
            Node("b", layoutX: 10, layoutY: 5, sortOrder: 2),
            Node("c", layoutX: 40, layoutY: 20, sortOrder: 3),
            Node("d", layoutX: 60, layoutY: 40, sortOrder: 4),
        };
        var edges = new[]
        {
            Edge("a", "b", "East"),
            Edge("b", "c", "East"),
            Edge("c", "d", "East"),
        };

        var placements = CardinalGraphLayout.Compute(nodes, edges);

        var a = placements.Single(p => p.NodeId == "a");
        var b = placements.Single(p => p.NodeId == "b");
        var c = placements.Single(p => p.NodeId == "c");
        var d = placements.Single(p => p.NodeId == "d");

        Assert.Equal(0, a.X);
        Assert.Equal(1, b.X);
        Assert.Equal(2, c.X);
        Assert.Equal(3, d.X);
        Assert.All(placements, placement => Assert.Equal(0, placement.Y));
        Assert.All(placements, placement => Assert.False(placement.IsAuthored));
    }

    [Fact]
    public void Compute_ResolvesOverlappingAuthoredPositions()
    {
        var nodes = new[]
        {
            Node("a", layoutX: 0, layoutY: 0, sortOrder: 1),
            Node("b", layoutX: 0, layoutY: 0, sortOrder: 2),
        };

        var placements = CardinalGraphLayout.Compute(nodes);

        Assert.Equal(2, placements.Select(p => (p.X, p.Y)).Distinct().Count());
    }

    [Fact]
    public void Compute_PlacesOutgoingNeighbourCardinally()
    {
        var nodes = new[]
        {
            Node("a", layoutX: 0, layoutY: 0, sortOrder: 1),
            Node("b", sortOrder: 2),
        };
        var edges = new[] { Edge("a", "b", "North") };

        var placements = CardinalGraphLayout.Compute(nodes, edges);

        var a = placements.Single(p => p.NodeId == "a");
        var b = placements.Single(p => p.NodeId == "b");
        Assert.Equal(a.X, b.X);
        Assert.Equal(a.Y - 1, b.Y);
        Assert.False(b.IsAuthored);
    }

    [Fact]
    public void Compute_PlacesIncomingNeighbourCardinally()
    {
        var nodes = new[]
        {
            Node("a", sortOrder: 1),
            Node("b", layoutX: 5, layoutY: 5, sortOrder: 2),
        };
        var edges = new[] { Edge("a", "b", "East") };

        var placements = CardinalGraphLayout.Compute(nodes, edges);

        var a = placements.Single(p => p.NodeId == "a");
        var b = placements.Single(p => p.NodeId == "b");
        Assert.Equal(b.Y, a.Y);
        Assert.Equal(b.X - 1, a.X);
        Assert.True(CardinalGraphLayout.AreCardinal(a, b));
    }

    [Fact]
    public void Compute_PlacesDisconnectedNodesUniquely()
    {
        var nodes = new[]
        {
            Node("a", sortOrder: 1),
            Node("b", sortOrder: 2),
            Node("c", sortOrder: 3),
        };

        var placements = CardinalGraphLayout.Compute(nodes);

        Assert.Equal(3, placements.Count);
        Assert.Equal(3, placements.Select(p => (p.X, p.Y)).Distinct().Count());
    }

    [Fact]
    public void Compute_IsDeterministic()
    {
        var nodes = new[]
        {
            Node("a", layoutX: 2, layoutY: 2, sortOrder: 1),
            Node("b", sortOrder: 2),
            Node("c", sortOrder: 3),
            Node("d", layoutX: 2, layoutY: 2, sortOrder: 4),
        };
        var edges = new[]
        {
            Edge("a", "b", "East"),
            Edge("b", "c", "South"),
            Edge("d", "c", "West"),
        };

        var first = CardinalGraphLayout.Compute(nodes, edges);
        var second = CardinalGraphLayout.Compute(nodes, edges);

        Assert.Equal(
            first.Select(p => (p.NodeId, p.X, p.Y, p.IsAuthored)),
            second.Select(p => (p.NodeId, p.X, p.Y, p.IsAuthored)));
    }

    [Fact]
    public void Compute_AlignsComputedNodeToAuthoredTarget()
    {
        var nodes = new[]
        {
            Node("a", sortOrder: 1),
            Node("b", layoutX: 10, layoutY: 10, sortOrder: 2),
        };
        var edges = new[] { Edge("a", "b", "East") };

        var placements = CardinalGraphLayout.Compute(nodes, edges);

        var a = placements.Single(p => p.NodeId == "a");
        var b = placements.Single(p => p.NodeId == "b");
        Assert.True(CardinalGraphLayout.AreCardinal(a, b));
    }

    [Fact]
    public void Compute_ReturnsEmptyForNoNodes()
    {
        var placements = CardinalGraphLayout.Compute([], []);

        Assert.Empty(placements);
    }

    [Fact]
    public void AreCardinal_DetectsRowColumnAndDiagonal()
    {
        var origin = new CardinalNodePlacement("a", 0, 0, false);

        Assert.True(CardinalGraphLayout.AreCardinal(origin, new CardinalNodePlacement("b", 0, 3, false)));
        Assert.True(CardinalGraphLayout.AreCardinal(origin, new CardinalNodePlacement("c", -2, 0, false)));
        Assert.False(CardinalGraphLayout.AreCardinal(origin, new CardinalNodePlacement("d", 1, 1, false)));
    }

    private static MapNodeDto Node(
        string id,
        int? layoutX = null,
        int? layoutY = null,
        int sortOrder = 0,
        bool isEntry = false) =>
        new(
            id,
            "zone:test",
            "Location",
            id,
            id,
            $"{id} description",
            "place",
            layoutX,
            layoutY,
            IsEntryPoint: isEntry,
            IsExitPoint: false,
            TargetMapId: null,
            TargetNodeId: null,
            IsHidden: false,
            UnlockType: null,
            UnlockKey: null,
            DiscoverThreshold: null,
            sortOrder);

    private static MapEdgeDto Edge(string from, string to, string direction) =>
        new(
            Guid.NewGuid(),
            "zone:test",
            from,
            to,
            direction,
            "Walk",
            Label: null,
            IsTraversable: true,
            IsHidden: false);
}
