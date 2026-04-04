using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// No-op in-memory stub of <see cref="ITileMapRepository"/>.
/// Always returns <see langword="null"/>; used in the InMemory DI path where asset files are not present.
/// </summary>
public class InMemoryTileMapRepository : ITileMapRepository
{
    /// <inheritdoc />
    public Task<TileMapDefinition?> GetByZoneIdAsync(string zoneId) =>
        Task.FromResult((TileMapDefinition?)null);
}
