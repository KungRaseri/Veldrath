using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Seeders;
using RealmEngine.Data.Validation;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Seeders;

[Trait("Category", "Seeder")]
public class MapGraphSeederTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task SeedAsync_SeedsCompleteValidGraph()
    {
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var maps = await db.Maps
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .AsNoTracking()
            .ToListAsync();

        maps.Should().HaveCount(23);
        maps.SelectMany(m => m.Nodes).Should().HaveCount(136);
        maps.SelectMany(m => m.Edges).Should().HaveCount(306);

        foreach (var map in maps)
        {
            map.EntryNodeId.Should().NotBeNullOrWhiteSpace();
            map.Nodes.Should().Contain(n => n.Id == map.EntryNodeId);
            map.Nodes.Should().OnlyContain(n => n.MapId == map.Id);
            map.Edges.Should().OnlyContain(e => e.MapId == map.Id);

            var result = MapGraphValidator.Validate(ToGraph(map));
            result.IsValid.Should().BeTrue(
                $"{map.Id} should be structurally valid: " +
                string.Join("; ", result.Issues.Select(i => $"{i.Severity} {i.Code}: {i.Message}")));
        }
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent()
    {
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);
        await MapGraphSeeder.SeedAsync(db);

        (await db.Maps.CountAsync()).Should().Be(23);
        (await db.MapNodes.CountAsync()).Should().Be(136);
        (await db.MapEdges.CountAsync()).Should().Be(306);
    }

    [Fact]
    public async Task SeedAsync_EveryVisibleZoneLocationIsWalkableFromArrival()
    {
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var maps = await db.Maps
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .AsNoTracking()
            .Where(m => m.MapType == MapType.Zone)
            .ToListAsync();

        maps.Should().HaveCount(19, "the seeder authors 19 zone maps and every one of them has to be walkable");

        var unreachable = new List<string>();

        foreach (var map in maps)
        {
            var reachable = WalkVisibleGraphFromArrival(map);

            unreachable.AddRange(map.Nodes
                .Where(n => n.NodeType == MapNodeType.Location && !n.IsHidden)
                .Where(n => !reachable.Contains(n.Id))
                .OrderBy(n => n.Id, StringComparer.Ordinal)
                .Select(n => $"{map.Id} → {n.Id}"));
        }

        unreachable.Should().BeEmpty(
            "a visible location that Arrival cannot walk to is content no player can enter, and hiding a " +
            $"location must never cut the map in two; unreachable: {string.Join(", ", unreachable)}");
    }

    [Fact]
    public async Task SeedAsync_AddsBidirectionalBypassWalksAroundHiddenLocations()
    {
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var edges = await db.MapEdges.AsNoTracking().ToListAsync();

        // Measured on the seeded graph: the generated chain already owns every direction the authored edges left
        // free, so each bypass takes the next one. Both hops are asserted because a bypass that exists in one
        // direction only would walk the player past the hidden span and strand them there.
        AssertWalkBypass(
            edges, "zone:grevenmire",
            "zone:grevenmire:entry", "zone:grevenmire:location:mire-crossing",
            MapDirection.South, MapDirection.North);
        AssertWalkBypass(
            edges, "zone:ashlen-wood",
            "zone:ashlen-wood:location:old-clearing", "zone:ashlen-wood:location:wood-road",
            MapDirection.North, MapDirection.West);

        // Additive, not a replacement: the authored walk through the hidden node survives, so the bypass is an
        // extra route for the player who has not found it rather than a route moved off it.
        edges.Should().Contain(e => e.FromNodeId == "zone:ashlen-wood:location:strangled-oak"
                                    && e.ToNodeId == "zone:ashlen-wood:location:wood-road");
        edges.Should().Contain(e => e.FromNodeId == "zone:grevenmire:location:bone-hollow"
                                    && e.ToNodeId == "zone:grevenmire:location:mire-crossing");
    }

    [Fact]
    public async Task SeedAsync_LinksEveryRegionZoneNodeToItsZoneMap()
    {
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var maps = await db.Maps
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .AsNoTracking()
            .ToListAsync();

        var zoneMapIds = maps
            .Where(m => m.MapType == MapType.Zone)
            .Select(m => m.Id)
            .ToHashSet(StringComparer.Ordinal);

        var regionZoneNodes = maps
            .Where(m => m.MapType == MapType.Region)
            .SelectMany(m => m.Nodes)
            .Where(n => n.NodeType == MapNodeType.Zone && n.RefId is { Length: > 0 })
            .Where(n => zoneMapIds.Contains($"zone:{n.RefId}"))
            .ToList();

        regionZoneNodes.Should().NotBeEmpty();
        regionZoneNodes.Should().OnlyContain(
            n => n.TargetMapId == $"zone:{n.RefId}",
            "a zone node with no target map is a dead end: travel only loads another map when the node names one, so the player would stand on the region map forever");

        // The route a player actually walks, and the one harvesting depends on: the region entry only reaches
        // Crestfall, and Crestfall reaches the wood — where the forage nodes are.
        var varenmark = maps.Single(m => m.Id == "region:varenmark");
        varenmark.Nodes
            .Single(n => n.Id == "region:varenmark:zone:ashlen-wood")
            .TargetMapId.Should().Be("zone:ashlen-wood");

        varenmark.Edges.Should().Contain(
            e => (e.FromNodeId == "region:varenmark:zone:crestfall" && e.ToNodeId == "region:varenmark:zone:ashlen-wood")
                 || (e.FromNodeId == "region:varenmark:zone:ashlen-wood" && e.ToNodeId == "region:varenmark:zone:crestfall"),
            "the wood has to be adjacent to something the region entry reaches");
    }

    [Fact]
    public async Task SeedAsync_CreatesRegionAndZoneMapsWithExits()
    {
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var varenmark = await db.Maps
            .Include(m => m.Nodes)
            .AsNoTracking()
            .SingleAsync(m => m.Id == "region:varenmark");

        varenmark.MapType.Should().Be(MapType.Region);
        varenmark.EntryNodeId.Should().Be("region:varenmark:entry");
        varenmark.Nodes.Should().Contain(n => n.NodeType == MapNodeType.Zone && n.RefId == "crestfall");
        varenmark.Nodes.Should().Contain(n =>
            n.NodeType == MapNodeType.RegionExit &&
            n.TargetMapId == "region:greymoor" &&
            n.TargetNodeId == "region:greymoor:entry");

        var crestfall = await db.Maps
            .Include(m => m.Nodes)
            .AsNoTracking()
            .SingleAsync(m => m.Id == "zone:crestfall");

        crestfall.MapType.Should().Be(MapType.Zone);
        crestfall.Nodes.Should().Contain(n =>
            n.NodeType == MapNodeType.Location && n.RefId == "crestfall-square");
        crestfall.Nodes.Should().Contain(n =>
            n.NodeType == MapNodeType.ZoneExit &&
            n.TargetMapId == "zone:the-droveway" &&
            n.TargetNodeId == "zone:the-droveway:entry");
        crestfall.Nodes.Should().Contain(n =>
            n.NodeType == MapNodeType.RegionExit &&
            n.TargetMapId == "region:varenmark" &&
            n.TargetNodeId == "region:varenmark:entry");
    }

    /// <summary>
    /// Asserts that one bypass walk exists in both directions and is an ordinary visible walk.
    /// </summary>
    /// <param name="edges">Every persisted edge.</param>
    /// <param name="mapId">Map that owns the bypass.</param>
    /// <param name="first">Node on one side of the hidden span.</param>
    /// <param name="second">Node on the other side of the hidden span.</param>
    /// <param name="firstToSecond">Expected direction of <paramref name="first"/> → <paramref name="second"/>.</param>
    /// <param name="secondToFirst">Expected direction of <paramref name="second"/> → <paramref name="first"/>.</param>
    private static void AssertWalkBypass(
        IEnumerable<RealmEngine.Data.Entities.MapEdge> edges,
        string mapId,
        string first,
        string second,
        MapDirection firstToSecond,
        MapDirection secondToFirst)
    {
        edges.Single(e => e.MapId == mapId && e.FromNodeId == first && e.ToNodeId == second)
            .Should().BeEquivalentTo(new
            {
                MapId = mapId,
                Direction = firstToSecond,
                EdgeType = MapEdgeType.Walk,
                IsTraversable = true,
                IsHidden = false,
            });

        edges.Single(e => e.MapId == mapId && e.FromNodeId == second && e.ToNodeId == first)
            .Should().BeEquivalentTo(new
            {
                MapId = mapId,
                Direction = secondToFirst,
                EdgeType = MapEdgeType.Walk,
                IsTraversable = true,
                IsHidden = false,
            });
    }

    /// <summary>
    /// Walks one zone the way a character does: from Arrival, along traversable and visible edges, never
    /// entering a hidden node and never stepping onto a node that changes maps.
    /// </summary>
    /// <remarks>
    /// <c>TravelToNodeHubCommand</c> only loads another map when the selected node carries a
    /// <c>TargetMapId</c>, so an exit, portal or zone node is a fresh walk on the destination map rather
    /// than a corridor inside this one; expanding it here would report a location as reachable when the
    /// only path to it leaves the zone.
    /// </remarks>
    /// <param name="map">Persisted zone map with its nodes and edges.</param>
    /// <returns>Every visible node id Arrival can walk to, including Arrival itself.</returns>
    private static HashSet<string> WalkVisibleGraphFromArrival(RealmEngine.Data.Entities.Map map)
    {
        var nodes = map.Nodes.ToDictionary(n => n.Id, StringComparer.Ordinal);
        var outgoing = map.Edges
            .Where(e => e.IsTraversable && !e.IsHidden)
            .ToLookup(e => e.FromNodeId, StringComparer.Ordinal);

        var reachable = new HashSet<string>(StringComparer.Ordinal);
        if (map.EntryNodeId is not { Length: > 0 } entryId || !nodes.TryGetValue(entryId, out var entry))
            return reachable;

        reachable.Add(entry.Id);
        var queue = new Queue<RealmEngine.Data.Entities.MapNode>();
        queue.Enqueue(entry);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!string.IsNullOrWhiteSpace(current.TargetMapId))
                continue;

            foreach (var edge in outgoing[current.Id])
            {
                if (!nodes.TryGetValue(edge.ToNodeId, out var next)) continue;
                if (next.IsHidden) continue;
                if (reachable.Add(next.Id)) queue.Enqueue(next);
            }
        }

        return reachable;
    }

    private static MapGraph ToGraph(RealmEngine.Data.Entities.Map map) =>
        new(
            map.Id,
            map.MapType,
            map.OwnerId,
            map.Name,
            map.Description,
            map.EntryNodeId,
            map.Nodes
                .OrderBy(n => n.SortOrder)
                .ThenBy(n => n.Id, StringComparer.OrdinalIgnoreCase)
                .Select(n => new MapNodeEntry(
                    n.Id,
                    n.MapId,
                    n.NodeType,
                    n.RefId,
                    n.DisplayName,
                    n.Description,
                    n.IconKey,
                    n.LayoutX,
                    n.LayoutY,
                    n.IsEntryPoint,
                    n.IsExitPoint,
                    n.TargetMapId,
                    n.TargetNodeId,
                    n.IsHidden,
                    n.UnlockType,
                    n.UnlockKey,
                    n.DiscoverThreshold,
                    n.SortOrder))
                .ToList(),
            map.Edges
                .Select(e => new MapEdgeEntry(
                    e.Id,
                    e.MapId,
                    e.FromNodeId,
                    e.ToNodeId,
                    e.Direction,
                    e.EdgeType,
                    e.Label,
                    e.IsTraversable,
                    e.IsHidden))
                .ToList());
}
