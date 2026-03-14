using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;
using MediatR;
using Serilog;
using RealmEngine.Core.Generators.Modern;

using RealmEngine.Core.Services;
namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for ExploreLocationCommand.
/// </summary>
public class ExploreLocationCommandHandler : IRequestHandler<ExploreLocationCommand, ExploreLocationResult>
{
    private readonly IMediator _mediator;
    private readonly GameStateService _gameState;
    private readonly ItemGenerator? _itemGenerator;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExploreLocationCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="gameState">The game state service.</param>
    /// <param name="itemGenerator">Optional item generator for location loot.</param>
    public ExploreLocationCommandHandler(IMediator mediator, GameStateService gameState, ItemGenerator? itemGenerator = null)
    {
        _mediator = mediator;
        _gameState = gameState;
        _itemGenerator = itemGenerator;
    }

    /// <summary>
    /// Handles the explore location command.
    /// </summary>
    /// <param name="request">The explore command.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The exploration result.</returns>
    public async Task<ExploreLocationResult> Handle(ExploreLocationCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var player = _gameState.Player;

            // Simulate exploration with brief delay
            await Task.Delay(500);

            // 60% chance of combat encounter, 40% chance of peaceful exploration
            var encounterRoll = Random.Shared.Next(100);

            if (encounterRoll < 60)
            {
                // Combat encounter!
                await Task.Delay(300);
                return new ExploreLocationResult(true, CombatTriggered: true);
            }

            // Peaceful exploration - gain some XP
            var xpGained = Random.Shared.Next(10, 30);
            var oldLevel = player.Level;
            player.GainExperience(xpGained);

            // Check if leveled up
            if (player.Level > oldLevel)
            {
                await _mediator.Publish(new PlayerLeveledUp(player.Name, player.Level), cancellationToken);
            }

            // Find gold
            var goldFound = Random.Shared.Next(5, 25);
            player.Gold += goldFound;
            await _mediator.Publish(new GoldGained(player.Name, goldFound), cancellationToken);

            string? itemFound = null;

            // Random chance to find an item (30% chance)
            if (Random.Shared.Next(100) < 30 && _itemGenerator != null)
            {
                var item = await GenerateLocationItem(player.Level);
                if (item != null)
                {
                    player.Inventory.Add(item);
                    itemFound = item.Name;
                }
            }

            return new ExploreLocationResult(
                Success: true,
                CombatTriggered: false,
                ExperienceGained: xpGained,
                GoldGained: goldFound,
                ItemFound: itemFound
            );
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Error during exploration");
            return new ExploreLocationResult(false, false, ErrorMessage: ex.Message);
        }
    }

    /// <summary>
    /// Generate a location-appropriate item based on player level.
    /// </summary>
    private async Task<Item?> GenerateLocationItem(int playerLevel)
    {
        if (_itemGenerator == null)
            return null;

        // Use location type to determine item category (default to consumables for exploration)
        var category = "consumables";
        
        // Create budget request based on player level
        var request = new RealmEngine.Core.Services.Budget.BudgetItemRequest
        {
            EnemyType = "exploration",
            EnemyLevel = playerLevel,
            IsBoss = false,
            IsElite = false,
            ItemCategory = category,
            AllowQuality = true
        };

        var item = await _itemGenerator.GenerateItemModelWithBudgetAsync(request);
        return item;
    }
}