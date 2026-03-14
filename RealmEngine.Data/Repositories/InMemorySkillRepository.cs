using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemorySkillRepository : ISkillRepository
{
    /// <inheritdoc />
    public Task<List<SkillDefinition>> GetAllAsync() => Task.FromResult(new List<SkillDefinition>());

    /// <inheritdoc />
    public Task<SkillDefinition?> GetBySlugAsync(string slug) => Task.FromResult<SkillDefinition?>(null);

    /// <inheritdoc />
    public Task<List<SkillDefinition>> GetByCategoryAsync(string category) => Task.FromResult(new List<SkillDefinition>());
}
