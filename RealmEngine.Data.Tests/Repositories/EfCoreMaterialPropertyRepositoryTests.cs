using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreMaterialPropertyRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static MaterialProperty MakeProp(string slug, string family = "metal", bool active = true) =>
        new() { Slug = slug, MaterialFamily = family, IsActive = active, DisplayName = slug, CostScale = 1.0f };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveMaterialProperties()
    {
        await using var db = CreateDbContext();
        db.MaterialProperties.AddRange(
            MakeProp("iron", active: true),
            MakeProp("deprecated-ore", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreMaterialPropertyRepository(db, NullLogger<EfCoreMaterialPropertyRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1).And.Contain(m => m.Slug == "iron");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsEntry_WhenActive()
    {
        await using var db = CreateDbContext();
        db.MaterialProperties.Add(MakeProp("oak", "wood"));
        await db.SaveChangesAsync();
        var repo = new EfCoreMaterialPropertyRepository(db, NullLogger<EfCoreMaterialPropertyRepository>.Instance);

        var result = await repo.GetBySlugAsync("oak");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("oak");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.MaterialProperties.Add(MakeProp("ancient-metal", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreMaterialPropertyRepository(db, NullLogger<EfCoreMaterialPropertyRepository>.Instance);

        (await repo.GetBySlugAsync("ancient-metal")).Should().BeNull();
    }

    [Fact]
    public async Task GetByFamilyAsync_FiltersOnMaterialFamily()
    {
        await using var db = CreateDbContext();
        db.MaterialProperties.AddRange(
            MakeProp("iron", "metal"),
            MakeProp("oak", "wood"),
            MakeProp("steel", "metal"));
        await db.SaveChangesAsync();
        var repo = new EfCoreMaterialPropertyRepository(db, NullLogger<EfCoreMaterialPropertyRepository>.Instance);

        var result = await repo.GetByFamilyAsync("metal");

        result.Should().HaveCount(2).And.OnlyContain(m => m.MaterialFamily == "metal");
    }
}
