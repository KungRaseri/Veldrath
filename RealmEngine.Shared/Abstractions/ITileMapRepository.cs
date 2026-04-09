using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Shared.Abstractions;

/// <summary>Repository interface for loading tilemap definitions by zone or region identifier.</summary>
public interface ITileMapRepository
{
    /// <summary>
    /// Returns the <see cref="TiledMap"/> for <paramref name="zoneId"/>,
    /// or <see langword="null"/> if no map is registered for that zone.
    /// </summary>
    Task<TiledMap?> GetByZoneIdAsync(string zoneId);

    /// <summary>
    /// Returns the <see cref="TiledMap"/> for <paramref name="regionId"/>,
    /// or <see langword="null"/> if no map is registered for that region.
    /// </summary>
    Task<TiledMap?> GetByRegionIdAsync(string regionId);
}
