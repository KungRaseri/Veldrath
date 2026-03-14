using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;

namespace RealmEngine.Data.Services;

/// <summary>
/// Provides read access to game configuration values stored in the GameConfigs table.
/// Subclass to switch between database-backed and no-op modes.
/// </summary>
public abstract class GameConfigService
{
    /// <summary>Returns the raw JSON string for the given config key, or <c>null</c> if not found.</summary>
    public abstract string? GetData(string key);
}

/// <summary>
/// No-op config service used in InMemory/test mode — always returns <c>null</c>,
/// causing all callers to fall back to built-in defaults.
/// </summary>
public sealed class NullGameConfigService : GameConfigService
{
    /// <inheritdoc />
    public override string? GetData(string key) => null;
}

/// <summary>
/// PostgreSQL-backed config service. Uses a factory to create a short-lived DbContext
/// so this singleton can safely query the DB without scope lifetime issues.
/// </summary>
public sealed class DbGameConfigService : GameConfigService
{
    private readonly IDbContextFactory<ContentDbContext> _dbFactory;

    /// <summary>Initializes a new instance of <see cref="DbGameConfigService"/>.</summary>
    public DbGameConfigService(IDbContextFactory<ContentDbContext> dbFactory)
        => _dbFactory = dbFactory;

    /// <inheritdoc />
    public override string? GetData(string key)
    {
        using var db = _dbFactory.CreateDbContext();
        return db.GameConfigs.AsNoTracking()
            .Where(c => c.ConfigKey == key)
            .Select(c => c.Data)
            .FirstOrDefault();
    }
}
