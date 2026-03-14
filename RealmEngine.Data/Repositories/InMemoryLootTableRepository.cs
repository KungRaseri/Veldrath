using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryLootTableRepository : ILootTableRepository
{
    /// <inheritdoc />
    public Task<List<LootTableData>> GetAllAsync() => Task.FromResult(new List<LootTableData>());

    /// <inheritdoc />
    public Task<LootTableData?> GetBySlugAsync(string slug) => Task.FromResult<LootTableData?>(null);

    /// <inheritdoc />
    public Task<List<LootTableData>> GetByContextAsync(string context) => Task.FromResult(new List<LootTableData>());
}
