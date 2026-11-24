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
        maps.SelectMany(m => m.Nodes).Should().HaveCount(148);
        maps.SelectMany(m => m.Edges).Should().HaveCount(332);

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
        (await db.MapNodes.CountAsync()).Should().Be(148);
        (await db.MapEdges.CountAsync()).Should().Be(332);
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
    public async Task SeedAsync_EveryRegionEntryWalksToAZoneMarker()
    {
        // A character only ever stands on a region board at its Arrival node, and travel refuses a node that is not
        // adjacent to the one they are standing on. Saltcliff's and Cinderplain's Arrival each carried one authored
        // edge, to the Greymoor portal, and a node with a TargetMapId loads that map instead of letting the character
        // pass over it — so every zone marker on both boards sat two hops away behind a map change and no zone on
        // either board could be entered on foot. Greymoor and Varenmark have always had the hop; this pins the shape
        // for all four boards rather than trusting that the next one is authored like the last.
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var regions = await db.Maps
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .AsNoTracking()
            .Where(m => m.MapType == MapType.Region)
            .ToListAsync();

        regions.Should().HaveCount(4, "the seeder authors four region boards");

        var withoutAZone = new List<string>();

        foreach (var region in regions)
        {
            var reachable = WalkVisibleGraphFromArrival(region);

            if (!region.Nodes.Any(n => n.NodeType == MapNodeType.Zone && reachable.Contains(n.Id)))
                withoutAZone.Add(region.Id);
        }

        withoutAZone.Should().BeEmpty(
            "Arrival can only travel to a node beside it, so a board whose Arrival reaches no zone marker has no " +
            $"zone a character on it can enter; boards with no marker in reach: {string.Join(", ", withoutAZone)}");
    }

    [Fact]
    public async Task SeedAsync_EveryZoneExitIsWalkableFromArrivalOrWaitsBehindItsNamedDiscovery()
    {
        // The two reachability tests above pin locations and zones; neither pins a door. A ZoneExit is only usable
        // if a character standing at the map's Arrival can walk to it — the door is the edge, and travel only
        // leaves the map when the selected node carries a TargetMapId, so an exit authored on a node no walk
        // reaches is a door nobody can open. map_unreachable cannot see that: it only asks whether some node on
        // another map names the zone as its target, so a door parked off the walkable graph keeps every gate green.
        // Authored doors behind a hidden location are legitimate (Ashlen Wood's northern track is on the Strangled
        // Oak, the Droveway's mirror hop is on the Sunken Fields), so each one has to be named with the discovery
        // that opens it rather than passing because the walk never looked.
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var maps = await db.Maps
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .AsNoTracking()
            .Where(m => m.MapType == MapType.Zone)
            .ToListAsync();

        var doors = maps
            .SelectMany(map => map.Nodes
                .Where(node => node.NodeType == MapNodeType.ZoneExit)
                .Select(node => (Map: map, Door: node)))
            .ToList();

        doors.Should().HaveCount(23, "the seeder authors 23 ZoneExit nodes across the zone maps");

        var unreachable = new List<string>();

        foreach (var (map, door) in doors)
        {
            if (door.IsHidden) continue;
            if (WalkVisibleGraphFromArrival(map).Contains(door.Id)) continue;
            if (ZoneExitsWaitingBehindADiscovery.ContainsKey(door.Id)) continue;

            unreachable.Add($"{map.Id} → {door.Id}");
        }

        unreachable.Should().BeEmpty(
            "an exit Arrival cannot walk to is a door no player can use, and no validator rule reports it: " +
            $"unreachable exits: {string.Join(", ", unreachable)}. Move the exit's origin edge onto a visible " +
            "location, or add the exit to ZoneExitsWaitingBehindADiscovery with the hidden location that opens it");

        // The named gates have to be true: each one sufficient on its own, each one necessary.
        foreach (var (doorId, gateId) in ZoneExitsWaitingBehindADiscovery)
        {
            var (map, _) = doors.Single(entry => entry.Door.Id == doorId);

            var gate = map.Nodes.SingleOrDefault(node => node.Id == gateId);
            gate.Should().NotBeNull($"{doorId} names {gateId} as the discovery that opens it, so it has to exist");
            gate!.NodeType.Should().Be(MapNodeType.Location, "only a hidden location can be discovered and unlocked");
            gate.IsHidden.Should().BeTrue($"a visible node is not a discovery, so {gateId} would not gate anything");
            gate.UnlockType.Should().NotBeNullOrWhiteSpace(
                $"nothing can ever unlock {gateId}, so {doorId} behind it would be content no player reaches");

            WalkVisibleGraphFromArrival(map)
                .Should().NotContain(doorId, $"{doorId} is listed as waiting for {gateId}");

            WalkVisibleGraphFromArrival(map, new HashSet<string>([gateId], StringComparer.Ordinal))
                .Should().Contain(doorId,
                    $"unlocking {gateId} is the authored way through, so it has to put {doorId} in reach");

            var everyOtherHiddenNode = map.Nodes
                .Where(node => node.IsHidden && node.Id != gateId)
                .Select(node => node.Id)
                .ToHashSet(StringComparer.Ordinal);

            WalkVisibleGraphFromArrival(map, everyOtherHiddenNode)
                .Should().NotContain(doorId,
                    $"{gateId} is named as the only discovery in front of {doorId}; unlocking every other hidden " +
                    "node must leave it out of reach, or the name is the wrong one");
        }
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

    [Fact]
    public async Task SeedAsync_EveryLiveZoneCanBeEnteredThroughADoorRatherThanItsRegionNode()
    {
        // The seeder links every region Zone node to its zone map, so a reachability rule that counts any
        // target sees every zone as reachable and cannot tell a zone with a door from one a player can only
        // reach by standing on the region board next to its marker. A zone authored without a door has to be
        // visible here: the Drowning Pits sat live and unenterable while every per-map rule passed. The rule
        // measures doors, not reachability — a marker beside the region entry is still a way in, and the warning
        // says so — so this list is the authored debt of zones with no door, not a claim that nobody reaches them.
        await using var db = CreateDbContext();

        await MapGraphSeeder.SeedAsync(db);

        var maps = await db.Maps
            .Include(m => m.Nodes)
            .Include(m => m.Edges)
            .AsNoTracking()
            .ToListAsync();

        var result = MapGraphValidator.ValidateWorld(
            maps.Select(ToGraph).ToList(),
            liveMapIds: maps
                .Where(m => m.IsActive && m.MapType == MapType.Zone)
                .Select(m => m.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase));

        var reachableOnlyFromARegionNode = result.Issues
            .Where(issue => issue.Code == "map_unreachable")
            .Select(issue => issue.MapId!)
            .OrderBy(mapId => mapId, StringComparer.Ordinal)
            .ToList();

        reachableOnlyFromARegionNode.Should().NotContain("zone:drowning-pits",
            "the descent from Grevenmire is the repair every zone below needs, and this journey proved it works");
        reachableOnlyFromARegionNode.Should().BeSubsetOf(SeededZonesWithNoInboundDoor,
            "a zone that is not on the debt list and has no door on another map was authored without one: add the " +
            "ZoneExit a neighbouring zone map needs rather than adding it here");
    }

    /// <summary>
    /// Zone exits that no walk from Arrival can reach until a hidden location is discovered, with the hidden
    /// location that opens each one.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every other visible <c>ZoneExit</c> has to sit in <c>WalkVisibleGraphFromArrival</c>. A door behind a
    /// hidden location is authored content — the player finds the place and the track past it opens — but it is
    /// only acceptable when it is named here, because the alternative is a door parked on a hidden span by an
    /// authoring slip, which reads identically from the graph and cannot be told apart by any validator rule.
    /// </para>
    /// <para>
    /// <c>SeedAsync_EveryZoneExitIsWalkableFromArrivalOrWaitsBehindItsNamedDiscovery</c> proves each entry from the
    /// seeded data: the gate exists, is a hidden location, carries an unlock type, is sufficient on its own, and is
    /// necessary — unlocking every other hidden node on the map still does not reach the door. Delete a row when
    /// the door moves onto a visible node, or when the hidden node it waits on stops being hidden.
    /// </para>
    /// </remarks>
    private static readonly IReadOnlyDictionary<string, string> ZoneExitsWaitingBehindADiscovery =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // The wood's northern track is authored on the Strangled Oak, which a passive skill check reveals;
            // no visible link reaches it, so the oak is the way through.
            ["zone:ashlen-wood:exit:zone:the-droveway"] = "zone:ashlen-wood:location:strangled-oak",
            // The mirror hop. The Droveway's path back is authored on the Sunken Fields, which shares the oak's
            // unlock type; the Drove Road bypass does not reach it.
            ["zone:the-droveway:exit:zone:ashlen-wood"] = "zone:the-droveway:location:sunken-fields",
        };

    /// <summary>
    /// Live zone maps that no door on another map leads to. A character standing beside one's marker on a region
    /// board can still walk in — the board is a way in from that node — but a door is the authored shape every
    /// live zone is expected to have, and the warning says that rather than claiming the zone is unreachable.
    /// Fixing one is content work: add a <c>ZoneExit</c> on a neighbouring zone map, the shape of
    /// <c>zone:grevenmire:exit:zone:drowning-pits</c>, and delete its row here. The list may only shrink.
    /// </summary>
    private static readonly string[] SeededZonesWithNoInboundDoor =
    [
        // The dev sandbox is entered by the test harness, not on foot, and is invisible to players.
        "zone:playground",
    ];

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
    /// Walks one map the way a character does: from Arrival, along traversable and visible edges, never
    /// entering a hidden node and never stepping onto a node that changes maps.
    /// </summary>
    /// <remarks>
    /// <c>TravelToNodeHubCommand</c> only loads another map when the selected node carries a
    /// <c>TargetMapId</c>, so an exit, portal or zone node is a fresh walk on the destination map rather
    /// than a corridor inside this one; expanding it here would report a location as reachable when the
    /// only path to it leaves the map. The walk answers the same question on a zone map (which locations
    /// Arrival reaches) and on a region board (which zone markers Arrival is adjacent to).
    /// <para>
    /// <paramref name="unlockedHiddenNodeIds"/> is the discovery case: a hidden location the player has
    /// unlocked is walkable like any other, while every hidden node not named here still stops the walk.
    /// Exits stay non-expanded either way, so "reachable once this is discovered" still means reachable on
    /// this map.
    /// </para>
    /// </remarks>
    /// <param name="map">Persisted map with its nodes and edges.</param>
    /// <param name="unlockedHiddenNodeIds">Hidden nodes to treat as passable, or <see langword="null"/> for a
    /// character who has discovered nothing.</param>
    /// <returns>Every visible node id Arrival can walk to, including Arrival itself.</returns>
    private static HashSet<string> WalkVisibleGraphFromArrival(
        RealmEngine.Data.Entities.Map map,
        IReadOnlySet<string>? unlockedHiddenNodeIds = null)
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
                if (next.IsHidden && !(unlockedHiddenNodeIds?.Contains(next.Id) ?? false)) continue;
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
