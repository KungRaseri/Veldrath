using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemorySpellRepository : ISpellRepository
{
    /// <inheritdoc />
    public Task<List<Spell>> GetAllAsync() => Task.FromResult(new List<Spell>());

    /// <inheritdoc />
    public Task<Spell?> GetBySlugAsync(string slug) => Task.FromResult<Spell?>(null);

    /// <inheritdoc />
    public Task<List<Spell>> GetBySchoolAsync(string school) => Task.FromResult(new List<Spell>());
}
