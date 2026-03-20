using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Repositories;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using Xunit;

namespace RealmEngine.Core.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreNamePatternRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NamePatternSet MakeSet(string entityPath) =>
        new() { EntityPath = entityPath };

    [Fact]
    public async Task GetAllAsync_ReturnsAllSets()
    {
        await using var db = CreateDbContext();
        db.NamePatternSets.AddRange(MakeSet("enemies/wolves"), MakeSet("items/weapons"));
        await db.SaveChangesAsync();
        var repo = new EfCoreNamePatternRepository(db, NullLogger<EfCoreNamePatternRepository>.Instance);

        var result = (await repo.GetAllAsync()).ToList();

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByEntityPathAsync_ReturnsMatch()
    {
        await using var db = CreateDbContext();
        db.NamePatternSets.Add(MakeSet("npcs/merchants"));
        await db.SaveChangesAsync();
        var repo = new EfCoreNamePatternRepository(db, NullLogger<EfCoreNamePatternRepository>.Instance);

        var result = await repo.GetByEntityPathAsync("npcs/merchants");

        result.Should().NotBeNull();
        result!.EntityPath.Should().Be("npcs/merchants");
    }

    [Fact]
    public async Task GetByEntityPathAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreNamePatternRepository(db, NullLogger<EfCoreNamePatternRepository>.Instance);

        (await repo.GetByEntityPathAsync("nonexistent")).Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmpty_WhenNoSets()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreNamePatternRepository(db, NullLogger<EfCoreNamePatternRepository>.Instance);

        (await repo.GetAllAsync()).Should().BeEmpty();
    }
}
