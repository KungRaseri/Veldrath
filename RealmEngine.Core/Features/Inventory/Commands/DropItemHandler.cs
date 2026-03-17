using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Inventory.Commands;

/// <summary>
/// Handles the DropItem command.
/// </summary>
public class DropItemHandler : IRequestHandler<DropItemCommand, DropItemResult>
{
    /// <summary>
    /// Handles the drop item command and removes the item from inventory.
    /// </summary>
    /// <param name="request">The drop item command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the drop result.</returns>
    public Task<DropItemResult> Handle(DropItemCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var item = request.Item;

        // Remove item from inventory
        var removed = player.Inventory.Remove(item);

        if (removed)
        {
            _logger.LogInformation("Player {PlayerName} dropped {ItemName}",
                player.Name, item.Name);

            return Task.FromResult(new DropItemResult
            {
                Success = true,
                Message = $"Dropped {item.Name}"
            });
        }

        return Task.FromResult(new DropItemResult
        {
            Success = false,
            Message = "Item not found in inventory"
        });
    }
}