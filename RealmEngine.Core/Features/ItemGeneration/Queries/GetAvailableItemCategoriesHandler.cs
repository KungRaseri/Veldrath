using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Core.Features.ItemGeneration.Queries;

/// <summary>
/// Handler for getting available item categories.
/// </summary>
public class GetAvailableItemCategoriesHandler : IRequestHandler<GetAvailableItemCategoriesQuery, GetAvailableItemCategoriesResult>
{
    private readonly GameDataCache _dataCache;
    private readonly CategoryDiscoveryService _categoryDiscovery;
    private readonly ILogger<GetAvailableItemCategoriesHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetAvailableItemCategoriesHandler"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="categoryDiscovery">The category discovery service.</param>
    /// <param name="logger">The logger instance.</param>
    public GetAvailableItemCategoriesHandler(
        GameDataCache dataCache,
        CategoryDiscoveryService categoryDiscovery,
        ILogger<GetAvailableItemCategoriesHandler> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _categoryDiscovery = categoryDiscovery ?? throw new ArgumentNullException(nameof(categoryDiscovery));
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

            // Get all leaf categories from cached discovery service
            var leafCategories = string.IsNullOrWhiteSpace(request.FilterPattern)
                ? _categoryDiscovery.GetLeafCategories("items")
                : _categoryDiscovery.FindCategories("items", request.FilterPattern);

            foreach (var category in leafCategories.OrderBy(c => c))
            {
                var catalogFile = _dataCache.GetFile($"items/{category}/catalog.json");
                
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
                if (request.IncludeItemCounts && catalogFile?.JsonData != null)
                {
                    categoryInfo.ItemCount = CountItemsInCatalog(catalogFile.JsonData);
                }

                // Get category metadata if available
                var catInfo = _categoryDiscovery.GetCategoryInfo("items", category);
                if (catInfo != null)
                {
                    categoryInfo.ItemCount = catInfo.ItemCount;
                }

                categories.Add(categoryInfo);
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
    /// Handles both old structure (items[]) and new structure (weapon_types, armor_types, etc.).
    /// </summary>
    private int CountItemsInCatalog(JToken catalog)
    {
        try
        {
            int totalCount = 0;

            // Try new structure: weapon_types, armor_types, consumable_types, etc.
            // Each type has an items[] array
            var typeCollections = catalog.Children<JProperty>()
                .Where(p => p.Name.EndsWith("_types") && p.Value.Type == JTokenType.Object)
                .ToList();

            if (typeCollections.Any())
            {
                foreach (var typeCollection in typeCollections)
                {
                    var types = typeCollection.Value.Children<JProperty>();
                    foreach (var type in types)
                    {
                        var typeItems = type.Value["items"];
                        if (typeItems != null && typeItems.Type == JTokenType.Array)
                        {
                            totalCount += typeItems.Count();
                        }
                    }
                }
                return totalCount;
            }

            // Try old structure: direct items[] array
            var itemsArray = catalog["items"];
            if (itemsArray != null && itemsArray.Type == JTokenType.Array)
            {
                return itemsArray.Count();
            }

            // Try "catalog" array (legacy)
            var catalogArray = catalog["catalog"];
            if (catalogArray != null && catalogArray.Type == JTokenType.Array)
            {
                return catalogArray.Count();
            }

            return 0;
        }
        catch
        {
            return 0;
        }
    }
}
