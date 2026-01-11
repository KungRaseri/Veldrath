namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a single room within a dungeon.
/// </summary>
public class DungeonRoom
{
    /// <summary>Gets or sets the unique identifier for this room.</summary>
    public required string Id { get; set; }
    
    /// <summary>Gets or sets the room number (1-based index in the dungeon).</summary>
    public int RoomNumber { get; set; }
    
    /// <summary>Gets or sets the room type (combat, treasure, rest, boss).</summary>
    public required string Type { get; set; }
    
    /// <summary>Gets or sets the room description.</summary>
    public string Description { get; set; } = "A dark chamber.";
    
    /// <summary>Gets or sets whether this room has been cleared.</summary>
    public bool IsCleared { get; set; }
    
    /// <summary>Gets or sets the enemies in this room (if combat type).</summary>
    public List<Enemy> Enemies { get; set; } = new();
    
    /// <summary>Gets or sets the loot in this room (treasure type or post-combat).</summary>
    public List<Item> Loot { get; set; } = new();
    
    /// <summary>Gets or sets the gold reward for clearing this room.</summary>
    public int GoldReward { get; set; }
    
    /// <summary>Gets or sets the XP reward for clearing this room.</summary>
    public int ExperienceReward { get; set; }
    
    /// <summary>Gets or sets IDs of connected rooms (for branching paths).</summary>
    public List<string> ConnectedRooms { get; set; } = new();
}

/// <summary>
/// Represents an active dungeon run with room progression.
/// </summary>
public class DungeonInstance
{
    /// <summary>Gets or sets the unique identifier for this dungeon instance.</summary>
    public required string Id { get; set; }
    
    /// <summary>Gets or sets the base location ID this dungeon is associated with.</summary>
    public required string LocationId { get; set; }
    
    /// <summary>Gets or sets the dungeon name.</summary>
    public required string Name { get; set; }
    
    /// <summary>Gets or sets the dungeon difficulty level.</summary>
    public int Level { get; set; }
    
    /// <summary>Gets or sets the total number of rooms in this dungeon.</summary>
    public int TotalRooms { get; set; }
    
    /// <summary>Gets or sets the current room number (1-based).</summary>
    public int CurrentRoomNumber { get; set; } = 1;
    
    /// <summary>Gets or sets all rooms in this dungeon.</summary>
    public List<DungeonRoom> Rooms { get; set; } = new();
    
    /// <summary>Gets or sets whether this dungeon has been completed.</summary>
    public bool IsCompleted { get; set; }
    
    /// <summary>Gets or sets the timestamp when this dungeon was entered.</summary>
    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>Gets the current room.</summary>
    public DungeonRoom? GetCurrentRoom() => Rooms.FirstOrDefault(r => r.RoomNumber == CurrentRoomNumber);
    
    /// <summary>Gets whether the player can proceed to the next room.</summary>
    public bool CanProceed() => GetCurrentRoom()?.IsCleared ?? false;
    
    /// <summary>Gets whether this is the final room.</summary>
    public bool IsFinalRoom() => CurrentRoomNumber >= TotalRooms;
}
