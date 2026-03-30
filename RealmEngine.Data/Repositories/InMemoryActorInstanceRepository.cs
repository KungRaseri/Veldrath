using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IActorInstanceRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryActorInstanceRepository : IActorInstanceRepository
{
    /// <inheritdoc />
    public Task<List<ActorInstanceEntry>> GetAllAsync() =>
        Task.FromResult(new List<ActorInstanceEntry>());

    /// <inheritdoc />
    public Task<ActorInstanceEntry?> GetBySlugAsync(string slug) =>
        Task.FromResult((ActorInstanceEntry?)null);

    /// <inheritdoc />
    public Task<List<ActorInstanceEntry>> GetByTypeKeyAsync(string typeKey) =>
        Task.FromResult(new List<ActorInstanceEntry>());
}
