using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Handler for removing socketed items from equipment.
/// </summary>
public class RemoveSocketedItemHandler : IRequestHandler<RemoveSocketedItemCommand, RemoveSocketedItemResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly SocketService _socketService;
    private readonly IPublisher _publisher;
    private readonly ILogger<RemoveSocketedItemHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="RemoveSocketedItemHandler"/> class.
    /// </summary>
    public RemoveSocketedItemHandler(
        ISaveGameService saveGameService,
        SocketService socketService,
        IPublisher publisher,
        ILogger<RemoveSocketedItemHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _socketService = socketService ?? throw new ArgumentNullException(nameof(socketService));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger;
    }

    /// <summary>
    /// Handles the remove socketed item command.
    /// </summary>
    public async Task<RemoveSocketedItemResult> Handle(RemoveSocketedItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.SocketIndex < 0)
                return new RemoveSocketedItemResult { Success = false, Message = $"Invalid socket index: {request.SocketIndex}" };

            if (request.GoldCost < 0)
                return new RemoveSocketedItemResult { Success = false, Message = "Gold cost cannot be negative" };

            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
                return new RemoveSocketedItemResult { Success = false, Message = "No active game session" };

            var character = saveGame.Character;

            if (request.GoldCost > 0 && character.Gold < request.GoldCost)
            {
                return new RemoveSocketedItemResult
                {
                    Success = false,
                    Message = $"Insufficient gold: need {request.GoldCost}g, have {character.Gold}g"
                };
            }

            var item = FindEquipmentItem(character, request.EquipmentItemId);
            if (item == null)
                return new RemoveSocketedItemResult { Success = false, Message = $"Equipment item '{request.EquipmentItemId}' not found" };

            var socket = GetSocketByFlatIndex(item.Sockets, request.SocketIndex);
            if (socket == null)
                return new RemoveSocketedItemResult { Success = false, Message = $"Socket index {request.SocketIndex} is out of range" };

            var removalResult = _socketService.RemoveSocketedItem(socket);
            if (!removalResult.Success)
                return new RemoveSocketedItemResult { Success = false, Message = removalResult.Message };

            // Deduct removal cost and persist
            character.Gold -= request.GoldCost;
            _saveGameService.SaveGame(saveGame);

            _logger.LogInformation("Removed socketed item from equipment {EquipmentId} at socket {Index} for {Gold}g",
                request.EquipmentItemId, request.SocketIndex, request.GoldCost);

            var result = new RemoveSocketedItemResult
            {
                Success = true,
                Message = $"Successfully removed {removalResult.RemovedItem!.Name} for {request.GoldCost}g",
                RemovedItem = removalResult.RemovedItem,
                RemovedTraits = new Dictionary<string, TraitValue>(removalResult.RemovedItem.Traits),
                GoldPaid = request.GoldCost
            };

            await _publisher.Publish(new ItemUnsocketed(
                request.EquipmentItemId,
                removalResult.RemovedItem.Name,
                removalResult.RemovedItem.SocketType,
                request.SocketIndex,
                request.GoldCost
            ), cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing socketed item from {EquipmentId}", request.EquipmentItemId);
            return new RemoveSocketedItemResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }

    private static Item? FindEquipmentItem(Character character, string itemId)
    {
        Item?[] equippedSlots =
        [
            character.EquippedMainHand, character.EquippedOffHand,
            character.EquippedHelmet, character.EquippedShoulders,
            character.EquippedChest, character.EquippedBracers,
            character.EquippedGloves, character.EquippedBelt,
            character.EquippedLegs, character.EquippedBoots,
            character.EquippedNecklace, character.EquippedRing1, character.EquippedRing2
        ];

        return equippedSlots.FirstOrDefault(i => i?.Id == itemId)
            ?? character.Inventory.FirstOrDefault(i => i.Id == itemId);
    }

    private static Socket? GetSocketByFlatIndex(Dictionary<SocketType, List<Socket>> sockets, int flatIndex)
    {
        var current = 0;
        foreach (var list in sockets.OrderBy(k => k.Key).Select(k => k.Value))
        {
            foreach (var socket in list)
            {
                if (current == flatIndex) return socket;
                current++;
            }
        }
        return null;
    }
}
