using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryAbilityRepository : IAbilityRepository
{
    /// <inheritdoc />
    public Task<List<Ability>> GetAllAsync() => Task.FromResult(new List<Ability>());

    /// <inheritdoc />
    public Task<Ability?> GetBySlugAsync(string slug) => Task.FromResult<Ability?>(null);

    /// <inheritdoc />
    public Task<List<Ability>> GetByTypeAsync(string abilityType) => Task.FromResult(new List<Ability>());
}
