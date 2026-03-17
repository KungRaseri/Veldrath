using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Generators.Modern;

namespace RealmEngine.Core.Features.ItemGeneration.Commands;

/// <summary>
/// Handler for GenerateItemCommand.
/// Delegates to ItemGenerator to create items.
/// </summary>
public class GenerateItemCommandHandler : IRequestHandler<GenerateItemCommand, GenerateItemResult>
{
    private readonly ItemGenerator _itemGenerator;
    private readonly ILogger<GenerateItemCommandHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateItemCommandHandler"/> class.
    /// </summary>
    /// <param name="itemGenerator">The item generator.</param>
    /// <param name="logger">The logger.</param>
    public GenerateItemCommandHandler(ItemGenerator itemGenerator, ILogger<GenerateItemCommandHandler> logger)
    {
        _itemGenerator = itemGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Handles the generate item command.
    /// </summary>
    /// <param name="request">The generate item command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The generated item result.</returns>
    public async Task<GenerateItemResult> Handle(GenerateItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Category))
            {
                return new GenerateItemResult
                {
                    Success = false,
                    ErrorMessage = "Category cannot be empty"
                };
            }

            // Use budget-based generation if budget request provided
            if (request.BudgetRequest != null)
            {
                var item = await _itemGenerator.GenerateItemModelWithBudgetAsync(request.BudgetRequest);
                
                if (item == null)
                {
                    return new GenerateItemResult
                    {
                        Success = false,
                        ErrorMessage = "Failed to generate item with budget"
                    };
                }

                _logger.LogDebug("Generated budget item: {ItemName} (Budget: {Budget})", 
                    item.Name, request.BudgetRequest.ItemCategory);

                return new GenerateItemResult
                {
                    Success = true,
                    Item = item
                };
            }

            // Standard category-based generation
            var items = await _itemGenerator.GenerateItemsAsync(request.Category, 1, request.Hydrate);
            
            if (items == null || items.Count == 0)
            {
                return new GenerateItemResult
                {
                    Success = false,
                    ErrorMessage = $"No items found in category: {request.Category}"
                };
            }

            var generatedItem = items[0];
            
            _logger.LogDebug("Generated item: {ItemName} from category {Category}", 
                generatedItem.Name, request.Category);

            return new GenerateItemResult
            {
                Success = true,
                Item = generatedItem
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating item from category {Category}", request.Category);
            return new GenerateItemResult
            {
                Success = false,
                ErrorMessage = $"Failed to generate item: {ex.Message}"
            };
        }
    }
}
