using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IMaterialRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryMaterialRepository : IMaterialRepository
{
    /// <inheritdoc />
    public Task<List<MaterialEntry>> GetAllAsync() =>
        Task.FromResult(new List<MaterialEntry>());

    /// <inheritdoc />
    public Task<List<MaterialEntry>> GetByFamiliesAsync(IEnumerable<string> families) =>
        Task.FromResult(new List<MaterialEntry>());

    /// <inheritdoc />
    public Task<MaterialEntry?> GetBySlugAsync(string slug) =>
        Task.FromResult((MaterialEntry?)null);
}
