using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for loading tilemap definitions by zone identifier.</summary>
public interface ITileMapRepository
{
    /// <summary>
    /// Returns the <see cref="TileMapDefinition"/> for <paramref name="zoneId"/>,
    /// or <see langword="null"/> if no map is registered for that zone.
    /// </summary>
    Task<TileMapDefinition?> GetByZoneIdAsync(string zoneId);
}
