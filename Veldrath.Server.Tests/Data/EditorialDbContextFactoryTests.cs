using Veldrath.Server.Data;

namespace Veldrath.Server.Tests.Data;

/// <summary>
/// Tests for <see cref="EditorialDbContextFactory"/>, the design-time factory EF tooling uses to scaffold
/// migrations without booting the host. It must build a context on the local-development Postgres provider and
/// must not require any configuration or running database to do so.
/// </summary>
public class EditorialDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_ReturnsAUsableContext()
    {
        var factory = new EditorialDbContextFactory();

        using var context = factory.CreateDbContext([]);

        context.Should().NotBeNull();
        context.PatchNotes.Should().NotBeNull();
        context.LoreArticles.Should().NotBeNull();
        context.Announcements.Should().NotBeNull();
    }

    [Fact]
    public void CreateDbContext_TargetsPostgresForMigrations()
    {
        var factory = new EditorialDbContextFactory();

        using var context = factory.CreateDbContext([]);

        context.Database.ProviderName.Should().Contain("Npgsql",
            "editorial migrations are generated against the production provider");
    }

    [Fact]
    public void CreateDbContext_IsRepeatableAndReturnsFreshInstances()
    {
        var factory = new EditorialDbContextFactory();

        using var first = factory.CreateDbContext(["ignored"]);
        using var second = factory.CreateDbContext([]);

        first.Should().NotBeSameAs(second);
    }
}
