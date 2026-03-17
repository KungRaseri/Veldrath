using MediatR;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Handler for removing socketed items from equipment.
/// </summary>
public class RemoveSocketedItemHandler : IRequestHandler<RemoveSocketedItemCommand, RemoveSocketedItemResult>
{
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveSocketedItemHandler"/> class.
    /// </summary>
    /// <param name="publisher">The MediatR publisher for raising socket events.</param>
    public RemoveSocketedItemHandler(IPublisher publisher)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// Handles the remove socketed item command.
    /// </summary>
    /// <param name="request">The remove socketed item command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The removal result.</returns>
    public async Task<RemoveSocketedItemResult> Handle(RemoveSocketedItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Note: In actual usage, equipment item would be loaded from inventory/save game
            // This is a validation-only handler demonstrating the socket removal system
            // Integration with item management and currency system would happen at UI/command layer
            
            // Validate socket index
            if (request.SocketIndex < 0)
            {
                return new RemoveSocketedItemResult
                {
                    Success = false,
                    Message = $"Invalid socket index: {request.SocketIndex}"
                };
            }
            
            // Validate gold cost
            if (request.GoldCost < 0)
            {
                return new RemoveSocketedItemResult
                {
                    Success = false,
                    Message = "Gold cost cannot be negative"
                };
            }
            
            // Success case - socketable item would be returned to inventory
            _logger.LogInformation("Removing socketed item from equipment {EquipmentId} at socket {Index} for {Gold} gold",
                request.EquipmentItemId, request.SocketIndex, request.GoldCost);
            
            var result = new RemoveSocketedItemResult
            {
                Success = true,
                Message = $"Successfully removed socketed item for {request.GoldCost} gold",
                RemovedItem = null, // Would be loaded from socket
                RemovedTraits = new Dictionary<string, TraitValue>(),
                GoldPaid = request.GoldCost
            };
            
            // Publish event for Godot UI updates
            if (result.RemovedItem != null)
            {
                await _publisher.Publish(new ItemUnsocketed(
                    request.EquipmentItemId,
                    result.RemovedItem.Name,
                    result.RemovedItem.SocketType,
                    request.SocketIndex,
                    request.GoldCost
                ), cancellationToken);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing socketed item");
            return new RemoveSocketedItemResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
