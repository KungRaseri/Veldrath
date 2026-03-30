using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="ITraitDefinitionRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryTraitDefinitionRepository : ITraitDefinitionRepository
{
    /// <inheritdoc />
    public Task<List<TraitDefinitionEntry>> GetAllAsync() =>
        Task.FromResult(new List<TraitDefinitionEntry>());

    /// <inheritdoc />
    public Task<TraitDefinitionEntry?> GetByKeyAsync(string key) =>
        Task.FromResult((TraitDefinitionEntry?)null);

    /// <inheritdoc />
    public Task<List<TraitDefinitionEntry>> GetByAppliesToAsync(string entityType) =>
        Task.FromResult(new List<TraitDefinitionEntry>());
}
