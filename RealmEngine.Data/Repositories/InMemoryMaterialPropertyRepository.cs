using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IMaterialPropertyRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryMaterialPropertyRepository : IMaterialPropertyRepository
{
    /// <inheritdoc />
    public Task<List<MaterialPropertyEntry>> GetAllAsync() =>
        Task.FromResult(new List<MaterialPropertyEntry>());

    /// <inheritdoc />
    public Task<MaterialPropertyEntry?> GetBySlugAsync(string slug) =>
        Task.FromResult((MaterialPropertyEntry?)null);

    /// <inheritdoc />
    public Task<List<MaterialPropertyEntry>> GetByFamilyAsync(string family) =>
        Task.FromResult(new List<MaterialPropertyEntry>());
}
