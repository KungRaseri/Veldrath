using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory stub implementation of <see cref="IOrganizationRepository"/>.
/// Returns empty results — used in the InMemory (no-database) DI configuration path.
/// </summary>
public class InMemoryOrganizationRepository : IOrganizationRepository
{
    /// <inheritdoc />
    public Task<List<OrganizationEntry>> GetAllAsync() =>
        Task.FromResult(new List<OrganizationEntry>());

    /// <inheritdoc />
    public Task<OrganizationEntry?> GetBySlugAsync(string slug) =>
        Task.FromResult((OrganizationEntry?)null);

    /// <inheritdoc />
    public Task<List<OrganizationEntry>> GetByTypeAsync(string orgType) =>
        Task.FromResult(new List<OrganizationEntry>());
}
