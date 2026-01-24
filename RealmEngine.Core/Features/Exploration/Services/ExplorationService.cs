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
/// Pure domain logic - UI handled by Godot.
/// </summary>
public class ExplorationService
{
    private readonly IMediator _mediator;
    private readonly GameStateService _gameState;
    private readonly SaveGameService _saveGameService;
    private readonly LocationGenerator _locationGenerator;
    private readonly ItemGenerator? _itemGenerator;

    private readonly List<Location> _knownLocations = new();
    private bool _locationsInitialized = false;

    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorationService"/> class for mocking.
    /// </summary>
    protected ExplorationService() { _mediator = null!; _gameState = null!; _saveGameService = null!; _locationGenerator = null!; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ExplorationService"/> class.
    /// </summary>
    /// <param name="mediator">The mediator.</param>
    /// <param name="gameState">The game state service.</param>
    /// <param name="saveGameService">The save game service.</param>
    /// <param name="locationGenerator">The location generator.</param>
    /// <param name="itemGenerator">The item generator (optional).</param>
    public ExplorationService(IMediator mediator, GameStateService gameState, SaveGameService saveGameService, LocationGenerator locationGenerator, ItemGenerator? itemGenerator = null)
    {
        _mediator = mediator;
        _gameState = gameState;
        _saveGameService = saveGameService;
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
    /// Godot handles displaying results and initiating combat.
    /// </summary>
    /// <returns>Exploration result with combat encounter flag, rewards, and loot.</returns>
    public async Task<ExplorationResult> ExploreAsync()
    {
        var player = _gameState.Player;

        // 60% chance of combat encounter, 40% chance of peaceful exploration
        var encounterRoll = Random.Shared.Next(100);
        bool combatEncounter = encounterRoll < 60;

        if (combatEncounter)
        {
            return new ExplorationResult
            {
                Success = true,
                CombatEncounter = true,
                CurrentLocation = _gameState.CurrentLocation
            };
        }

        // Peaceful exploration - gain some XP
        var xpGained = Random.Shared.Next(10, 30);
        var oldLevel = player.Level;
        player.GainExperience(xpGained);
        bool leveledUp = player.Level > oldLevel;

        if (leveledUp)
        {
            await _mediator.Publish(new PlayerLeveledUp(player.Name, player.Level));
        }

        // Find gold
        var goldFound = Random.Shared.Next(5, 25);
        player.Gold += goldFound;
        await _mediator.Publish(new GoldGained(player.Name, goldFound));

        Item? itemFound = null;
        
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
                        itemFound = item;
                    }
                }
            }
        }

        return new ExplorationResult
        {
            Success = true,
            CombatEncounter = false,
            CurrentLocation = _gameState.CurrentLocation,
            XpGained = xpGained,
            GoldGained = goldFound,
            ItemFound = itemFound,
            LeveledUp = leveledUp,
            NewLevel = leveledUp ? player.Level : null
        };
    }

    /// <summary>
    /// Get available travel locations.
    /// Godot uses this to display location selection menu.
    /// </summary>
    /// <returns>Available locations and dropped items info.</returns>
    public async Task<TravelResult> GetAvailableLocations()
    {
        await InitializeLocationsAsync();
        
        var availableLocations = _knownLocations
            .Where(loc => loc.Name != _gameState.CurrentLocation)
            .ToList();

        return new TravelResult
        {
            Success = true,
            CurrentLocation = _gameState.CurrentLocation,
            AvailableLocations = availableLocations
        };
    }

    /// <summary>
    /// Travel to a specific location.
    /// Godot handles location selection UI.
    /// </summary>
    /// <param name="locationName">The location to travel to.</param>
    /// <returns>Travel result with dropped items at new location.</returns>
    public async Task<TravelResult> TravelToLocation(string locationName)
    {
        await InitializeLocationsAsync();
        
        var availableLocations = _knownLocations
            .Where(loc => loc.Name != _gameState.CurrentLocation)
            .ToList();

        var destination = availableLocations.FirstOrDefault(l => l.Name == locationName);
        
        if (destination == null)
        {
            return new TravelResult
            {
                Success = false,
                ErrorMessage = "Location not found or already at that location.",
                CurrentLocation = _gameState.CurrentLocation
            };
        }

        _gameState.UpdateLocation(locationName);

        // Check for dropped items at the new location
        var droppedItems = await CheckForDroppedItemsAsync(locationName);

        return new TravelResult
        {
            Success = true,
            CurrentLocation = locationName,
            AvailableLocations = availableLocations,
            DroppedItemsAtLocation = droppedItems
        };
    }

    /// <summary>
    /// Check for dropped items at a specific location.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <returns>List of dropped items at location.</returns>
    private async Task<List<Item>> CheckForDroppedItemsAsync(string location)
    {
        var result = await _mediator.Send(new GetDroppedItemsQuery { Location = location });
        return result.HasItems ? result.Items : new List<Item>();
    }

    /// <summary>
    /// Recover dropped items at current location.
    /// Godot handles confirmation UI.
    /// </summary>
    /// <param name="location">The location to recover items from.</param>
    /// <returns>True if items were recovered.</returns>
    public bool RecoverDroppedItems(string location)
    {
        var saveGame = _saveGameService.GetCurrentSave();
        if (saveGame != null && saveGame.Character != null && saveGame.DroppedItemsAtLocations.ContainsKey(location))
        {
            var items = saveGame.DroppedItemsAtLocations[location];
            saveGame.Character.Inventory.AddRange(items);
            saveGame.DroppedItemsAtLocations.Remove(location);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Get all known locations.
    /// </summary>
    public virtual async Task<IReadOnlyList<Location>> GetKnownLocationsAsync()
    {
        await InitializeLocationsAsync();
        return _knownLocations.AsReadOnly();
    }
}

/// <summary>
/// Result of an exploration action.
/// </summary>
public class ExplorationResult
{
    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets a value indicating whether combat was encountered.</summary>
    public bool CombatEncounter { get; set; }

    /// <summary>Gets or sets the current location name.</summary>
    public string CurrentLocation { get; set; } = string.Empty;

    /// <summary>Gets or sets the XP gained from exploration.</summary>
    public int XpGained { get; set; }

    /// <summary>Gets or sets the gold gained from exploration.</summary>
    public int GoldGained { get; set; }

    /// <summary>Gets or sets the item found during exploration.</summary>
    public Item? ItemFound { get; set; }

    /// <summary>Gets or sets a value indicating whether the player leveled up.</summary>
    public bool LeveledUp { get; set; }

    /// <summary>Gets or sets the new level if leveled up.</summary>
    public int? NewLevel { get; set; }

    /// <summary>Gets or sets the error message if failed.</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of a travel operation.
/// </summary>
public class TravelResult
{
    /// <summary>Gets or sets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the current location name.</summary>
    public string CurrentLocation { get; set; } = string.Empty;

    /// <summary>Gets or sets the available locations to travel to.</summary>
    public List<Location> AvailableLocations { get; set; } = new();

    /// <summary>Gets or sets dropped items at the location.</summary>
    public List<Item> DroppedItemsAtLocation { get; set; } = new();

    /// <summary>Gets or sets the error message if failed.</summary>
    public string? ErrorMessage { get; set; }
}