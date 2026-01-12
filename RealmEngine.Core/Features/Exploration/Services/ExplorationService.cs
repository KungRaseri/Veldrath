using RealmEngine.Core.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Death.Queries;
using RealmEngine.Core.Generators.Modern;
using MediatR;

using RealmEngine.Core.Services;
using RealmEngine.Core.Generators;

namespace RealmEngine.Core.Features.Exploration;

/// <summary>
/// Service for handling exploration, travel, and location-based events.
/// </summary>
public class ExplorationService
{
    private readonly IMediator _mediator;
    private readonly GameStateService _gameState;
    private readonly SaveGameService _saveGameService;
    private readonly IGameUI _console;
    private readonly LocationGenerator _locationGenerator;
    private readonly ItemGenerator? _itemGenerator;

    private readonly List<Location> _knownLocations = new();
    private bool _locationsInitialized = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorationService"/> class for mocking.
    /// </summary>
    protected ExplorationService() { _mediator = null!; _gameState = null!; _saveGameService = null!; _console = null!; _locationGenerator = null!; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorationService"/> class.
    /// </summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="gameState">The game state service.</param>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="console">The game UI.</param>
    /// <param name="locationGenerator">The location generator.</param>
    /// <param name="itemGenerator">The item generator (optional).</param>
    public ExplorationService(IMediator mediator, GameStateService gameState, SaveGameService saveGameService, IGameUI console, LocationGenerator locationGenerator, ItemGenerator? itemGenerator = null)
    {
        _mediator = mediator;
        _gameState = gameState;
        _saveGameService = saveGameService;
        _console = console;
        _locationGenerator = locationGenerator;
        _itemGenerator = itemGenerator;
    }
    
    /// <summary>
    /// Initialize known locations by generating them from data.
    /// </summary>
    private async Task InitializeLocationsAsync()
    {
        if (_locationsInitialized)
            return;

        _knownLocations.Clear();
        
        // Generate initial locations (mix of towns, dungeons, wilderness)
        // Hydration enabled: Resolves NPC/Enemy/Loot references to full objects
        var towns = await _locationGenerator.GenerateLocationsAsync("towns", 2, hydrate: true);
        var dungeons = await _locationGenerator.GenerateLocationsAsync("dungeons", 3, hydrate: true);
        var wilderness = await _locationGenerator.GenerateLocationsAsync("wilderness", 3, hydrate: true);
        
        _knownLocations.AddRange(towns);
        _knownLocations.AddRange(dungeons);
        _knownLocations.AddRange(wilderness);
        
        _locationsInitialized = true;
    }

    /// <summary>
    /// Perform exploration at the current location.
    /// Returns true if combat should be initiated.
    /// </summary>
    public async Task<bool> ExploreAsync()
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
            return true; // Indicates combat should start
        }

        // Peaceful exploration - gain some XP
        var xpGained = Random.Shared.Next(10, 30);
        player.GainExperience(xpGained);

        // Check if leveled up
        var newLevel = player.Level;
        if (newLevel > player.Level - 1)
        {
            await _mediator.Publish(new PlayerLeveledUp(player.Name, newLevel));
        }

        _console.ShowSuccess($"Gained {xpGained} XP!");

        // Find gold
        var goldFound = Random.Shared.Next(5, 25);
        player.Gold += goldFound;
        await _mediator.Publish(new GoldGained(player.Name, goldFound));

        // Random chance to find an item (30% chance)
        if (Random.Shared.Next(100) < 30)
        {
            // Get current location object
            var currentLoc = _knownLocations.FirstOrDefault(l => l.Name == _gameState.CurrentLocation);
            
            if (currentLoc != null && _itemGenerator != null)
            {
                // Generate location-appropriate loot using ItemGenerator
                var lootResult = _locationGenerator.GenerateLocationLoot(currentLoc);
                
                if (lootResult != null)
                {
                    // Create budget request based on location danger
                    var request = new RealmEngine.Core.Services.Budget.BudgetItemRequest
                    {
                        EnemyType = "exploration",
                        EnemyLevel = player.Level,
                        IsBoss = false,
                        IsElite = false,
                        ItemCategory = lootResult.ItemCategory ?? "materials"
                    };
                    
                    var item = await _itemGenerator.GenerateItemWithBudgetAsync(request);
                    if (item != null)
                    {
                        player.Inventory.Add(item);
                        _console.ShowSuccess($"Found: {item.Name}!");
                    }
                }
            }
        }

        return false; // No combat
    }

    /// <summary>
    /// Allow player to travel to a different location.
    /// </summary>
    public async Task TravelToLocation()
    {
        await InitializeLocationsAsync();
        
        var availableLocations = _knownLocations
            .Where(loc => loc.Name != _gameState.CurrentLocation)
            .ToList();

        if (!availableLocations.Any())
        {
            _console.ShowInfo("No other locations available.");
            return;
        }

        var locationNames = availableLocations.Select(l => l.Name).ToArray();
        var choice = _console.ShowMenu(
            $"Current Location: {_gameState.CurrentLocation}\n\nWhere would you like to travel?",
            locationNames.Concat(new[] { "Cancel" }).ToArray()
        );

        if (choice == "Cancel")
            return;

        _gameState.UpdateLocation(choice);

        _console.ShowSuccess($"Traveled to {_gameState.CurrentLocation}");

        // Check for dropped items at the new location
        await CheckForDroppedItemsAsync(choice);
    }

    /// <summary>
    /// Check for dropped items at the current location and allow player to recover them.
    /// </summary>
    private async Task CheckForDroppedItemsAsync(string location)
    {
        var result = await _mediator.Send(new GetDroppedItemsQuery { Location = location });

        if (result.HasItems)
        {
            _console.ShowWarning($"\n⚠️  You see your dropped items here! ({result.Items.Count} items)");

            if (_console.Confirm("Retrieve your items?"))
            {
                // Recover items
                var saveGame = _saveGameService.GetCurrentSave();
                if (saveGame != null && saveGame.Character != null)
                {
                    saveGame.Character.Inventory.AddRange(result.Items);
                    saveGame.DroppedItemsAtLocations.Remove(location);

                    _console.ShowSuccess($"Recovered {result.Items.Count} items!");
                    await Task.Delay(1500);
                }
            }
        }
    }

    /// <summary>
    /// Get all known locations.
    /// </summary>
    public virtual async Task<IReadOnlyList<Location>> GetKnownLocationsAsync()
    {
        await InitializeLocationsAsync();
        return _knownLocations.AsReadOnly();
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