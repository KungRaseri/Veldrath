using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for generating items by category with quantity control.
/// </summary>
public class GenerateItemsByCategoryHandler : IRequestHandler<GenerateItemsByCategoryCommand, GenerateItemsByCategoryResult>
{
    private readonly ItemGenerator _itemGenerator;
    private readonly ILogger<GenerateItemsByCategoryHandler> _logger;
    private readonly Random _random;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateItemsByCategoryHandler"/> class.
    /// </summary>
    /// <param name="itemGenerator">The item generator service.</param>
    /// <param name="logger">The logger instance.</param>
    public GenerateItemsByCategoryHandler(
        ItemGenerator itemGenerator,
        ILogger<GenerateItemsByCategoryHandler> logger)
    {
        _itemGenerator = itemGenerator ?? throw new ArgumentNullException(nameof(itemGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _random = new Random();
    }

    /// <summary>
    /// Handles the generation of items by category command.
    /// </summary>
    /// <param name="request">The command request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result containing generated items.</returns>
    public async Task<GenerateItemsByCategoryResult> Handle(GenerateItemsByCategoryCommand request, CancellationToken cancellationToken)
    {
        var result = new GenerateItemsByCategoryResult
        {
            Category = request.Category,
            RequestedQuantity = request.Quantity
        };

        try
        {
            // Validate category
            if (string.IsNullOrWhiteSpace(request.Category))
            {
                result.Success = false;
                result.ErrorMessage = "Category is required";
                return result;
            }

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

            var items = new List<Item>();

            if (!string.IsNullOrWhiteSpace(request.ItemName))
            {
                // Generate specific item by name (repeated)
                items = await GenerateSpecificItemAsync(
                    request.Category,
                    request.ItemName,
                    request.Quantity,
                    request.MinBudget,
                    request.MaxBudget,
                    request.UseBudgetGeneration,
                    request.Hydrate);
            }
            else if (request.UseBudgetGeneration)
            {
                // Budget-based random generation
                items = await GenerateWithBudgetAsync(
                    request.Category,
                    request.Quantity,
                    request.MinBudget,
                    request.MaxBudget,
                    request.Hydrate);
            }
            else
            {
                // Simple catalog-based random generation
                items = await _itemGenerator.GenerateItemsAsync(request.Category, request.Quantity, request.Hydrate);
            }

            result.Items = items;
            result.Success = items.Count > 0;

            if (result.Success)
            {
                _logger.LogInformation(
                    "Generated {ActualCount}/{RequestedCount} items from category '{Category}'",
                    result.ActualQuantity,
                    result.RequestedQuantity,
                    result.Category);
            }
            else
            {
                result.ErrorMessage = "Failed to generate any items";
                _logger.LogWarning("Item generation produced no results for category '{Category}'", request.Category);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating items from category '{Category}'", request.Category);
            result.Success = false;
            result.ErrorMessage = $"Generation failed: {ex.Message}";
            return result;
        }
    }

    /// <summary>
    /// Generates a specific item by name, repeated for quantity.
    /// </summary>
    private async Task<List<Item>> GenerateSpecificItemAsync(
        string category,
        string itemName,
        int quantity,
        int minBudget,
        int maxBudget,
        bool useBudgetGeneration,
        bool hydrate)
    {
        var items = new List<Item>();

        for (int i = 0; i < quantity; i++)
        {
            try
            {
                Item? item;

                if (useBudgetGeneration)
                {
                    var budget = _random.Next(minBudget, maxBudget + 1);
                    var request = new BudgetItemRequest
                    {
                        ItemCategory = category,
                        EnemyLevel = budget / 5,
                        AllowQuality = true
                    };
                    item = await _itemGenerator.GenerateItemModelWithBudgetAsync(request);
                }
                else
                {
                    item = await _itemGenerator.GenerateItemByNameAsync(category, itemName, hydrate);
                }

                if (item != null)
                {
                    items.Add(item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to generate item '{ItemName}' {Index}/{Total}", itemName, i + 1, quantity);
            }
        }

        return items;
    }

    /// <summary>
    /// Generates items using budget-based generation.
    /// </summary>
    private async Task<List<Item>> GenerateWithBudgetAsync(
        string category,
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
                var budget = _random.Next(minBudget, maxBudget + 1);
                var request = new BudgetItemRequest
                {
                    ItemCategory = category,
                    EnemyLevel = budget / 5,
                    AllowQuality = true
                };

                var item = await _itemGenerator.GenerateItemModelWithBudgetAsync(request);
                
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
}
