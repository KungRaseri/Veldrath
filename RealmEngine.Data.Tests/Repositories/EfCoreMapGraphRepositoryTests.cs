using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreMapGraphRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Map MakeMap(
        string id,
        MapType mapType,
        string ownerId,
        bool active = true) =>
        new()
        {
            Id = id,
            MapType = mapType,
            OwnerId = ownerId,
            Name = $"{ownerId} map",
            Description = "Test map",
            EntryNodeId = $"{ownerId}:entry",
            IsActive = active,
        };

    private static MapNode MakeNode(
        string id,
        string mapId,
        MapNodeType nodeType,
        string displayName,
        int sortOrder = 0) =>
        new()
        {
            Id = id,
            MapId = mapId,
            NodeType = nodeType,
            RefId = id,
            DisplayName = displayName,
            Description = $"{displayName} description",
            IconKey = "Place",
            LayoutX = sortOrder,
            LayoutY = sortOrder,
            IsEntryPoint = nodeType == MapNodeType.EntryPoint,
            IsExitPoint = nodeType is MapNodeType.ZoneExit or MapNodeType.RegionExit,
            SortOrder = sortOrder,
        };

    private static MapEdge MakeEdge(
        string mapId,
        string fromNodeId,
        string toNodeId,
        MapDirection direction) =>
        new()
        {
            MapId = mapId,
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            Direction = direction,
            EdgeType = MapEdgeType.Walk,
            Label = direction.ToString(),
        };

    [Fact]
    public async Task GetMapAsync_ReturnsGraphWithOrderedNodesAndEdges()
    {
        await using var db = CreateDbContext();
        var map = MakeMap("zone:crestfall", MapType.Zone, "crestfall");
        map.Nodes.Add(MakeNode("crestfall:entry", map.Id, MapNodeType.EntryPoint, "Entry", 0));
        map.Nodes.Add(MakeNode("crestfall:square", map.Id, MapNodeType.Location, "Square", 1));
        map.Nodes.Add(MakeNode("crestfall:east", map.Id, MapNodeType.ZoneExit, "East Road", 2));
        map.Edges.Add(MakeEdge(map.Id, "crestfall:square", "crestfall:east", MapDirection.East));
        db.Maps.Add(map);
        await db.SaveChangesAsync();

        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var graph = await repo.GetMapAsync(map.Id);

        graph.Should().NotBeNull();
        graph!.Nodes.Should().HaveCount(3);
        graph.Nodes[0].Id.Should().Be("crestfall:entry");
        graph.Nodes[^1].Id.Should().Be("crestfall:east");
        graph.Edges.Should().ContainSingle(e =>
            e.FromNodeId == "crestfall:square" &&
            e.ToNodeId == "crestfall:east" &&
            e.Direction == MapDirection.East);
    }

    [Fact]
    public async Task GetMapAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Maps.Add(MakeMap("zone:old", MapType.Zone, "old", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var graph = await repo.GetMapAsync("zone:old");

        graph.Should().BeNull();
    }

    [Fact]
    public async Task GetMapForZoneAsync_FindsZoneMap()
    {
        await using var db = CreateDbContext();
        db.Maps.Add(MakeMap("zone:fenwick-crossing", MapType.Zone, "fenwick-crossing"));
        db.Maps.Add(MakeMap("region:thornveil", MapType.Region, "thornveil"));
        await db.SaveChangesAsync();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var graph = await repo.GetMapForZoneAsync("fenwick-crossing");

        graph.Should().NotBeNull();
        graph!.MapType.Should().Be(MapType.Zone);
        graph.OwnerId.Should().Be("fenwick-crossing");
    }

    [Fact]
    public async Task GetMapForRegionAsync_FindsRegionMap()
    {
        await using var db = CreateDbContext();
        db.Maps.Add(MakeMap("region:thornveil", MapType.Region, "thornveil"));
        await db.SaveChangesAsync();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var graph = await repo.GetMapForRegionAsync("thornveil");

        graph.Should().NotBeNull();
        graph!.MapType.Should().Be(MapType.Region);
    }

    [Fact]
    public async Task GetOutgoingEdgesAsync_ReturnsOnlyEdgesFromNodeOnMap()
    {
        await using var db = CreateDbContext();
        db.Maps.Add(MakeMap("zone:one", MapType.Zone, "one"));
        db.Maps.Add(MakeMap("zone:two", MapType.Zone, "two"));
        db.MapNodes.AddRange(
            MakeNode("one:square", "zone:one", MapNodeType.Location, "Square"),
            MakeNode("one:market", "zone:one", MapNodeType.Location, "Market"),
            MakeNode("one:exit", "zone:one", MapNodeType.ZoneExit, "Exit"),
            MakeNode("two:square", "zone:two", MapNodeType.Location, "Other Square"));
        db.MapEdges.AddRange(
            MakeEdge("zone:one", "one:square", "one:market", MapDirection.North),
            MakeEdge("zone:one", "one:square", "one:exit", MapDirection.East),
            MakeEdge("zone:one", "one:market", "one:square", MapDirection.South),
            MakeEdge("zone:two", "two:square", "one:square", MapDirection.West));
        await db.SaveChangesAsync();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var edges = await repo.GetOutgoingEdgesAsync("zone:one", "one:square");

        edges.Should().HaveCount(2);
        edges.Select(e => e.ToNodeId).Should().BeEquivalentTo(["one:market", "one:exit"]);
    }

    [Fact]
    public async Task GetNodeAsync_ReturnsNode()
    {
        await using var db = CreateDbContext();
        db.Maps.Add(MakeMap("zone:one", MapType.Zone, "one"));
        db.MapNodes.Add(MakeNode("one:square", "zone:one", MapNodeType.Location, "Square"));
        await db.SaveChangesAsync();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var node = await repo.GetNodeAsync("one:square");

        node.Should().NotBeNull();
        node!.DisplayName.Should().Be("Square");
        node.NodeType.Should().Be(MapNodeType.Location);
    }

    [Fact]
    public async Task GetNodeAsync_ReturnsNull_WhenUnknown()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var node = await repo.GetNodeAsync("missing");

        node.Should().BeNull();
    }

    [Fact]
    public async Task GetMapsAsync_ReturnsSummariesWithCounts()
    {
        await using var db = CreateDbContext();
        var map = MakeMap("zone:crestfall", MapType.Zone, "crestfall");
        map.Nodes.Add(MakeNode("crestfall:entry", map.Id, MapNodeType.EntryPoint, "Entry"));
        map.Nodes.Add(MakeNode("crestfall:square", map.Id, MapNodeType.Location, "Square"));
        map.Edges.Add(MakeEdge(map.Id, "crestfall:entry", "crestfall:square", MapDirection.North));
        db.Maps.Add(map);
        db.Maps.Add(MakeMap("region:thornveil", MapType.Region, "thornveil", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreMapGraphRepository(db, NullLogger<EfCoreMapGraphRepository>.Instance);

        var summaries = await repo.GetMapsAsync();

        summaries.Should().ContainSingle();
        summaries[0].Id.Should().Be("zone:crestfall");
        summaries[0].NodeCount.Should().Be(2);
        summaries[0].EdgeCount.Should().Be(1);
    }
}
