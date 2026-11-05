using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Tests.Persistence;

/// <summary>
/// Tests for the EF design-time context factories: <c>dotnet ef</c> needs a context built without a running host, so
/// each factory must hand back a Npgsql-configured context and must not try to connect while doing it.
/// </summary>
[Trait("Category", "Persistence")]
public class DbContextFactoryTests
{
    [Fact]
    public void GameDbContextFactory_BuildsANpgsqlContext()
    {
        var factory = new GameDbContextFactory();

        using var context = factory.CreateDbContext([]);

        context.Should().BeOfType<GameDbContext>();
        context.Database.ProviderName.Should().Contain("Npgsql");
    }

    [Fact]
    public void ContentDbContextFactory_BuildsANpgsqlContext()
    {
        var factory = new ContentDbContextFactory();

        using var context = factory.CreateDbContext(["ignored"]);

        context.Should().BeOfType<ContentDbContext>();
        context.Database.ProviderName.Should().Contain("Npgsql");
    }

    [Fact]
    public void ContentDbContext_ExposesEveryContentSet()
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        using var context = new ContentDbContext(options);

        context.ArchetypePowerPools.Should().NotBeNull();
        context.InstancePowerPools.Should().NotBeNull();
        context.RecipeIngredients.Should().NotBeNull();
        context.NamePatterns.Should().NotBeNull();
        context.NameComponents.Should().NotBeNull();
        context.ArchetypePowerPools.EntityType.ClrType.Name.Should().Be("ArchetypePowerPool");
        context.NamePatterns.EntityType.ClrType.Name.Should().Be("NamePattern");
    }
}
