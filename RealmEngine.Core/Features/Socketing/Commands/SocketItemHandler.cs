using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Commands;

/// <summary>
/// Handler for socketing items into equipment.
/// </summary>
public class SocketItemHandler : IRequestHandler<SocketItemCommand, SocketItemResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly SocketService _socketService;
    private readonly IPublisher _publisher;
    private readonly ILogger<SocketItemHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="SocketItemHandler"/> class.
    /// </summary>
    public SocketItemHandler(
        ISaveGameService saveGameService,
        SocketService socketService,
        IPublisher publisher,
        ILogger<SocketItemHandler> logger)
    {
        _saveGameService = saveGameService ?? throw new ArgumentNullException(nameof(saveGameService));
        _socketService = socketService ?? throw new ArgumentNullException(nameof(socketService));
        _publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        _logger = logger;
    }

    /// <summary>
    /// Handles the socket item command.
    /// </summary>
    public async Task<SocketItemResult> Handle(SocketItemCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (request.SocketableItem == null)
                return new SocketItemResult { Success = false, Message = "Socketable item cannot be null" };

            if (request.SocketIndex < 0)
                return new SocketItemResult { Success = false, Message = $"Invalid socket index: {request.SocketIndex}" };

            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
                return new SocketItemResult { Success = false, Message = "No active game session" };

            var item = FindEquipmentItem(saveGame.Character, request.EquipmentItemId);
            if (item == null)
                return new SocketItemResult { Success = false, Message = $"Equipment item '{request.EquipmentItemId}' not found" };

            var socket = GetSocketByFlatIndex(item.Sockets, request.SocketIndex);
            if (socket == null)
                return new SocketItemResult { Success = false, Message = $"Socket index {request.SocketIndex} is out of range" };

            var operationResult = _socketService.SocketItem(socket, request.SocketableItem);
            if (!operationResult.Success)
                return new SocketItemResult { Success = false, Message = operationResult.Message };

            _saveGameService.SaveGame(saveGame);

            _logger.LogInformation("Socketed {ItemName} into equipment {EquipmentId} at socket {Index}",
                request.SocketableItem.Name, request.EquipmentItemId, request.SocketIndex);

            var result = new SocketItemResult
            {
                Success = true,
                Message = $"Successfully socketed {request.SocketableItem.Name}",
                SocketedItem = request.SocketableItem,
                AppliedTraits = new Dictionary<string, TraitValue>(request.SocketableItem.Traits),
                IsLinked = socket.LinkGroup >= 0
            };

            await _publisher.Publish(new ItemSocketed(
                request.EquipmentItemId,
                request.SocketableItem.Name,
                request.SocketableItem.SocketType,
                request.SocketIndex,
                result.AppliedTraits
            ), cancellationToken);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error socketing item into {EquipmentId}", request.EquipmentItemId);
            return new SocketItemResult { Success = false, Message = $"Error: {ex.Message}" };
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
