using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for reading zone location catalog data.</summary>
public interface IZoneLocationRepository
{
    /// <summary>Returns all active zone locations.</summary>
    Task<List<ZoneLocationEntry>> GetAllAsync();

    /// <summary>Returns a single zone location by slug, or <see langword="null"/> if not found.</summary>
    Task<ZoneLocationEntry?> GetBySlugAsync(string slug);

    /// <summary>Returns all active zone locations with the given type key (e.g. "dungeons", "locations", "environments").</summary>
    Task<List<ZoneLocationEntry>> GetByTypeKeyAsync(string typeKey);

    /// <summary>Returns all non-hidden active zone locations that belong to the given zone.</summary>
    Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId);

    /// <summary>Returns non-hidden active zone locations plus any hidden ones whose slugs appear in <paramref name="unlockedSlugs"/>.</summary>
    Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId, IEnumerable<string> unlockedSlugs);

    /// <summary>Returns only hidden active zone locations in the given zone (used for discovery checks).</summary>
    Task<List<ZoneLocationEntry>> GetHiddenByZoneIdAsync(string zoneId);

    /// <summary>Returns all active zone locations in the given zone, including hidden ones. For dev tooling use only.</summary>
    Task<List<ZoneLocationEntry>> GetAllByZoneIdAsync(string zoneId);
}
