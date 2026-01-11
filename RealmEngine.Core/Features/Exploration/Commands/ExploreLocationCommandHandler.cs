using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;
using MediatR;
using Serilog;

using RealmEngine.Core.Services;
namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for ExploreLocationCommand.
/// </summary>
public class ExploreLocationCommandHandler : IRequestHandler<ExploreLocationCommand, ExploreLocationResult>
{
    private readonly IMediator _mediator;
    private readonly GameStateService _gameState;
    private readonly IGameUI _console;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExploreLocationCommandHandler"/> class.
    /// </summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="gameState">The game state service.</param>
    /// <param name="console">The game UI.</param>
    public ExploreLocationCommandHandler(IMediator mediator, GameStateService gameState, IGameUI console)
    {
        _mediator = mediator;
        _gameState = gameState;
        _console = console;
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

            _console.ShowInfo($"Exploring {_gameState.CurrentLocation}...");

            // Simulate exploration
            _console.ShowMessage("Exploring...");
            await Task.Delay(500); // Brief pause for immersion

            // 60% chance of combat encounter, 40% chance of peaceful exploration
            var encounterRoll = Random.Shared.Next(100);

            if (encounterRoll < 60)
            {
                // Combat encounter!
                _console.ShowWarning("You encounter an enemy!");
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

            _console.ShowSuccess($"Gained {xpGained} XP!");

            // Find gold
            var goldFound = Random.Shared.Next(5, 25);
            player.Gold += goldFound;
            await _mediator.Publish(new GoldGained(player.Name, goldFound), cancellationToken);

            string? itemFound = null;

            // Random chance to find an item (30% chance)
            if (Random.Shared.Next(100) < 30)
            {
                // Note: Item generation integrated via ExplorationService.GenerateLocationAppropriateItemAsync
                // This legacy exploration path is superseded by location-specific loot generation
                // See GenerateLocationAppropriateItemAsync for modern implementation
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

    private static string GetRarityColor(ItemRarity rarity)
    {
        return rarity switch
        {
            ItemRarity.Common => "[white]",
            ItemRarity.Uncommon => "[green]",
            ItemRarity.Rare => "[blue]",
            ItemRarity.Epic => "[purple]",
            ItemRarity.Legendary => "[orange1]",
            _ => "[grey]"
        };
    }
}