using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreTraitDefinitionRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TraitDefinition MakeTrait(string key, string valueType = "bool", string? appliesTo = null) =>
        new() { Key = key, ValueType = valueType, AppliesTo = appliesTo };

    [Fact]
    public async Task GetAllAsync_ReturnsAllTraits()
    {
        await using var db = CreateDbContext();
        db.TraitDefinitions.AddRange(
            MakeTrait("aggressive"),
            MakeTrait("fire-resist"),
            MakeTrait("poisonous"));
        await db.SaveChangesAsync();
        var repo = new EfCoreTraitDefinitionRepository(db, NullLogger<EfCoreTraitDefinitionRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsEntry_WhenExists()
    {
        await using var db = CreateDbContext();
        db.TraitDefinitions.Add(MakeTrait("stunned", "bool", "enemies"));
        await db.SaveChangesAsync();
        var repo = new EfCoreTraitDefinitionRepository(db, NullLogger<EfCoreTraitDefinitionRepository>.Instance);

        var result = await repo.GetByKeyAsync("stunned");

        result.Should().NotBeNull();
        result!.Key.Should().Be("stunned");
    }

    [Fact]
    public async Task GetByKeyAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreTraitDefinitionRepository(db, NullLogger<EfCoreTraitDefinitionRepository>.Instance);

        (await repo.GetByKeyAsync("nonexistent")).Should().BeNull();
    }

    [Fact]
    public async Task GetByAppliesToAsync_ReturnsWildcardAndMatchingTraits()
    {
        await using var db = CreateDbContext();
        db.TraitDefinitions.AddRange(
            MakeTrait("global-trait",    appliesTo: "*"),
            MakeTrait("enemy-specific",  appliesTo: "enemies"),
            MakeTrait("weapon-specific", appliesTo: "weapons"),
            MakeTrait("multi-target",    appliesTo: "enemies,weapons"));
        await db.SaveChangesAsync();
        var repo = new EfCoreTraitDefinitionRepository(db, NullLogger<EfCoreTraitDefinitionRepository>.Instance);

        var result = await repo.GetByAppliesToAsync("enemies");

        // Expects: global-trait (wildcard), enemy-specific (exact), multi-target (contains)
        result.Should().HaveCount(3)
            .And.Contain(t => t.Key == "global-trait")
            .And.Contain(t => t.Key == "enemy-specific")
            .And.Contain(t => t.Key == "multi-target");
    }
}
