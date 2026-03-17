using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data;

/// <summary>
/// Design-time factory that lets <c>dotnet ef migrations add</c> create
/// <see cref="GameDbContext"/> migrations without a running host.
/// </summary>
public class GameDbContextFactory : IDesignTimeDbContextFactory<GameDbContext>
{
    /// <summary>
    /// Creates a <see cref="GameDbContext"/> configured for EF design-time tooling.
    /// </summary>
    /// <param name="args">Command-line arguments (unused).</param>
    /// <returns>A configured <see cref="GameDbContext"/> instance.</returns>
    public GameDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseNpgsql("Host=localhost;Port=5433;Database=realmunbound;Username=realmunbound;Password=realmunbound_dev")
            .Options;
        return new GameDbContext(options);
    }
}
