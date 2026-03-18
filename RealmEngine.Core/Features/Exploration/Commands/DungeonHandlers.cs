using MediatR;
using RealmEngine.Core.Features.Exploration.Services;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Handler for EnterDungeonCommand.
/// </summary>
public class EnterDungeonHandler : IRequestHandler<EnterDungeonCommand, EnterDungeonResult>
{
    private readonly ExplorationService _explorationService;
    private readonly DungeonGeneratorService _dungeonGenerator;
    private readonly IGameStateService _gameState;
    private readonly ILogger<EnterDungeonHandler> _logger;

    private static readonly Dictionary<string, DungeonInstance> _activeDungeons = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="EnterDungeonHandler"/> class.
    /// </summary>
    public EnterDungeonHandler(
        ExplorationService explorationService,
        DungeonGeneratorService dungeonGenerator,
        IGameStateService gameState,
        ILogger<EnterDungeonHandler> logger)
    {
        _explorationService = explorationService;
        _dungeonGenerator = dungeonGenerator;
        _gameState = gameState;
        _logger = logger;
    }

    /// <summary>
    /// Handles the EnterDungeonCommand.
    /// </summary>
    public async Task<EnterDungeonResult> Handle(EnterDungeonCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Find the location
            var locations = await _explorationService.GetKnownLocationsAsync();
            var location = locations.FirstOrDefault(l => l.Id == request.LocationId);

            if (location == null)
            {
                return new EnterDungeonResult
                {
                    Success = false,
                    ErrorMessage = $"Location '{request.LocationId}' not found."
                };
            }

            // Check if location is a dungeon
            if (location.Type.ToLower() != "dungeons")
            {
                return new EnterDungeonResult
                {
                    Success = false,
                    ErrorMessage = $"{location.Name} is not a dungeon."
                };
            }

            // Get character level for scaling
            var player = _gameState.Player;
            if (player == null)
            {
                return new EnterDungeonResult
                {
                    Success = false,
                    ErrorMessage = "No active character found."
                };
            }

            // Generate the dungeon
            var dungeon = await _dungeonGenerator.GenerateDungeonAsync(location, player.Level);
            
            // Store the active dungeon instance
            _activeDungeons[dungeon.Id] = dungeon;

            _logger.LogInformation(
                "Character {CharacterName} entered dungeon {DungeonName} ({RoomCount} rooms, level {Level})",
                player.Name, dungeon.Name, dungeon.TotalRooms, dungeon.Level);

            return new EnterDungeonResult
            {
                Success = true,
                Dungeon = dungeon,
                CurrentRoom = dungeon.GetCurrentRoom()
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error entering dungeon {LocationId}", request.LocationId);
            return new EnterDungeonResult
            {
                Success = false,
                ErrorMessage = $"An error occurred while entering the dungeon: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Gets an active dungeon instance by ID (for use by other handlers).
    /// </summary>
    public static DungeonInstance? GetActiveDungeon(string dungeonId)
    {
        return _activeDungeons.TryGetValue(dungeonId, out var dungeon) ? dungeon : null;
    }

    /// <summary>
    /// Removes a completed dungeon instance.
    /// </summary>
    public static void RemoveDungeon(string dungeonId)
    {
        _activeDungeons.Remove(dungeonId);
    }
}

/// <summary>
/// Handler for ProceedToNextRoomCommand.
/// </summary>
public class ProceedToNextRoomHandler : IRequestHandler<ProceedToNextRoomCommand, ProceedToNextRoomResult>
{
    private readonly ILogger<ProceedToNextRoomHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ProceedToNextRoomHandler"/> class.
    /// </summary>
    public ProceedToNextRoomHandler(ILogger<ProceedToNextRoomHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Handles the ProceedToNextRoomCommand.
    /// </summary>
    public Task<ProceedToNextRoomResult> Handle(ProceedToNextRoomCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var dungeon = EnterDungeonHandler.GetActiveDungeon(request.DungeonInstanceId);
            
            if (dungeon == null)
            {
                return Task.FromResult(new ProceedToNextRoomResult
                {
                    Success = false,
                    ErrorMessage = "Dungeon instance not found."
                });
            }

            // Check if current room is cleared
            if (!dungeon.CanProceed())
            {
                return Task.FromResult(new ProceedToNextRoomResult
                {
                    Success = false,
                    ErrorMessage = "You must clear the current room before proceeding."
                });
            }

            // Check if this is the last room
            if (dungeon.IsFinalRoom())
            {
                dungeon.IsCompleted = true;
                EnterDungeonHandler.RemoveDungeon(dungeon.Id);
                
                _logger.LogInformation("Dungeon {DungeonName} completed!", dungeon.Name);
                
                return Task.FromResult(new ProceedToNextRoomResult
                {
                    Success = true,
                    CurrentRoom = dungeon.GetCurrentRoom(),
                    DungeonCompleted = true
                });
            }

            // Proceed to next room
            dungeon.CurrentRoomNumber++;
            var newRoom = dungeon.GetCurrentRoom();

            _logger.LogInformation(
                "Proceeded to room {RoomNumber}/{TotalRooms} in dungeon {DungeonName}",
                dungeon.CurrentRoomNumber, dungeon.TotalRooms, dungeon.Name);

            return Task.FromResult(new ProceedToNextRoomResult
            {
                Success = true,
                CurrentRoom = newRoom,
                DungeonCompleted = false
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error proceeding to next room in dungeon {DungeonId}", request.DungeonInstanceId);
            return Task.FromResult(new ProceedToNextRoomResult
            {
                Success = false,
                ErrorMessage = $"An error occurred: {ex.Message}"
            });
        }
    }
}

/// <summary>
/// Handler for ClearDungeonRoomCommand.
/// </summary>
public class ClearDungeonRoomHandler : IRequestHandler<ClearDungeonRoomCommand, ClearDungeonRoomResult>
{
    private readonly IGameStateService _gameState;
    private readonly ILogger<ClearDungeonRoomHandler> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ClearDungeonRoomHandler"/> class.
    /// </summary>
    public ClearDungeonRoomHandler(
        IGameStateService gameState,
        ILogger<ClearDungeonRoomHandler> logger)
    {
        _gameState = gameState;
        _logger = logger;
    }

    /// <summary>
    /// Handles the ClearDungeonRoomCommand.
    /// </summary>
    public Task<ClearDungeonRoomResult> Handle(ClearDungeonRoomCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var dungeon = EnterDungeonHandler.GetActiveDungeon(request.DungeonInstanceId);
            
            if (dungeon == null)
            {
                return Task.FromResult(new ClearDungeonRoomResult
                {
                    Success = false,
                    ErrorMessage = "Dungeon instance not found."
                });
            }

            var room = dungeon.Rooms.FirstOrDefault(r => r.RoomNumber == request.RoomNumber);
            
            if (room == null)
            {
                return Task.FromResult(new ClearDungeonRoomResult
                {
                    Success = false,
                    ErrorMessage = $"Room {request.RoomNumber} not found."
                });
            }

            // Mark room as cleared
            room.IsCleared = true;

            // Award gold and XP
            var player = _gameState.Player;
            if (player != null)
            {
                player.Gold += room.GoldReward;
                player.GainExperience(room.ExperienceReward);
            }

            _logger.LogInformation(
                "Cleared room {RoomNumber} in dungeon {DungeonName}. Rewarded {Gold} gold, {XP} XP",
                room.RoomNumber, dungeon.Name, room.GoldReward, room.ExperienceReward);

            return Task.FromResult(new ClearDungeonRoomResult
            {
                Success = true,
                GoldRewarded = room.GoldReward,
                ExperienceRewarded = room.ExperienceReward,
                LootFound = room.Loot
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error clearing room {RoomNumber} in dungeon {DungeonId}", 
                request.RoomNumber, request.DungeonInstanceId);
            return Task.FromResult(new ClearDungeonRoomResult
            {
                Success = false,
                ErrorMessage = $"An error occurred: {ex.Message}"
            });
        }
    }
}
