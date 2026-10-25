using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Inventory.Queries;

/// <summary>
/// Handles the GetEquippedItems query.
/// </summary>
public class GetEquippedItemsHandler : IRequestHandler<GetEquippedItemsQuery, GetEquippedItemsResult>
{
    /// <summary>
    /// Handles the get equipped items query.
    /// </summary>
    /// <param name="request">The query request.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The equipped items result.</returns>
    public Task<GetEquippedItemsResult> Handle(GetEquippedItemsQuery request, CancellationToken cancellationToken)
    {
        var player = request.Player;

        var equippedItems = new Dictionary<string, Item?>
        {
            { "MainHand", player.EquippedMainHand },
            { "OffHand", player.EquippedOffHand },
            { "Helmet", player.EquippedHelmet },
            { "Shoulders", player.EquippedShoulders },
            { "Chest", player.EquippedChest },
            { "Bracers", player.EquippedBracers },
            { "Gloves", player.EquippedGloves },
            { "Belt", player.EquippedBelt },
            { "Legs", player.EquippedLegs },
            { "Boots", player.EquippedBoots },
            { "Necklace", player.EquippedNecklace },
            { "Ring1", player.EquippedRing1 },
            { "Ring2", player.EquippedRing2 }
        };

        var result = new GetEquippedItemsResult
        {
            EquippedItems = equippedItems
        };

        return Task.FromResult(result);
    }
}
