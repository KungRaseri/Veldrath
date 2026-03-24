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

    [Fact]
    public async Task GetByZoneIdAsync_FiltersOnZoneId()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "fenwick-market", ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true, DisplayName = "Fenwick Market" },
            new ZoneLocation { Slug = "fenwick-cave",   ZoneId = "fenwick-crossing", LocationType = "dungeon",  IsActive = true, DisplayName = "Fenwick Cave" },
            new ZoneLocation { Slug = "other-place",    ZoneId = "greenveil-paths",  LocationType = "location", IsActive = true, DisplayName = "Other Place" });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetByZoneIdAsync("fenwick-crossing");

        result.Should().HaveCount(2).And.OnlyContain(l => l.ZoneId == "fenwick-crossing");
    }

    [Fact]
    public async Task GetByZoneIdAsync_ExcludesInactiveLocations()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "active-loc",   ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true,  DisplayName = "Active" },
            new ZoneLocation { Slug = "inactive-loc", ZoneId = "fenwick-crossing", LocationType = "location", IsActive = false, DisplayName = "Inactive" });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetByZoneIdAsync("fenwick-crossing");

        result.Should().ContainSingle(l => l.Slug == "active-loc");
    }

    [Fact]
    public async Task GetByZoneIdAsync_ReturnsEmpty_WhenNoLocationsExist()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetByZoneIdAsync("nonexistent-zone");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByZoneIdAsync_ExcludesHiddenLocations()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "visible-loc", ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true, DisplayName = "Visible" },
            new ZoneLocation { Slug = "hidden-loc",  ZoneId = "fenwick-crossing", LocationType = "dungeon",  IsActive = true, DisplayName = "Hidden",
                Traits = new ZoneLocationTraits { IsHidden = true } });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetByZoneIdAsync("fenwick-crossing");

        result.Should().ContainSingle(l => l.Slug == "visible-loc");
        result.Should().NotContain(l => l.Slug == "hidden-loc");
    }

    [Fact]
    public async Task GetByZoneIdAsync_WithUnlockedSlugs_IncludesUnlockedHiddenLocations()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "visible-loc",      ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true, DisplayName = "Visible" },
            new ZoneLocation { Slug = "hidden-unlocked",  ZoneId = "fenwick-crossing", LocationType = "dungeon",  IsActive = true, DisplayName = "Secret Lair",
                Traits = new ZoneLocationTraits { IsHidden = true } },
            new ZoneLocation { Slug = "hidden-locked",    ZoneId = "fenwick-crossing", LocationType = "dungeon",  IsActive = true, DisplayName = "Still Hidden",
                Traits = new ZoneLocationTraits { IsHidden = true } });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetByZoneIdAsync("fenwick-crossing", ["hidden-unlocked"]);

        result.Should().HaveCount(2);
        result.Should().Contain(l => l.Slug == "visible-loc");
        result.Should().Contain(l => l.Slug == "hidden-unlocked");
        result.Should().NotContain(l => l.Slug == "hidden-locked");
    }

    [Fact]
    public async Task GetHiddenByZoneIdAsync_ReturnsOnlyHiddenLocations()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "visible-loc", ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true, DisplayName = "Visible" },
            new ZoneLocation { Slug = "hidden-loc",  ZoneId = "fenwick-crossing", LocationType = "dungeon",  IsActive = true, DisplayName = "Secret Passage",
                Traits = new ZoneLocationTraits { IsHidden = true, UnlockType = "skill_check_passive", DiscoverThreshold = 5 } });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetHiddenByZoneIdAsync("fenwick-crossing");

        result.Should().ContainSingle(l => l.Slug == "hidden-loc");
        result.Single().IsHidden.Should().BeTrue();
        result.Single().UnlockType.Should().Be("skill_check_passive");
        result.Single().DiscoverThreshold.Should().Be(5);
    }

    [Fact]
    public async Task GetHiddenByZoneIdAsync_ReturnsEmpty_WhenNoHiddenLocationsExist()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.Add(MakeLoc("visible-loc"));
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetHiddenByZoneIdAsync("fenwick-crossing");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetConnectionsFromAsync_ReturnsConnectionsForSlug()
    {
        await using var db = CreateDbContext();
        db.ZoneLocationConnections.AddRange(
            new ZoneLocationConnection { FromLocationSlug = "fenwick-market", ToLocationSlug = "fenwick-inn",    ConnectionType = "path",   IsTraversable = true  },
            new ZoneLocationConnection { FromLocationSlug = "fenwick-market", ToZoneId        = "greenveil-paths", ConnectionType = "portal", IsTraversable = false },
            new ZoneLocationConnection { FromLocationSlug = "other-place",    ToLocationSlug = "somewhere",      ConnectionType = "path",   IsTraversable = true  });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetConnectionsFromAsync("fenwick-market");

        result.Should().HaveCount(2);
        result.Should().OnlyContain(c => c.FromLocationSlug == "fenwick-market");
    }

    [Fact]
    public async Task GetConnectionsFromAsync_ReturnsEmpty_WhenNoConnectionsExist()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetConnectionsFromAsync("nonexistent-location");

        result.Should().BeEmpty();
    }

    // ── GetAllConnectionsForZoneAsync ────────────────────────────────────────

    [Fact]
    public async Task GetAllConnectionsForZoneAsync_Returns_Connections_For_All_Locations_In_Zone()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "fenwick-inn",    ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true, DisplayName = "Inn" },
            new ZoneLocation { Slug = "fenwick-market", ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true, DisplayName = "Market" });
        db.ZoneLocationConnections.AddRange(
            new ZoneLocationConnection { FromLocationSlug = "fenwick-inn",    ToLocationSlug = "fenwick-market", ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "fenwick-market", ToLocationSlug = "fenwick-inn",    ConnectionType = "path", IsTraversable = true });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllConnectionsForZoneAsync("fenwick-crossing");

        result.Should().HaveCount(2);
        result.Should().Contain(c => c.FromLocationSlug == "fenwick-inn");
        result.Should().Contain(c => c.FromLocationSlug == "fenwick-market");
    }

    [Fact]
    public async Task GetAllConnectionsForZoneAsync_Only_Includes_Connections_From_Locations_In_Zone()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "fenwick-inn",  ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true, DisplayName = "Inn" },
            new ZoneLocation { Slug = "other-place",  ZoneId = "greenveil-paths",  LocationType = "location", IsActive = true, DisplayName = "Other" });
        db.ZoneLocationConnections.AddRange(
            new ZoneLocationConnection { FromLocationSlug = "fenwick-inn",  ToLocationSlug = "fenwick-market", ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "other-place",  ToLocationSlug = "somewhere",      ConnectionType = "path", IsTraversable = true });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllConnectionsForZoneAsync("fenwick-crossing");

        result.Should().ContainSingle(c => c.FromLocationSlug == "fenwick-inn");
        result.Should().NotContain(c => c.FromLocationSlug == "other-place");
    }

    [Fact]
    public async Task GetAllConnectionsForZoneAsync_Returns_Empty_When_No_Connections()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.Add(
            new ZoneLocation { Slug = "empty-inn", ZoneId = "empty-zone", LocationType = "location", IsActive = true, DisplayName = "Inn" });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllConnectionsForZoneAsync("empty-zone");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllConnectionsForZoneAsync_Returns_Empty_When_Zone_Has_No_Locations()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllConnectionsForZoneAsync("nonexistent-zone");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllConnectionsForZoneAsync_Excludes_Inactive_Locations()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "active-loc",   ZoneId = "fenwick-crossing", LocationType = "location", IsActive = true,  DisplayName = "Active" },
            new ZoneLocation { Slug = "inactive-loc", ZoneId = "fenwick-crossing", LocationType = "location", IsActive = false, DisplayName = "Inactive" });
        db.ZoneLocationConnections.AddRange(
            new ZoneLocationConnection { FromLocationSlug = "active-loc",   ToLocationSlug = "active-loc", ConnectionType = "path", IsTraversable = true },
            new ZoneLocationConnection { FromLocationSlug = "inactive-loc", ToLocationSlug = "active-loc", ConnectionType = "path", IsTraversable = true });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllConnectionsForZoneAsync("fenwick-crossing");

        result.Should().ContainSingle(c => c.FromLocationSlug == "active-loc");
        result.Should().NotContain(c => c.FromLocationSlug == "inactive-loc");
    }
}
