using System.Text.Json;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Loads <see cref="TileMapDefinition"/> instances from JSON asset files bundled with the game.
/// Each map lives at <c>{mapsBasePath}/{zoneId}.json</c>.
/// </summary>
public class JsonFileTileMapRepository : ITileMapRepository
{
    private static readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

    private readonly string _mapsBasePath;
    private readonly ILogger<JsonFileTileMapRepository> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="JsonFileTileMapRepository"/>
    /// resolving map files from <paramref name="mapsBasePath"/>.
    /// </summary>
    public JsonFileTileMapRepository(string mapsBasePath, ILogger<JsonFileTileMapRepository> logger)
    {
        _mapsBasePath = mapsBasePath;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<TileMapDefinition?> GetByZoneIdAsync(string zoneId)
    {
        var filePath = Path.Combine(_mapsBasePath, $"{zoneId}.json");
        if (!File.Exists(filePath))
        {
            _logger.LogDebug("No tilemap asset found for zone '{ZoneId}' at {Path}", zoneId, filePath);
            return null;
        }

        await using var stream = File.OpenRead(filePath);
        var definition = await JsonSerializer.DeserializeAsync<TileMapDefinition>(stream, _jsonOptions);
        return definition;
    }
}
