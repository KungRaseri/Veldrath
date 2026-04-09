using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Tiled;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// No-op in-memory stub of <see cref="ITileMapRepository"/>.
/// Always returns <see langword="null"/>; used in the InMemory DI path where asset files are not present.
/// </summary>
public class InMemoryTileMapRepository : ITileMapRepository
{
    /// <inheritdoc />
    public Task<TiledMap?> GetByZoneIdAsync(string zoneId) =>
        Task.FromResult((TiledMap?)null);

    /// <inheritdoc />
    public Task<TiledMap?> GetByRegionIdAsync(string regionId) =>
        Task.FromResult((TiledMap?)null);
}
