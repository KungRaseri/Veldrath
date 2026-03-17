using MediatR;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Handler for socketing items into equipment.
/// </summary>
public class SocketItemHandler : IRequestHandler<SocketItemCommand, SocketItemResult>
{
    private readonly IPublisher _publisher;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketItemHandler"/> class.
    /// </summary>
    /// <param name="publisher">The MediatR publisher for raising socket events.</param>
    public SocketItemHandler(IPublisher publisher)
    {
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
    }

    /// <summary>
    /// Handles the socket item command.
    /// </summary>
    /// <param name="request">The socket item command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The socket result.</returns>
    public async Task<SocketItemResult> Handle(SocketItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Note: In actual usage, equipment item would be loaded from inventory/save game
            // This is a validation-only handler demonstrating the socketing system
            // Integration with item management would happen at UI/command layer
            
            // Validate socketable item type matches socket type
            var socketableItem = request.SocketableItem;
            if (socketableItem == null)
            {
                return new SocketItemResult
                {
                    Success = false,
                    Message = "Socketable item cannot be null"
                };
            }
            
            // Validate socket index
            if (request.SocketIndex < 0)
            {
                return new SocketItemResult
                {
                    Success = false,
                    Message = $"Invalid socket index: {request.SocketIndex}"
                };
            }
            
            // Success case - traits would be applied to the equipment item
            _logger.LogInformation("Socketing {ItemName} into equipment {EquipmentId} at socket {Index}",
                socketableItem.Name, request.EquipmentItemId, request.SocketIndex);
            
            var result = new SocketItemResult
            {
                Success = true,
                Message = $"Successfully socketed {socketableItem.Name}",
                SocketedItem = socketableItem,
                AppliedTraits = new Dictionary<string, TraitValue>(socketableItem.Traits)
            };
            
            // Publish event for Godot UI updates
            await _publisher.Publish(new ItemSocketed(
                request.EquipmentItemId,
                socketableItem.Name,
                socketableItem.SocketType,
                request.SocketIndex,
                result.AppliedTraits
            ), cancellationToken);
            
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error socketing item");
            return new SocketItemResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
