using RealmEngine.Shared.Models;
using RealmEngine.Core.Generators.Modern;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Exploration.Services;

/// <summary>
/// Generates and manages dungeon instances with room-by-room progression.
/// </summary>
public class DungeonGeneratorService
{
    private readonly EnemyGenerator _enemyGenerator;
    private readonly ILogger<DungeonGeneratorService> _logger;
    private readonly Random _random = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="DungeonGeneratorService"/> class.
    /// </summary>
    public DungeonGeneratorService(
        EnemyGenerator enemyGenerator,
        ILogger<DungeonGeneratorService> logger)
    {
        _enemyGenerator = enemyGenerator;
        _logger = logger;
    }

    /// <summary>
    /// Generates a new dungeon instance for a location.
    /// </summary>
    /// <param name="location">The dungeon location.</param>
    /// <param name="characterLevel">The character's level (for scaling).</param>
    public async Task<DungeonInstance> GenerateDungeonAsync(Location location, int characterLevel)
    {
        if (location.Type.ToLower() != "dungeons")
        {
            throw new ArgumentException("Location must be a dungeon type.", nameof(location));
        }

        // Determine dungeon size based on danger rating
        var roomCount = location.DangerRating switch
        {
            <= 3 => _random.Next(5, 8),    // Easy: 5-7 rooms
            <= 6 => _random.Next(8, 12),   // Medium: 8-11 rooms
            <= 8 => _random.Next(10, 14),  // Hard: 10-13 rooms
            _ => _random.Next(12, 16)      // Deadly: 12-15 rooms
        };

        var dungeon = new DungeonInstance
        {
            Id = $"dungeon-{Guid.NewGuid()}",
            LocationId = location.Id,
            Name = location.Name,
            Level = location.Level,
            TotalRooms = roomCount,
            CurrentRoomNumber = 1
        };

        // Generate rooms
        for (int i = 1; i <= roomCount; i++)
        {
            var room = await GenerateRoomAsync(i, roomCount, location, characterLevel);
            dungeon.Rooms.Add(room);
        }

        _logger.LogInformation(
            "Generated dungeon {DungeonName} with {RoomCount} rooms at level {Level}",
            dungeon.Name, roomCount, dungeon.Level);

        return dungeon;
    }

    /// <summary>
    /// Generates a single dungeon room.
    /// </summary>
    private async Task<DungeonRoom> GenerateRoomAsync(int roomNumber, int totalRooms, Location location, int characterLevel)
    {
        var room = new DungeonRoom
        {
            Id = $"room-{roomNumber}",
            RoomNumber = roomNumber,
            Type = DetermineRoomType(roomNumber, totalRooms),
            Description = GenerateRoomDescription(roomNumber, totalRooms)
        };

        // Populate room based on type
        switch (room.Type.ToLower())
        {
            case "combat":
                await PopulateCombatRoomAsync(room, location, characterLevel);
                break;
            case "treasure":
                PopulateTreasureRoom(room, location.Level);
                break;
            case "boss":
                await PopulateBossRoomAsync(room, location, characterLevel);
                break;
            case "rest":
                // Rest room has no enemies or loot
                room.Description = "A quiet chamber with ancient carvings. You feel safe here.";
                break;
        }

        return room;
    }

    /// <summary>
    /// Determines the room type based on position in dungeon.
    /// </summary>
    private string DetermineRoomType(int roomNumber, int totalRooms)
    {
        // Final room is always boss
        if (roomNumber == totalRooms)
            return "boss";

        // Middle room might be rest area (20% chance)
        if (roomNumber == totalRooms / 2 && _random.Next(100) < 20)
            return "rest";

        // 15% chance for treasure room (not first or last)
        if (roomNumber > 1 && roomNumber < totalRooms && _random.Next(100) < 15)
            return "treasure";

        // Default to combat
        return "combat";
    }

    /// <summary>
    /// Generates a room description.
    /// </summary>
    private string GenerateRoomDescription(int roomNumber, int totalRooms)
    {
        var descriptions = new[]
        {
            "A damp stone corridor stretches before you.",
            "Ancient pillars support a crumbling ceiling.",
            "Flickering torches cast dancing shadows on the walls.",
            "The air grows colder as you venture deeper.",
            "Strange runes glow faintly on the floor.",
            "Cobwebs hang from every corner of this abandoned chamber.",
            "The sound of dripping water echoes through the darkness.",
            "Bones litter the floor, remnants of previous adventurers."
        };

        if (roomNumber == totalRooms)
            return "A massive chamber looms before you. You sense a powerful presence within.";

        return descriptions[_random.Next(descriptions.Length)];
    }

    /// <summary>
    /// Populates a combat room with enemies.
    /// </summary>
    private async Task PopulateCombatRoomAsync(DungeonRoom room, Location location, int characterLevel)
    {
        // Generate 1-3 enemies based on room difficulty
        var enemyCount = _random.Next(1, 4);
        
        // Get appropriate enemy category for this dungeon
        var enemyCategory = GetDungeonEnemyCategory(location);
        
        // Generate enemies
        var enemies = await _enemyGenerator.GenerateEnemiesAsync(enemyCategory, enemyCount, hydrate: true);
        
        // Filter to appropriate level (within ±2 levels of character)
        room.Enemies = enemies
            .Where(e => Math.Abs(e.Level - characterLevel) <= 2)
            .Take(enemyCount)
            .ToList();

        // Calculate rewards based on enemies
        room.GoldReward = room.Enemies.Sum(e => e.GoldReward);
        room.ExperienceReward = room.Enemies.Sum(e => e.XPReward);
    }

    /// <summary>
    /// Populates a treasure room with loot.
    /// </summary>
    private void PopulateTreasureRoom(DungeonRoom room, int dungeonLevel)
    {
        room.Description = "A treasure chamber! Chests and valuables glitter in the torchlight.";
        
        // Generous gold reward
        room.GoldReward = _random.Next(50, 150) * dungeonLevel;
        
        // Small XP for finding treasure
        room.ExperienceReward = 20 * dungeonLevel;
        
        // Treasure room auto-clears (no combat)
        room.IsCleared = false; // Player must "claim" the treasure
    }

    /// <summary>
    /// Populates a boss room with a powerful enemy.
    /// </summary>
    private async Task PopulateBossRoomAsync(DungeonRoom room, Location location, int characterLevel)
    {
        var enemyCategory = GetDungeonEnemyCategory(location);
        
        // Generate boss enemies (higher level, stronger)
        var enemies = await _enemyGenerator.GenerateEnemiesAsync(enemyCategory, 5, hydrate: true);
        
        // Pick the strongest enemy as the boss
        var boss = enemies
            .Where(e => e.Level >= characterLevel)
            .OrderByDescending(e => e.MaxHealth)
            .FirstOrDefault();

        if (boss != null)
        {
            // Buff the boss
            boss.MaxHealth = (int)(boss.MaxHealth * 1.5);
            boss.Health = boss.MaxHealth;
            boss.GoldReward = (int)(boss.GoldReward * 2);
            boss.XPReward = (int)(boss.XPReward * 2);
            
            room.Enemies = new List<Enemy> { boss };
        }

        room.GoldReward = boss?.GoldReward ?? 100;
        room.ExperienceReward = boss?.XPReward ?? 100;
    }

    /// <summary>
    /// Determines enemy category based on dungeon features.
    /// </summary>
    private string GetDungeonEnemyCategory(Location location)
    {
        // Check location features for hints
        var features = location.Features.Select(f => f.ToLower()).ToList();
        
        if (features.Any(f => f.Contains("undead") || f.Contains("crypt") || f.Contains("tomb")))
            return "undead";
        if (features.Any(f => f.Contains("demon") || f.Contains("infernal")))
            return "demons";
        if (features.Any(f => f.Contains("elemental")))
            return "elementals";
        if (features.Any(f => f.Contains("construct") || f.Contains("mechanical")))
            return "constructs";
        
        // Default to humanoids (bandits, cultists, etc.)
        return "humanoids";
    }
}
