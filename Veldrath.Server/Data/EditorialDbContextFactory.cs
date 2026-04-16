using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Veldrath.Server.Data;

/// <summary>
/// Provides design-time construction of <see cref="EditorialDbContext"/> for EF tooling
/// (migrations, scaffolding) without needing a running host or configuration secrets.
/// Targets the local Docker Postgres instance — run <c>docker compose up postgres -d</c> first.
/// </summary>
public class EditorialDbContextFactory : IDesignTimeDbContextFactory<EditorialDbContext>
{
    /// <inheritdoc/>
    public EditorialDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EditorialDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=veldrath;Username=veldrath;Password=veldrath_dev")
            .Options;

        return new EditorialDbContext(options);
    }
}
