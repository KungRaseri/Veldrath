using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryPowerRepository : IPowerRepository
{
    /// <inheritdoc />
    public Task<List<Power>> GetAllAsync() => Task.FromResult(new List<Power>());

    /// <inheritdoc />
    public Task<Power?> GetBySlugAsync(string slug) => Task.FromResult<Power?>(null);

    /// <inheritdoc />
    public Task<List<Power>> GetByTypeAsync(string powerType) => Task.FromResult(new List<Power>());

    /// <inheritdoc />
    public Task<List<Power>> GetBySchoolAsync(string school) => Task.FromResult(new List<Power>());
}
