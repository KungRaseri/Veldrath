using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreLanguageRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Language MakeLanguage(string slug, string typeKey = "imperial", bool active = true) =>
        new()
        {
            Slug        = slug,
            DisplayName = slug,
            TypeKey     = typeKey,
            IsActive    = active,
            Phonology   = new LanguagePhonology(),
            Morphology  = new LanguageMorphology(),
            RegisterSystem = new LanguageRegisters(),
        };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveLanguages()
    {
        await using var db = CreateDbContext();
        db.Languages.AddRange(
            MakeLanguage("calethic",  active: true),
            MakeLanguage("dead-tongue", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreLanguageRepository(db, NullLogger<EfCoreLanguageRepository>.Instance);

        var result = await repo.GetAllAsync();

        result.Should().HaveCount(1).And.Contain(l => l.Slug == "calethic");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsEntry_WhenActive()
    {
        await using var db = CreateDbContext();
        db.Languages.Add(MakeLanguage("elvish", "elven"));
        await db.SaveChangesAsync();
        var repo = new EfCoreLanguageRepository(db, NullLogger<EfCoreLanguageRepository>.Instance);

        var result = await repo.GetBySlugAsync("elvish");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("elvish");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Languages.Add(MakeLanguage("forgotten", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreLanguageRepository(db, NullLogger<EfCoreLanguageRepository>.Instance);

        (await repo.GetBySlugAsync("forgotten")).Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeKeyAsync_FiltersOnTypeKey()
    {
        await using var db = CreateDbContext();
        db.Languages.AddRange(
            MakeLanguage("calethic",   "imperial"),
            MakeLanguage("silvari",    "elven"),
            MakeLanguage("high-caleth", "imperial"));
        await db.SaveChangesAsync();
        var repo = new EfCoreLanguageRepository(db, NullLogger<EfCoreLanguageRepository>.Instance);

        var result = await repo.GetByTypeKeyAsync("imperial");

        result.Should().HaveCount(2).And.OnlyContain(l => l.TypeKey == "imperial");
    }
}
