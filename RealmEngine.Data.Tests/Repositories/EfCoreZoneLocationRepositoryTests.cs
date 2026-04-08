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

    private static ZoneLocation MakeLoc(string slug, string typeKey = "dungeon", bool active = true) =>
        new() { Slug = slug, ZoneId = "fenwick-crossing", TypeKey = typeKey, IsActive = active, DisplayName = slug };

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
    public async Task GetByTypeKeyAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            MakeLoc("crystal-cave", "dungeon"),
            MakeLoc("market-town", "location"),
            MakeLoc("spider-lair", "dungeon"));
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetByTypeKeyAsync("dungeon");

        result.Should().HaveCount(2).And.OnlyContain(l => l.TypeKey == "dungeon");
    }

    [Fact]
    public async Task GetByZoneIdAsync_FiltersOnZoneId()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.AddRange(
            new ZoneLocation { Slug = "fenwick-market", ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Fenwick Market" },
            new ZoneLocation { Slug = "fenwick-cave",   ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Fenwick Cave" },
            new ZoneLocation { Slug = "other-place",    ZoneId = "greenveil-paths", IsActive = true, DisplayName = "Other Place" });
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
            new ZoneLocation { Slug = "active-loc",   ZoneId = "fenwick-crossing", IsActive = true,  DisplayName = "Active" },
            new ZoneLocation { Slug = "inactive-loc", ZoneId = "fenwick-crossing", IsActive = false, DisplayName = "Inactive" });
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
            new ZoneLocation { Slug = "visible-loc", ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Visible" },
            new ZoneLocation { Slug = "hidden-loc",  ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Hidden",
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
            new ZoneLocation { Slug = "visible-loc",      ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Visible" },
            new ZoneLocation { Slug = "hidden-unlocked",  ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Secret Lair",
                Traits = new ZoneLocationTraits { IsHidden = true } },
            new ZoneLocation { Slug = "hidden-locked",    ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Still Hidden",
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
            new ZoneLocation { Slug = "visible-loc", ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Visible" },
            new ZoneLocation { Slug = "hidden-loc",  ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Secret Passage",
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

    // ── ActorPool ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetAllAsync_MapsActorPool_WhenPopulated()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.Add(new ZoneLocation
        {
            Slug = "darkwood-forest", ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Darkwood",
            ActorPool = [new ActorPoolEntry { ArchetypeSlug = "wolf", Weight = 3 }, new ActorPoolEntry { ArchetypeSlug = "bandit", Weight = 1 }]
        });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllAsync();

        var entry = result.Should().ContainSingle().Subject;
        entry.ActorPool.Should().HaveCount(2);
        entry.ActorPool!.Should().Contain(a => a.ArchetypeSlug == "wolf" && a.Weight == 3);
        entry.ActorPool!.Should().Contain(a => a.ArchetypeSlug == "bandit" && a.Weight == 1);
    }

    [Fact]
    public async Task GetAllAsync_MapsEmptyActorPool_WhenNoEntries()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.Add(MakeLoc("empty-loc"));
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().ContainSingle().Subject.ActorPool.Should().BeNullOrEmpty();
    }

    [Fact]
    public async Task GetBySlugAsync_MapsActorPool()
    {
        await using var db = CreateDbContext();
        db.ZoneLocations.Add(new ZoneLocation
        {
            Slug = "iron-keep", ZoneId = "fenwick-crossing", IsActive = true, DisplayName = "Iron Keep",
            ActorPool = [new ActorPoolEntry { ArchetypeSlug = "skeleton", Weight = 5 }]
        });
        await db.SaveChangesAsync();
        var repo = new EfCoreZoneLocationRepository(db, NullLogger<EfCoreZoneLocationRepository>.Instance);

        var result = await repo.GetBySlugAsync("iron-keep");

        result!.ActorPool.Should().ContainSingle(a => a.ArchetypeSlug == "skeleton" && a.Weight == 5);
    }
}
