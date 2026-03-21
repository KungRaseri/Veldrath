using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreActorInstanceRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static ActorInstance MakeActor(string slug, string typeKey = "enemy", bool active = true) =>
        new() { Slug = slug, TypeKey = typeKey, IsActive = active, DisplayName = slug, ArchetypeId = Guid.NewGuid() };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveActorInstances()
    {
        await using var db = CreateDbContext();
        db.ActorInstances.AddRange(
            MakeActor("goblin-01", active: true),
            MakeActor("deleted-boss", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreActorInstanceRepository(db, NullLogger<EfCoreActorInstanceRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1).And.Contain(a => a.Slug == "goblin-01");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsEntry_WhenActive()
    {
        await using var db = CreateDbContext();
        db.ActorInstances.Add(MakeActor("orc-warrior", "enemy"));
        await db.SaveChangesAsync();
        var repo = new EfCoreActorInstanceRepository(db, NullLogger<EfCoreActorInstanceRepository>.Instance);

        var result = await repo.GetBySlugAsync("orc-warrior");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("orc-warrior");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.ActorInstances.Add(MakeActor("cut-npc", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreActorInstanceRepository(db, NullLogger<EfCoreActorInstanceRepository>.Instance);

        (await repo.GetBySlugAsync("cut-npc")).Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeKeyAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.ActorInstances.AddRange(
            MakeActor("goblin-scout", "enemy"),
            MakeActor("town-guard", "npc"),
            MakeActor("goblin-chief", "enemy"));
        await db.SaveChangesAsync();
        var repo = new EfCoreActorInstanceRepository(db, NullLogger<EfCoreActorInstanceRepository>.Instance);

        var result = await repo.GetByTypeKeyAsync("enemy");

        result.Should().HaveCount(2).And.OnlyContain(a => a.TypeKey == "enemy");
    }
}
