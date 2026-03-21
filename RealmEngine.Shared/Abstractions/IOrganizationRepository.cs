using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading organization catalog data.</summary>
public interface IOrganizationRepository
{
    /// <summary>Returns all active organizations.</summary>
    Task<List<OrganizationEntry>> GetAllAsync();

    /// <summary>Returns a single organization by slug, or <see langword="null"/> if not found.</summary>
    Task<OrganizationEntry?> GetBySlugAsync(string slug);

    /// <summary>Returns all active organizations with the given org type (e.g. "faction", "guild").</summary>
    Task<List<OrganizationEntry>> GetByTypeAsync(string orgType);
}
