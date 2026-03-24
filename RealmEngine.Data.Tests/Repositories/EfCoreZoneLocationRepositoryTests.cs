using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreZoneLocationRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ZoneLocation MakeLoc(string slug, string locationType = "dungeon", bool active = true) =>
        new() { Slug = slug, ZoneId = "fenwick-crossing", LocationType = locationType, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveLocations()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            MakeLoc("darkwood-forest", active: true),
            MakeLoc("destroyed-village", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1).And.Contain(l => l.Slug == "darkwood-forest");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsEntry_WhenActive()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.Add(MakeLoc("iron-keep", "dungeon"));
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetBySlugAsync("iron-keep");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("iron-keep");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.Add(MakeLoc("old-ruins", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        (await repo.GetBySlugAsync("old-ruins")).Should().BeNull();
    }

    [Fact]
    public async Task GetByLocationTypeAsync_FiltersOnLocationType()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            MakeLoc("crystal-cave", "dungeon"),
            MakeLoc("market-town", "location"),
            MakeLoc("spider-lair", "dungeon"));
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetByLocationTypeAsync("dungeon");

        result.Should().HaveCount(2).And.OnlyContain(l => l.LocationType == "dungeon");
    }
}
