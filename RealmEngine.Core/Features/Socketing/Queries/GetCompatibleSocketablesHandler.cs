using MediatR;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Socketing.Queries;

/// <summary>
/// Handler for getting compatible socketable items for a socket type.
/// </summary>
public class GetCompatibleSocketablesHandler : IRequestHandler<GetCompatibleSocketablesQuery, CompatibleSocketablesResult>
{
    private readonly ISaveGameService _saveGameService;
    private readonly ILogger<GetCompatibleSocketablesHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GetCompatibleSocketablesHandler"/> class.
    /// </summary>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="logger">The logger.</param>
    public GetCompatibleSocketablesHandler(ISaveGameService saveGameService, ILogger<GetCompatibleSocketablesHandler> logger)
    {
        _saveGameService = saveGameService;
        _logger = logger;
    }

    /// <summary>
    /// Handles the compatible socketables query.
    /// </summary>
    /// <param name="request">The compatibility query.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of compatible socketable items.</returns>
    public Task<CompatibleSocketablesResult> Handle(GetCompatibleSocketablesQuery request, CancellationToken cancellationToken)
    {
        var result = new CompatibleSocketablesResult
        {
            Success = true,
            SocketType = request.SocketType
        };

        try
        {
            _logger.LogInformation("Querying compatible socketables for socket type {SocketType}", request.SocketType);

            var saveGame = _saveGameService.GetCurrentSave();
            if (saveGame == null)
            {
                result.Items = [];
                result.TotalCount = 0;
                result.SuggestedItems = [];
                return Task.FromResult(result);
            }

            var allSocketables = CollectSocketablesFromCharacter(saveGame.Character);

            var filtered = allSocketables
                .Where(s => s.SocketType == request.SocketType)
                .Where(s => request.Category == null || s.Category == request.Category)
                .ToList();

            if (request.MinimumRarity.HasValue)
            {
                filtered = filtered.Where(s => RarityWeightToItemRarity(s.RarityWeight) >= request.MinimumRarity.Value).ToList();
            }

            var items = filtered
                .OrderByDescending(s => s.RarityWeight)
                .Select(MapToDto)
                .ToList();

            result.Items = items;
            result.TotalCount = items.Count;
            result.SuggestedItems = [];

            _logger.LogDebug("Found {Count} compatible socketables for {SocketType}",
                result.TotalCount, request.SocketType);

            return Task.FromResult(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error querying compatible socketables");
            result.Success = false;
            return Task.FromResult(result);
        }
    }

    private static List<ISocketable> CollectSocketablesFromCharacter(Character character)
    {
        var socketables = new List<ISocketable>();

        foreach (var item in character.Inventory)
            ExtractSocketContents(item, socketables);

        Item?[] equipped =
        [
            character.EquippedMainHand, character.EquippedOffHand, character.EquippedHelmet,
            character.EquippedShoulders, character.EquippedChest, character.EquippedBracers,
            character.EquippedGloves, character.EquippedBelt, character.EquippedLegs,
            character.EquippedBoots, character.EquippedNecklace, character.EquippedRing1, character.EquippedRing2
        ];

        foreach (var item in equipped.OfType<Item>())
            ExtractSocketContents(item, socketables);

        return socketables;
    }

    private static void ExtractSocketContents(Item item, List<ISocketable> result)
    {
        foreach (var socketList in item.Sockets.Values)
            foreach (var socket in socketList)
                if (socket.Content != null)
                    result.Add(socket.Content);
    }

    private static ItemRarity RarityWeightToItemRarity(int rarityWeight) => rarityWeight switch
    {
        >= 80 => ItemRarity.Common,
        >= 50 => ItemRarity.Uncommon,
        >= 25 => ItemRarity.Rare,
        >= 10 => ItemRarity.Epic,
        _ => ItemRarity.Legendary
    };

    private static SocketableItemDto MapToDto(ISocketable socketable)
    {
        var dto = new SocketableItemDto
        {
            Id = socketable.Id,
            Name = socketable.Name,
            SocketType = socketable.SocketType,
            Description = socketable.Description ?? string.Empty,
            Category = GetSocketableCategory(socketable),
            Rarity = RarityWeightToItemRarity(socketable.RarityWeight)
        };

        foreach (var trait in socketable.Traits)
        {
            var value = trait.Value.Type == TraitType.Number
                ? trait.Value.AsDouble()
                : 0.0;
            var isPercentage = value < 1.0 && value > 0;

            dto.Bonuses.Add(new StatBonusDto
            {
                StatName = trait.Key,
                Value = value,
                IsPercentage = isPercentage,
                DisplayText = isPercentage
                    ? $"+{value * 100:F1}% {trait.Key}"
                    : $"+{value} {trait.Key}"
            });
        }

        return dto;
    }

    private static string GetSocketableCategory(ISocketable socketable)
    {
        return socketable switch
        {
            Gem => "Gem",
            Rune => "Rune",
            Crystal => "Crystal",
            Orb => "Orb",
            _ => "Unknown"
        };
    }
}
