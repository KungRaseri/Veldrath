using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IArmorRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryArmorRepository : IArmorRepository
{
    /// <inheritdoc />
    public Task<List<Item>> GetAllAsync() =>
        Task.FromResult(new List<Item>());

    /// <inheritdoc />
    public Task<Item?> GetBySlugAsync(string slug) =>
        Task.FromResult((Item?)null);
}
