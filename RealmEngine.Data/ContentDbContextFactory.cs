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
    /// <summary>
    /// Creates a <see cref="ContentDbContext"/> configured for EF design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (unused).</param>
    /// <returns>A configured <see cref="ContentDbContext"/> instance.</returns>
    public ContentDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<ContentDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=veldrath;Username=veldrath;Password=veldrath_dev")
            .Options;
        return new ContentDbContext(options);
    }
}
