using MediatR;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Handler for batch socketing multiple items into equipment.
/// </summary>
public class SocketMultipleItemsHandler : IRequestHandler<SocketMultipleItemsCommand, SocketMultipleItemsResult>
{
    private readonly IMediator _mediator;
    private readonly ILogger<SocketMultipleItemsHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketMultipleItemsHandler"/> class.
    /// </summary>
    /// <param name="mediator">The MediatR mediator for sending individual socket commands.</param>
    /// <param name="logger">The logger.</param>
    public SocketMultipleItemsHandler(IMediator mediator, ILogger<SocketMultipleItemsHandler> logger)
    {
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger;
    }

    /// <summary>
    /// Handles the batch socket command.
    /// </summary>
    /// <param name="request">The batch socket command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The batch socket result.</returns>
    public async Task<SocketMultipleItemsResult> Handle(SocketMultipleItemsCommand request, CancellationToken cancellationToken)
    {
        var result = new SocketMultipleItemsResult
        {
            Success = true,
            Results = new List<SingleSocketResult>()
        };

        try
        {
            _logger.LogInformation("Starting batch socket operation for {ItemId} with {Count} operations",
                request.EquipmentItemId, request.Operations.Count);

            // Process each operation
            foreach (var operation in request.Operations)
            {
                var socketCommand = new SocketItemCommand(
                    request.EquipmentItemId,
                    operation.SocketIndex,
                    operation.SocketableItem);

                var socketResult = await _mediator.Send(socketCommand, cancellationToken);

                var singleResult = new SingleSocketResult
                {
                    SocketIndex = operation.SocketIndex,
                    Success = socketResult.Success,
                    Message = socketResult.Message,
                    SocketableItemName = operation.SocketableItem.Name
                };

                result.Results.Add(singleResult);

                if (socketResult.Success)
                {
                    result.SuccessCount++;
                    
                    // Merge applied traits into total
                    foreach (var trait in socketResult.AppliedTraits)
                    {
                        if (result.TotalAppliedTraits.ContainsKey(trait.Key))
                        {
                            // Sum numeric values
                            var existing = result.TotalAppliedTraits[trait.Key];
                            if (existing.Type == TraitType.Number && trait.Value.Type == TraitType.Number)
                            {
                                result.TotalAppliedTraits[trait.Key] = new TraitValue(
                                    existing.AsDouble() + trait.Value.AsDouble(),
                                    TraitType.Number);
                            }
                        }
                        else
                        {
                            result.TotalAppliedTraits[trait.Key] = trait.Value;
                        }
                    }
                }
                else
                {
                    result.FailureCount++;
                    result.Success = false; // Overall batch fails if any operation fails
                }
            }

            result.Message = result.Success
                ? $"Successfully socketed {result.SuccessCount} items"
                : $"Batch operation completed with {result.SuccessCount} successes and {result.FailureCount} failures";

            _logger.LogInformation("Batch socket operation completed: {Success}/{Total} successful",
                result.SuccessCount, request.Operations.Count);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch socket operation");
            result.Success = false;
            result.Message = $"Batch operation failed: {ex.Message}";
            return result;
        }
    }
}
