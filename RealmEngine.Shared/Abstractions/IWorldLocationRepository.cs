using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading world location catalog data.</summary>
public interface IWorldLocationRepository
{
    /// <summary>Returns all active world locations.</summary>
    Task<List<WorldLocationEntry>> GetAllAsync();

    /// <summary>Returns a single world location by slug, or <see langword="null"/> if not found.</summary>
    Task<WorldLocationEntry?> GetBySlugAsync(string slug);

    /// <summary>Returns all active world locations with the given location type (e.g. "environment", "location", "region").</summary>
    Task<List<WorldLocationEntry>> GetByLocationTypeAsync(string locationType);
}
