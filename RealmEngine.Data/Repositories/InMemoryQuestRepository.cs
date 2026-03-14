using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryQuestRepository : IQuestRepository
{
    /// <inheritdoc />
    public Task<List<Quest>> GetAllAsync() => Task.FromResult(new List<Quest>());

    /// <inheritdoc />
    public Task<Quest?> GetBySlugAsync(string slug) => Task.FromResult<Quest?>(null);

    /// <inheritdoc />
    public Task<List<Quest>> GetByTypeKeyAsync(string typeKey) => Task.FromResult(new List<Quest>());
}
