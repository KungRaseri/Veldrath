using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Data.Services;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// Repository that loads character backgrounds from backgrounds/catalog.json via GameDataCache.
/// </summary>
public class BackgroundRepository : IBackgroundRepository
{
    private readonly GameDataCache _cache;
    private readonly ILogger<BackgroundRepository> _logger;
    private List<Background>? _cachedBackgrounds;

    /// <summary>
    /// Initializes a new instance of the BackgroundRepository
    /// </summary>
    public BackgroundRepository(GameDataCache cache, ILogger<BackgroundRepository> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<List<Background>> GetAllBackgroundsAsync()
    {
        if (_cachedBackgrounds != null)
            return Task.FromResult(_cachedBackgrounds);

        _cachedBackgrounds = LoadBackgroundsFromCatalog();
        return Task.FromResult(_cachedBackgrounds);
    }

    /// <inheritdoc />
    public Task<Background?> GetBackgroundByIdAsync(string backgroundId)
    {
        var backgrounds = GetAllBackgroundsAsync().GetAwaiter().GetResult();
        var result = backgrounds.FirstOrDefault(b => 
            b.GetBackgroundId().Equals(backgroundId, StringComparison.OrdinalIgnoreCase) ||
            b.Slug.Equals(backgroundId, StringComparison.OrdinalIgnoreCase));
        return Task.FromResult(result);
    }

    /// <inheritdoc />
    public Task<List<Background>> GetBackgroundsByAttributeAsync(string attribute)
    {
        var backgrounds = GetAllBackgroundsAsync().GetAwaiter().GetResult();
        var result = backgrounds
            .Where(b => b.PrimaryAttribute.Equals(attribute, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return Task.FromResult(result);
    }

    private List<Background> LoadBackgroundsFromCatalog()
    {
        var backgrounds = new List<Background>();

        try
        {
            var catalogPath = "backgrounds/catalog.json";
            var cachedFile = _cache.GetFile(catalogPath);
            
            if (cachedFile == null)
            {
                _logger.LogWarning("Background catalog not found at {Path}", catalogPath);
                return backgrounds;
            }

            var catalogData = cachedFile.JsonData;
            var backgroundTypes = catalogData["background_types"] as JObject;
            if (backgroundTypes == null)
            {
                _logger.LogWarning("No background_types found in catalog");
                return backgrounds;
            }

            foreach (var typeProperty in backgroundTypes.Properties())
            {
                var items = typeProperty.Value["items"] as JArray;
                if (items == null) continue;

                foreach (var item in items)
                {
                    var background = item.ToObject<Background>();
                    if (background != null)
                    {
                        backgrounds.Add(background);
                    }
                }
            }

            _logger.LogInformation("Loaded {Count} backgrounds from catalog", backgrounds.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading backgrounds from catalog");
        }

        return backgrounds;
    }
}
