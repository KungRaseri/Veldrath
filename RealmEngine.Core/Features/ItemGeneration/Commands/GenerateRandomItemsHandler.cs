using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for generating random items with quantity control.
/// </summary>
public class GenerateRandomItemsHandler : IRequestHandler<GenerateRandomItemsCommand, GenerateRandomItemsResult>
{
    private readonly GameDataCache _dataCache;
    private readonly ItemGenerator _itemGenerator;
    private readonly ILogger<GenerateRandomItemsHandler> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateRandomItemsHandler"/> class.
    /// </summary>
    /// <param name="dataCache">The game data cache.</param>
    /// <param name="itemGenerator">The item generator service.</param>
    /// <param name="logger">The logger instance.</param>
    public GenerateRandomItemsHandler(
        GameDataCache dataCache,
        ItemGenerator itemGenerator,
        ILogger<GenerateRandomItemsHandler> logger)
    {
        _dataCache = dataCache ?? throw new ArgumentNullException(nameof(dataCache));
        _itemGenerator = itemGenerator ?? throw new ArgumentNullException(nameof(itemGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Gets all available item categories by dynamically discovering them from the cache.
    /// Returns only leaf categories that have actual catalog files (not parent-only directories).
    /// </summary>
    private List<string> GetAllCategories()
    {
        var allSubdomains = _dataCache.GetSubdomainsForDomain("items")
            .Where(cat => !string.IsNullOrWhiteSpace(cat))
            .ToList();
        
        var leafCategories = new List<string>();
        
        foreach (var category in allSubdomains)
        {
            leafCategories.AddRange(DiscoverLeafCategories(category));
        }
        
        // Remove duplicates and return
        return leafCategories.Distinct().ToList();
    }
    
    /// <summary>
    /// Discovers leaf categories (ones with actual catalog files) for a given parent category.
    /// Returns the paths where actual catalog.json files exist.
    /// </summary>
    /// <param name="category">The category to check</param>
    /// <returns>List of usable leaf categories</returns>
    private List<string> DiscoverLeafCategories(string category)
    {
        var leafCategories = new List<string>();
        
        // Check if this category has a direct catalog file (use FileExists to avoid cache miss warnings)
        var catalogPath = $"items/{category}/catalog.json";
        if (_dataCache.FileExists(catalogPath))
        {
            // This is a leaf category with a catalog
            leafCategories.Add(category);
            return leafCategories;
        }
        
        // No direct catalog, check for catalogs in subcategories
        var subcategoryCatalogs = _dataCache.GetCatalogsBySubdomain("items", category).ToList();
        if (subcategoryCatalogs.Any())
        {
            // Extract the directory paths from catalog files
            foreach (var catalog in subcategoryCatalogs)
            {
                // catalog.RelativePath is like "items/crystals/life/catalog.json"
                // We want to extract "crystals/life"
                var path = catalog.RelativePath;
                if (path.StartsWith("items/", StringComparison.OrdinalIgnoreCase) &&
                    path.EndsWith("/catalog.json", StringComparison.OrdinalIgnoreCase))
                {
                    // Remove "items/" prefix and "/catalog.json" suffix
                    var categoryPath = path.Substring(6, path.Length - 6 - 13); // "items/".Length=6, "/catalog.json".Length=13
                    if (!string.IsNullOrEmpty(categoryPath) && !leafCategories.Contains(categoryPath))
                    {
                        leafCategories.Add(categoryPath);
                    }
                }
            }
        }
        
        return leafCategories;
    }

    /// <summary>
    /// Handles the generation of random items command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing generated items.</returns>
    public async Task<GenerateRandomItemsResult> Handle(GenerateRandomItemsCommand request, CancellationToken cancellationToken)
    {
        var result = new GenerateRandomItemsResult
        {
            RequestedQuantity = request.Quantity
        };

        try
        {
            // Validate quantity
            if (request.Quantity <= 0)
            {
                result.Success = false;
                result.ErrorMessage = "Quantity must be greater than 0";
                return result;
            }

            if (request.Quantity > 1000)
            {
                result.Success = false;
                result.ErrorMessage = "Quantity must not exceed 1000";
                return result;
            }

            // Determine categories to use
            var categoriesToUse = await DetermineCategoriesAsync(request.Category);
            if (categoriesToUse.Count == 0)
            {
                result.Success = false;
                result.ErrorMessage = $"No valid categories found for '{request.Category}'";
                return result;
            }

            result.CategoriesUsed = categoriesToUse;

            // Generate items
            var items = new List<Item>();
            
            if (request.UseBudgetGeneration)
            {
                // Budget-based generation (materials + enchantments)
                items = await GenerateWithBudgetAsync(categoriesToUse, request.Quantity, request.MinBudget, request.MaxBudget, request.Hydrate);
            }
            else
            {
                // Simple catalog-based generation
                items = await GenerateFromCatalogAsync(categoriesToUse, request.Quantity, request.Hydrate);
            }

            result.Items = items;
            result.Success = items.Count > 0;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Generated {ActualCount}/{RequestedCount} items from {CategoryCount} categories",
                    result.ActualQuantity,
                    result.RequestedQuantity,
                    result.CategoriesUsed.Count);
            }
            else
            {
                result.ErrorMessage = "Failed to generate any items";
                _logger.LogWarning("Item generation produced no results");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating random items");
            result.Success = false;
            result.ErrorMessage = $"Generation failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Determines which categories to use based on the request.
    /// </summary>
    private Task<List<string>> DetermineCategoriesAsync(string? categoryFilter)
    {
        var categories = new List<string>();

        // If no filter or "random", get all available categories
        if (string.IsNullOrWhiteSpace(categoryFilter) || 
            categoryFilter.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            // GetAllCategories now returns only valid leaf categories
            categories = GetAllCategories();
            return Task.FromResult(categories);
        }

        // Check if specific category exists and discover its leaf categories
        var leafCategories = DiscoverLeafCategories(categoryFilter);
        if (leafCategories.Any())
        {
            categories.AddRange(leafCategories);
            return Task.FromResult(categories);
        }

        // Check for wildcard patterns (e.g., "weapons/*")
        if (categoryFilter.Contains('*'))
        {
            var pattern = categoryFilter.Replace("*", "");
            var allCategories = GetAllCategories();
            categories = allCategories
                .Where(c => c.StartsWith(pattern, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return Task.FromResult(categories);
    }

    /// <summary>
    /// Generates items using budget-based generation (materials + enchantments).
    /// </summary>
    private async Task<List<Item>> GenerateWithBudgetAsync(
        List<string> categories,
        int quantity,
        int minBudget,
        int maxBudget,
        bool hydrate)
    {
        var items = new List<Item>();

        for (int i = 0; i < quantity; i++)
        {
            try
            {
                // Pick random category
                var category = categories[_random.Next(categories.Count)];
                
                // Random budget in range
                var budget = _random.Next(minBudget, maxBudget + 1);

                // Generate item with budget
                var request = new BudgetItemRequest
                {
                    ItemCategory = category,
                    EnemyLevel = budget / 5, // Scale level with budget
                    AllowQuality = true
                };

                var item = await _itemGenerator.GenerateItemWithBudgetAsync(request);
                
                if (item != null)
                {
                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate item {Index}/{Total}", i + 1, quantity);
            }
        }

        return items;
    }

    /// <summary>
    /// Generates items using simple catalog-based generation (no materials/enchantments).
    /// </summary>
    private async Task<List<Item>> GenerateFromCatalogAsync(
        List<string> categories,
        int quantity,
        bool hydrate)
    {
        var items = new List<Item>();

        for (int i = 0; i < quantity; i++)
        {
            try
            {
                // Pick random category
                var category = categories[_random.Next(categories.Count)];

                // Generate random item from category
                var categoryItems = await _itemGenerator.GenerateItemsAsync(category, 1, hydrate);
                
                if (categoryItems != null && categoryItems.Count > 0)
                {
                    items.Add(categoryItems[0]);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate item {Index}/{Total}", i + 1, quantity);
            }
        }

        return items;
    }
}
