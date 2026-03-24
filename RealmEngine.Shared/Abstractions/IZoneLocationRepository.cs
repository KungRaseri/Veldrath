using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading zone location catalog data.</summary>
public interface IZoneLocationRepository
{
    /// <summary>Returns all active zone locations.</summary>
    Task<List<ZoneLocationEntry>> GetAllAsync();

    /// <summary>Returns a single zone location by slug, or <see langword="null"/> if not found.</summary>
    Task<ZoneLocationEntry?> GetBySlugAsync(string slug);

    /// <summary>Returns all active zone locations with the given location type (e.g. "dungeon", "location", "environment").</summary>
    Task<List<ZoneLocationEntry>> GetByLocationTypeAsync(string locationType);

    /// <summary>Returns all active zone locations that belong to the given zone.</summary>
    Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId);
}
