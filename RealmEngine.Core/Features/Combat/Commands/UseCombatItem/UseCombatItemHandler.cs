using RealmEngine.Shared.Models;
using MediatR;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Combat.Commands.UseCombatItem;

/// <summary>
/// Handles the UseCombatItem command.
/// </summary>
public class UseCombatItemHandler : IRequestHandler<UseCombatItemCommand, UseCombatItemResult>
{
    private readonly IMediator _mediator;

    /// <summary>
    /// Initializes a new instance of the <see cref="UseCombatItemHandler"/> class.
    /// </summary>
    /// <param name="mediator">The mediator for publishing events.</param>
    public UseCombatItemHandler(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Handles the use combat item command and applies item effects.
    /// </summary>
    /// <param name="request">The use combat item command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the item use result.</returns>
    public async Task<UseCombatItemResult> Handle(UseCombatItemCommand request, CancellationToken cancellationToken)
    {
        var player = request.Player;
        var item = request.Item;
        var combatLog = request.CombatLog;

        if (item.Type != ItemType.Consumable)
        {
            return new UseCombatItemResult
            {
                Success = false,
                Message = "Item cannot be used in combat"
            };
        }

        // Calculate healing based on rarity
        var healAmount = item.Rarity switch
        {
            ItemRarity.Common => 25,
            ItemRarity.Uncommon => 50,
            ItemRarity.Rare => 75,
            ItemRarity.Epic => 100,
            ItemRarity.Legendary => 150,
            _ => 0
        };

        var healthBefore = player.Health;
        player.Health = Math.Min(player.Health + healAmount, player.MaxHealth);
        var actualHealing = player.Health - healthBefore;

        // Remove item from inventory
        player.Inventory.Remove(item);

        combatLog?.AddEntry($"{player.Name} used {item.Name} and restored {actualHealing} health!");
        
        // Publish item used event
        await _mediator.Publish(new ItemAcquired(player.Name, item.Name), cancellationToken);

        _logger.LogInformation("Player {PlayerName} used {ItemName} in combat (healed: {Healing})",
            player.Name, item.Name, actualHealing);

        return new UseCombatItemResult
        {
            Success = true,
            HealthRestored = actualHealing,
            Message = $"Restored {actualHealing} health!"
        };
    }
}