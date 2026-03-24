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

    /// <summary>Returns all non-hidden active zone locations that belong to the given zone.</summary>
    Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId);

    /// <summary>Returns non-hidden active zone locations plus any hidden ones whose slugs appear in <paramref name="unlockedSlugs"/>.</summary>
    Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId, IEnumerable<string> unlockedSlugs);

    /// <summary>Returns only hidden active zone locations in the given zone (used for discovery checks).</summary>
    Task<List<ZoneLocationEntry>> GetHiddenByZoneIdAsync(string zoneId);

    /// <summary>Returns all traversal edges originating from the given location slug.</summary>
    Task<List<ZoneLocationConnectionEntry>> GetConnectionsFromAsync(string locationSlug);
}
