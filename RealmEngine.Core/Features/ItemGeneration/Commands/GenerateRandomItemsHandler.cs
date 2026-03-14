using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Shared.Models;
using Newtonsoft.Json.Linq;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for generating random items with quantity control.
/// </summary>
public class GenerateRandomItemsHandler : IRequestHandler<GenerateRandomItemsCommand, GenerateRandomItemsResult>
{
    private readonly ItemGenerator _itemGenerator;
    private readonly CategoryDiscoveryService _categoryDiscovery;
    private readonly ILogger<GenerateRandomItemsHandler> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateRandomItemsHandler"/> class.
    /// </summary>
    /// <param name="itemGenerator">The item generator service.</param>
    /// <param name="categoryDiscovery">The category discovery service.</param>
    /// <param name="logger">The logger instance.</param>
    public GenerateRandomItemsHandler(
        ItemGenerator itemGenerator,
        CategoryDiscoveryService categoryDiscovery,
        ILogger<GenerateRandomItemsHandler> logger)
    {
        _itemGenerator = itemGenerator ?? throw new ArgumentNullException(nameof(itemGenerator));
        _categoryDiscovery = categoryDiscovery ?? throw new ArgumentNullException(nameof(categoryDiscovery));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Gets all available item categories using the cached category discovery service.
    /// Returns only leaf categories that have actual catalog files.
    /// </summary>
    private List<string> GetAllCategories()
    {
        return _categoryDiscovery.GetLeafCategories("items").ToList();
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
            var categoriesToUse = await DetermineCategoriesAsync(request);
            if (categoriesToUse.Count == 0)
            {
                var filterDesc = request.CategoryPattern ?? request.Category ?? "random";
                result.Success = false;
                result.ErrorMessage = $"No valid categories found for '{filterDesc}'";
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
    /// Supports CategoryPattern for wildcard matching (e.g., "materials/*", "*", "weapons/swords").
    /// Falls back to Category for backward compatibility.
    /// </summary>
    private Task<List<string>> DetermineCategoriesAsync(GenerateRandomItemsCommand request)
    {
        var categories = new List<string>();

        // Priority 1: CategoryPattern (supports wildcards)
        if (!string.IsNullOrWhiteSpace(request.CategoryPattern))
        {
            categories = _categoryDiscovery.FindCategories("items", request.CategoryPattern).ToList();
            
            if (categories.Any())
            {
                _logger.LogDebug("Using CategoryPattern '{Pattern}': found {Count} categories", 
                    request.CategoryPattern, categories.Count);
                return Task.FromResult(categories);
            }
            
            _logger.LogWarning("CategoryPattern '{Pattern}' matched no categories", request.CategoryPattern);
            return Task.FromResult(categories);
        }

        // Priority 2: Category (legacy, exact match or "random")
        var categoryFilter = request.Category;
        
        // If no filter or "random", get all available categories
        if (string.IsNullOrWhiteSpace(categoryFilter) || 
            categoryFilter.Equals("random", StringComparison.OrdinalIgnoreCase))
        {
            categories = GetAllCategories();
            return Task.FromResult(categories);
        }

        // Check if it's a wildcard pattern (support legacy wildcard in Category field)
        if (categoryFilter.Contains('*'))
        {
            categories = _categoryDiscovery.FindCategories("items", categoryFilter).ToList();
            return Task.FromResult(categories);
        }

        // Exact match or prefix - check if category exists as leaf
        if (_categoryDiscovery.IsLeafCategory("items", categoryFilter))
        {
            categories.Add(categoryFilter);
            return Task.FromResult(categories);
        }

        // Try to find all leaf categories under this parent path
        categories = _categoryDiscovery.FindCategories("items", $"{categoryFilter}/*").ToList();
        
        if (!categories.Any())
        {
            _logger.LogWarning("Category '{Category}' not found and has no subcategories", categoryFilter);
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
