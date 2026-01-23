using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Services;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Core.Features.ItemGeneration.Queries;

/// <summary>
/// Handler for getting available item categories.
/// </summary>
public class GetAvailableItemCategoriesHandler : IRequestHandler<GetAvailableItemCategoriesQuery, GetAvailableItemCategoriesResult>
{
    private readonly GameDataCache _dataCache;
    private readonly ILogger<GetAvailableItemCategoriesHandler> _logger;

    /// <summary>
    /// Known item category paths to check.
    /// </summary>
    private static readonly string[] KnownCategories = new[]
    {
        "weapons/swords",
        "weapons/axes",
        "weapons/maces",
        "weapons/daggers",
        "weapons/staves",
        "weapons/bows",
        "weapons/crossbows",
        "weapons/spears",
        "weapons/fist-weapons",
        "armor/light",
        "armor/medium",
        "armor/heavy",
        "armor/shields",
        "accessories/amulets",
        "accessories/rings",
        "accessories/cloaks",
        "accessories/belts",
        "consumables/potions",
        "consumables/food",
        "consumables/scrolls"
    };

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAvailableItemCategoriesHandler"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="logger">The logger instance.</param>
    public GetAvailableItemCategoriesHandler(
        GameDataCache dataCache,
        ILogger<GetAvailableItemCategoriesHandler> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Handles the query for available item categories.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing available categories.</returns>
    public Task<GetAvailableItemCategoriesResult> Handle(GetAvailableItemCategoriesQuery request, CancellationToken cancellationToken)
    {
        var result = new GetAvailableItemCategoriesResult();

        try
        {
            var categories = new List<ItemCategoryInfo>();

            foreach (var category in KnownCategories)
            {
                // Apply filter if provided
                if (!string.IsNullOrWhiteSpace(request.FilterPattern))
                {
                    var pattern = request.FilterPattern.Replace("*", "");
                    if (!category.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }
                }

                var catalogFile = _dataCache.GetFile($"items/{category}/catalog.json");
                if (catalogFile?.JsonData != null)
                {
                    var categoryInfo = new ItemCategoryInfo
                    {
                        Category = category,
                        DisplayName = GetDisplayName(category),
                        ParentCategory = GetParentCategory(category)
                    };

                    // Check for names file
                    var namesFile = _dataCache.GetFile($"items/{category}/names.json");
                    categoryInfo.HasNamesFile = namesFile?.JsonData != null;

                    // Count items if requested
                    if (request.IncludeItemCounts)
                    {
                        categoryInfo.ItemCount = CountItemsInCatalog(catalogFile.JsonData);
                    }

                    categories.Add(categoryInfo);
                }
            }

            result.Categories = categories;
            result.Success = true;

            _logger.LogInformation(
                "Found {Count} available item categories{Filter}",
                result.TotalCategories,
                string.IsNullOrWhiteSpace(request.FilterPattern) ? "" : $" matching '{request.FilterPattern}'");

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving available item categories");
            result.Success = false;
            result.ErrorMessage = $"Failed to retrieve categories: {ex.Message}";
            return Task.FromResult(result);
        }
    }

    /// <summary>
    /// Gets a display name from a category path.
    /// </summary>
    private string GetDisplayName(string category)
    {
        var parts = category.Split('/');
        var name = parts[^1]; // Last part
        
        // Capitalize and clean up
        return char.ToUpper(name[0]) + name.Substring(1);
    }

    /// <summary>
    /// Gets the parent category from a category path.
    /// </summary>
    private string? GetParentCategory(string category)
    {
        var parts = category.Split('/');
        return parts.Length > 1 ? parts[0] : null;
    }

    /// <summary>
    /// Counts the number of items in a catalog.
    /// </summary>
    private int CountItemsInCatalog(JToken catalog)
    {
        try
        {
            // Try "items" array first
            var itemsArray = catalog["items"];
            if (itemsArray != null && itemsArray.Type == JTokenType.Array)
            {
                return itemsArray.Count();
            }

            // Try "catalog" array
            var catalogArray = catalog["catalog"];
            if (catalogArray != null && catalogArray.Type == JTokenType.Array)
            {
                return catalogArray.Count();
            }

            // Count all array properties
            var arrayProperties = catalog.Children<JProperty>()
                .Where(p => p.Value.Type == JTokenType.Array)
                .ToList();

            return arrayProperties.Sum(p => p.Value.Count());
        }
        catch
        {
            return 0;
        }
    }
}
