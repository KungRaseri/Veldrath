using RealmUnbound.Server.Data;

namespace RealmUnbound.Server.Tests.Data;

public class ApplicationDbContextFactoryTests
{
    [Fact]
    public void CreateDbContext_Should_Return_NonNull_Context()
    {
        var factory = new ApplicationDbContextFactory();

        using var ctx = factory.CreateDbContext([]);

        ctx.Should().NotBeNull();
    }

    [Fact]
    public void CreateDbContext_Should_Use_Sqlite_Provider()
    {
        var factory = new ApplicationDbContextFactory();

        using var ctx = factory.CreateDbContext([]);

        ctx.Database.ProviderName.Should().Be("Microsoft.EntityFrameworkCore.Sqlite");
    }
}
