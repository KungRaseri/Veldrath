using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryEnemyRepository : IEnemyRepository
{
    /// <inheritdoc />
    public Task<List<Enemy>> GetAllAsync() => Task.FromResult(new List<Enemy>());

    /// <inheritdoc />
    public Task<Enemy?> GetBySlugAsync(string slug) => Task.FromResult<Enemy?>(null);

    /// <inheritdoc />
    public Task<List<Enemy>> GetByFamilyAsync(string family) => Task.FromResult(new List<Enemy>());
}
