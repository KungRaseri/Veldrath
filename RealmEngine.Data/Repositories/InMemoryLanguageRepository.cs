using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>In-memory stub returning empty data. Used in tests or when no database is configured.</summary>
public class InMemoryLanguageRepository : ILanguageRepository
{
    /// <inheritdoc />
    public Task<List<Language>> GetAllAsync() => Task.FromResult(new List<Language>());

    /// <inheritdoc />
    public Task<Language?> GetBySlugAsync(string slug) => Task.FromResult<Language?>(null);

    /// <inheritdoc />
    public Task<List<Language>> GetByTypeKeyAsync(string typeKey) => Task.FromResult(new List<Language>());
}
