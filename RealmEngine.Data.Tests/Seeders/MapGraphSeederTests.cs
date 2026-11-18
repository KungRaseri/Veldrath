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
        maps.SelectMany(m => m.Nodes).Should().HaveCount(134);
        maps.SelectMany(m => m.Edges).Should().HaveCount(298);

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
        (await db.MapNodes.CountAsync()).Should().Be(134);
        (await db.MapEdges.CountAsync()).Should().Be(298);
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
