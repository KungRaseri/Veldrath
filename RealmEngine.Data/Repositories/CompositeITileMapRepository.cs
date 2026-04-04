using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Composite tilemap repository that first checks JSON asset files, then falls back to
/// procedural dungeon generation for zone identifiers that start with <c>"dungeon-"</c>.
/// Generated dungeon maps are cached in memory so the same zone gets the same layout
/// for the lifetime of the process.
/// </summary>
public class CompositeITileMapRepository : ITileMapRepository
{
    private readonly JsonFileTileMapRepository _json;
    private readonly ILogger<CompositeITileMapRepository> _logger;

    // Cache of procedurally generated maps, keyed by zone ID
    private readonly ConcurrentDictionary<string, TileMapDefinition> _generatedCache =
        new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of <see cref="CompositeITileMapRepository"/>.
    /// </summary>
    /// <param name="mapsBasePath">Directory that contains the <c>{zoneId}.json</c> map files.</param>
    /// <param name="logger">Logger instance.</param>
    public CompositeITileMapRepository(string mapsBasePath, ILogger<CompositeITileMapRepository> logger)
    {
        _json   = new JsonFileTileMapRepository(mapsBasePath, NullLogger<JsonFileTileMapRepository>.Instance);
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TileMapDefinition?> GetByZoneIdAsync(string zoneId)
    {
        // 1. Try JSON asset first
        var fromFile = await _json.GetByZoneIdAsync(zoneId);
        if (fromFile is not null)
            return fromFile;

        // 2. Procedurally generate dungeon maps on demand
        if (zoneId.StartsWith("dungeon-", StringComparison.OrdinalIgnoreCase))
        {
            return _generatedCache.GetOrAdd(zoneId, id =>
            {
                _logger.LogInformation("Generating dungeon map for zone '{ZoneId}'", id);
                return DungeonGenerator.Generate(id);
            });
        }

        return null;
    }
}
