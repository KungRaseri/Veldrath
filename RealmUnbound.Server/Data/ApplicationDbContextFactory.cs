using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace RealmUnbound.Server.Data;

/// <summary>
/// Provides design-time construction of <see cref="ApplicationDbContext"/> for EF tooling
/// (migrations, scaffolding) without needing a running host or configuration secrets.
/// Targets the local Docker Postgres instance — run <c>docker compose up postgres -d</c> first.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=realmunbound;Username=realmunbound;Password=realmunbound_dev")
            .Options;

        return new ApplicationDbContext(options);
    }
}
