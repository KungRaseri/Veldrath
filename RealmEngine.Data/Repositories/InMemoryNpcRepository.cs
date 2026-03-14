using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryNpcRepository : INpcRepository
{
    /// <inheritdoc />
    public Task<List<NPC>> GetAllAsync() => Task.FromResult(new List<NPC>());

    /// <inheritdoc />
    public Task<NPC?> GetBySlugAsync(string slug) => Task.FromResult<NPC?>(null);

    /// <inheritdoc />
    public Task<List<NPC>> GetByCategoryAsync(string category) => Task.FromResult(new List<NPC>());
}
