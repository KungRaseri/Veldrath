using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="ISpeciesRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemorySpeciesRepository : ISpeciesRepository
{
    /// <inheritdoc />
    public Task<List<Species>> GetAllSpeciesAsync() =>
        Task.FromResult(new List<Species>());

    /// <inheritdoc />
    public Task<Species?> GetSpeciesBySlugAsync(string slug) =>
        Task.FromResult((Species?)null);

    /// <inheritdoc />
    public Task<List<Species>> GetSpeciesByTypeAsync(string typeKey) =>
        Task.FromResult(new List<Species>());
}
