using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory background repository that returns empty data. Used when no database is configured (tests, in-memory mode).
/// </summary>
public class InMemoryBackgroundRepository : IBackgroundRepository
{
    /// <inheritdoc />
    public Task<List<Background>> GetAllBackgroundsAsync() =>
        Task.FromResult<List<Background>>([]);

    /// <inheritdoc />
    public Task<Background?> GetBackgroundByIdAsync(string backgroundId) =>
        Task.FromResult<Background?>(null);

    /// <inheritdoc />
    public Task<List<Background>> GetBackgroundsByAttributeAsync(string attribute) =>
        Task.FromResult<List<Background>>([]);
}
