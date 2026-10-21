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
        maps.SelectMany(m => m.Nodes).Should().HaveCount(130);
        maps.SelectMany(m => m.Edges).Should().HaveCount(196);

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
        (await db.MapNodes.CountAsync()).Should().Be(130);
        (await db.MapEdges.CountAsync()).Should().Be(196);
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
