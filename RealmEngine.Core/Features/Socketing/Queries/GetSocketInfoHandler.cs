using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Handler for getting socket information.
/// </summary>
public class GetSocketInfoHandler : IRequestHandler<GetSocketInfoQuery, SocketInfoResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<GetSocketInfoHandler> _logger;

    public GetSocketInfoHandler(ISaveGameService saveGameService, ILogger<GetSocketInfoHandler> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the get socket info query.
    /// </summary>
    public Task<SocketInfoResult> Handle(GetSocketInfoQuery request, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Querying socket info for equipment {EquipmentId}", request.EquipmentItemId);

            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame?.Character == null)
            {
                return Task.FromResult(new SocketInfoResult
                {
                    Success = false,
                    Message = "No active game session"
                });
            }

            var item = FindEquipmentItem(saveGame.Character, request.EquipmentItemId);
            if (item == null)
            {
                return Task.FromResult(new SocketInfoResult
                {
                    Success = false,
                    Message = $"Item '{request.EquipmentItemId}' not found in equipped slots or inventory"
                });
            }

            var socketsByType = new Dictionary<SocketType, List<SocketDetailInfo>>();
            var linkedGroupMap = new Dictionary<int, List<int>>();
            var flatIndex = 0;

            foreach (var kvp in item.Sockets.OrderBy(k => k.Key))
            {
                var typeList = new List<SocketDetailInfo>();
                foreach (var socket in kvp.Value)
                {
                    typeList.Add(new SocketDetailInfo
                    {
                        Index = flatIndex,
                        Type = socket.Type,
                        IsEmpty = socket.Content == null,
                        IsLocked = socket.IsLocked,
                        LinkGroup = socket.LinkGroup,
                        Content = socket.Content
                    });

                    if (socket.LinkGroup >= 0)
                    {
                        if (!linkedGroupMap.ContainsKey(socket.LinkGroup))
                            linkedGroupMap[socket.LinkGroup] = [];
                        linkedGroupMap[socket.LinkGroup].Add(flatIndex);
                    }

                    flatIndex++;
                }
                socketsByType[kvp.Key] = typeList;
            }

            var totalSockets = flatIndex;
            var filledSockets = item.Sockets.Values.Sum(list => list.Count(s => s.Content != null));

            return Task.FromResult(new SocketInfoResult
            {
                Success = true,
                Message = "Socket information retrieved successfully",
                SocketsByType = socketsByType,
                TotalSockets = totalSockets,
                FilledSockets = filledSockets,
                EmptySockets = totalSockets - filledSockets,
                LinkedGroups = linkedGroupMap.Select(lg => new LinkedSocketGroup
                {
                    LinkGroupId = lg.Key,
                    SocketIndices = lg.Value,
                    LinkSize = lg.Value.Count,
                    BonusMultiplier = 1.0 + (lg.Value.Count * 0.1)
                }).ToList()
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting socket info for {EquipmentId}", request.EquipmentItemId);
            return Task.FromResult(new SocketInfoResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            });
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
}
