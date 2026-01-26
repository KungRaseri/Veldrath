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

            // Dynamically discover all item categories from the cache
            var itemSubdomains = _dataCache.GetSubdomainsForDomain("items");

            foreach (var category in itemSubdomains.OrderBy(c => c))
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

                // Check if this category has either a direct catalog or subcategory catalogs
                var catalogFile = _dataCache.GetFile($"items/{category}/catalog.json");
                var hasDirectCatalog = catalogFile?.JsonData != null;
                
                // Also check if there are any catalog files in subcategories
                var hasSubcategoryCatalogs = _dataCache.GetCatalogsBySubdomain("items", category).Any();

                if (hasDirectCatalog || hasSubcategoryCatalogs)
                {
                    var categoryInfo = new ItemCategoryInfo
                    {
                        Category = category,
                        DisplayName = GetDisplayName(category),
                        ParentCategory = null // Top-level categories have no parent
                    };

                    // Check for names file
                    var namesFile = _dataCache.GetFile($"items/{category}/names.json");
                    categoryInfo.HasNamesFile = namesFile?.JsonData != null;

                    // Count items if requested (counts all items across all types/subcategories)
                    if (request.IncludeItemCounts)
                    {
                        if (hasDirectCatalog)
                        {
                            categoryInfo.ItemCount = CountItemsInCatalog(catalogFile!.JsonData);
                        }
                        else
                        {
                            // Count items across all subcategory catalogs
                            var subcategoryCatalogs = _dataCache.GetCatalogsBySubdomain("items", category);
                            categoryInfo.ItemCount = subcategoryCatalogs.Sum(c => CountItemsInCatalog(c.JsonData));
                        }
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
