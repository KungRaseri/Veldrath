using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data;

/// <summary>
/// Design-time factory for <see cref="ContentDbContext"/> — enables EF tooling (migrations,
/// scaffolding) without a running host. Targets the local Docker Postgres instance.
/// Prerequisite: docker compose up postgres -d
/// </summary>
public class ContentDbContextFactory : IDesignTimeDbContextFactory<ContentDbContext>
{
    public ContentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=realmunbound;Username=realmunbound;Password=realmunbound_dev")
            .Options;
        return new ContentDbContext(options);
    }
}
