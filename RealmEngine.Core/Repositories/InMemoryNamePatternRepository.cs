using RealmEngine.Core.Abstractions;
using RealmEngine.Data.Entities;

namespace RealmEngine.Core.Repositories;

/// <summary>In-memory (test/headless) repository for name-generation pattern sets — always empty.</summary>
public class InMemoryNamePatternRepository : INamePatternRepository
{
    /// <inheritdoc />
    public Task<IEnumerable<NamePatternSet>> GetAllAsync()
        => Task.FromResult<IEnumerable<NamePatternSet>>([]);

    /// <inheritdoc />
    public Task<NamePatternSet?> GetByEntityPathAsync(string entityPath)
        => Task.FromResult<NamePatternSet?>(null);
}
