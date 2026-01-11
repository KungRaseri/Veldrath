using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Exploration.Commands;

/// <summary>
/// Command to enter a dungeon and begin room-by-room progression.
/// </summary>
public record EnterDungeonCommand(string LocationId, string CharacterName) : IRequest<EnterDungeonResult>;

/// <summary>
/// Result of entering a dungeon.
/// </summary>
public record EnterDungeonResult
{
    /// <summary>Gets whether entering the dungeon was successful.</summary>
    public bool Success { get; init; }
    
    /// <summary>Gets the generated dungeon instance.</summary>
    public DungeonInstance? Dungeon { get; init; }
    
    /// <summary>Gets the first room the player enters.</summary>
    public DungeonRoom? CurrentRoom { get; init; }
    
    /// <summary>Gets an error message if entering failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Command to proceed to the next room in a dungeon.
/// </summary>
public record ProceedToNextRoomCommand(string DungeonInstanceId, string CharacterName) : IRequest<ProceedToNextRoomResult>;

/// <summary>
/// Result of proceeding to the next room.
/// </summary>
public record ProceedToNextRoomResult
{
    /// <summary>Gets whether proceeding was successful.</summary>
    public bool Success { get; init; }
    
    /// <summary>Gets the new current room.</summary>
    public DungeonRoom? CurrentRoom { get; init; }
    
    /// <summary>Gets whether the dungeon is now complete.</summary>
    public bool DungeonCompleted { get; init; }
    
    /// <summary>Gets an error message if proceeding failed.</summary>
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Command to clear the current room (after combat or claiming treasure).
/// </summary>
public record ClearDungeonRoomCommand(string DungeonInstanceId, int RoomNumber) : IRequest<ClearDungeonRoomResult>;

/// <summary>
/// Result of clearing a room.
/// </summary>
public record ClearDungeonRoomResult
{
    /// <summary>Gets whether clearing was successful.</summary>
    public bool Success { get; init; }
    
    /// <summary>Gets the gold rewarded.</summary>
    public int GoldRewarded { get; init; }
    
    /// <summary>Gets the XP rewarded.</summary>
    public int ExperienceRewarded { get; init; }
    
    /// <summary>Gets any loot items found.</summary>
    public List<Item> LootFound { get; init; } = new();
    
    /// <summary>Gets an error message if clearing failed.</summary>
    public string? ErrorMessage { get; init; }
}
