using MediatR;

namespace RealmEngine.Core.Features.Inventory.Queries;

/// <summary>
/// Handles the GetItemDetails query.
/// </summary>
public class GetItemDetailsHandler : IRequestHandler<GetItemDetailsQuery, GetItemDetailsResult>
{
    /// <summary>
    /// Handles the get item details query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The item details result.</returns>
    public Task<GetItemDetailsResult> Handle(GetItemDetailsQuery request, CancellationToken cancellationToken)
    {
        var item = request.Item;

        var result = new GetItemDetailsResult
        {
            Name = item.Name,
            Description = item.Description,
            Type = item.Type,
            Rarity = item.Rarity,
            Price = item.Price,
            Traits = item.Traits,
            UpgradeLevel = item.UpgradeLevel,
            Enchantments = item.Enchantments,
            SetName = item.SetName
        };

        return Task.FromResult(result);
    }
}
