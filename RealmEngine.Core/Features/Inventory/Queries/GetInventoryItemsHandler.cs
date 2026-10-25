using MediatR;

namespace RealmEngine.Core.Features.Inventory.Queries;

/// <summary>
/// Handles the GetInventoryItems query.
/// </summary>
public class GetInventoryItemsHandler : IRequestHandler<GetInventoryItemsQuery, GetInventoryItemsResult>
{
    /// <summary>
    /// Handles the get inventory items query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The inventory items result.</returns>
    public Task<GetInventoryItemsResult> Handle(GetInventoryItemsQuery request, CancellationToken cancellationToken)
    {
        var player = request.Player;

        var result = new GetInventoryItemsResult
        {
            Items = player.Inventory.ToList(),
            TotalItems = player.Inventory.Count
        };

        return Task.FromResult(result);
    }
}
