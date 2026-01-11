using FluentAssertions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Exploration;

/// <summary>
/// Unit tests for dungeon progression models and logic.
/// </summary>
public class DungeonProgressionTests
{
    [Fact]
    public void DungeonInstance_Should_Track_Current_Room()
    {
        // Arrange
        var dungeon = new DungeonInstance
        {
            Id = Guid.NewGuid().ToString(),
            LocationId = "dungeon-1",
            Name = "Dark Catacombs",
            Level = 3,
            TotalRooms = 3,
            Rooms = new List<DungeonRoom>
            {
                new() { Id = "1", RoomNumber = 1, Type = "combat", IsCleared = true },
                new() { Id = "2", RoomNumber = 2, Type = "combat", IsCleared = false },
                new() { Id = "3", RoomNumber = 3, Type = "treasure", IsCleared = false }
            },
            CurrentRoomNumber = 1
        };

        // Act
        var currentRoom = dungeon.GetCurrentRoom();

        // Assert
        currentRoom.Should().NotBeNull();
        currentRoom!.RoomNumber.Should().Be(1);
        currentRoom.IsCleared.Should().BeTrue();
    }

    [Fact]
    public void DungeonInstance_Should_Not_Allow_Proceed_When_Room_Not_Cleared()
    {
        // Arrange
        var dungeon = new DungeonInstance
        {
            Id = Guid.NewGuid().ToString(),
            LocationId = "dungeon-1",
            Name = "Dark Catacombs",
            Level = 3,
            TotalRooms = 2,
            Rooms = new List<DungeonRoom>
            {
                new() { Id = "1", RoomNumber = 1, Type = "combat", IsCleared = false },
                new() { Id = "2", RoomNumber = 2, Type = "combat", IsCleared = false }
            },
            CurrentRoomNumber = 1
        };

        // Act
        var canProceed = dungeon.CanProceed();

        // Assert
        canProceed.Should().BeFalse();
    }

    [Fact]
    public void DungeonInstance_Should_Allow_Proceed_When_Room_Is_Cleared()
    {
        // Arrange
        var dungeon = new DungeonInstance
        {
            Id = Guid.NewGuid().ToString(),
            LocationId = "dungeon-1",
            Name = "Dark Catacombs",
            Level = 3,
            TotalRooms = 2,
            Rooms = new List<DungeonRoom>
            {
                new() { Id = "1", RoomNumber = 1, Type = "combat", IsCleared = true },
                new() { Id = "2", RoomNumber = 2, Type = "combat", IsCleared = false }
            },
            CurrentRoomNumber = 1
        };

        // Act
        var canProceed = dungeon.CanProceed();

        // Assert
        canProceed.Should().BeTrue();
    }

    [Fact]
    public void DungeonInstance_Should_Identify_Final_Room()
    {
        // Arrange
        var dungeon = new DungeonInstance
        {
            Id = Guid.NewGuid().ToString(),
            LocationId = "dungeon-1",
            Name = "Dark Catacombs",
            Level = 3,
            TotalRooms = 3,
            Rooms = new List<DungeonRoom>
            {
                new() { Id = "1", RoomNumber = 1, Type = "combat", IsCleared = true },
                new() { Id = "2", RoomNumber = 2, Type = "combat", IsCleared = true },
                new() { Id = "3", RoomNumber = 3, Type = "boss", IsCleared = false }
            },
            CurrentRoomNumber = 3
        };

        // Act
        var isFinalRoom = dungeon.IsFinalRoom();

        // Assert
        isFinalRoom.Should().BeTrue();
    }

    [Fact]
    public void DungeonRoom_Combat_Should_Have_Enemies()
    {
        // Arrange & Act
        var room = new DungeonRoom
        {
            Id = Guid.NewGuid().ToString(),
            RoomNumber = 1,
            Type = "combat",
            IsCleared = false,
            Enemies = new List<Enemy>
            {
                new() { Name = "Goblin", Level = 2, Health = 20, GoldReward = 5, XPReward = 10 },
                new() { Name = "Orc", Level = 3, Health = 35, GoldReward = 10, XPReward = 20 }
            }
        };

        // Assert
        room.Enemies.Should().HaveCount(2);
        room.Enemies.Should().Contain(e => e.Name == "Goblin");
        room.Enemies.Should().Contain(e => e.Name == "Orc");
        room.Type.Should().Be("combat");
    }

    [Fact]
    public void DungeonRoom_Treasure_Should_Have_Loot()
    {
        // Arrange
        var loot = new Item
        {
            Name = "Magic Sword",
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Rare
        };

        // Act
        var room = new DungeonRoom
        {
            Id = Guid.NewGuid().ToString(),
            RoomNumber = 2,
            Type = "treasure",
            IsCleared = false,
            Loot = new List<Item> { loot }
        };

        // Assert
        room.Loot.Should().HaveCount(1);
        room.Loot.First().Name.Should().Be("Magic Sword");
        room.Loot.First().Rarity.Should().Be(ItemRarity.Rare);
        room.Type.Should().Be("treasure");
    }

    [Fact]
    public void DungeonRoom_Boss_Should_Have_High_Rewards()
    {
        // Arrange & Act
        var bossRoom = new DungeonRoom
        {
            Id = Guid.NewGuid().ToString(),
            RoomNumber = 5,
            Type = "boss",
            IsCleared = false,
            Enemies = new List<Enemy>
            {
                new() 
                { 
                    Name = "Shadow Lord", 
                    Level = 10, 
                    Health = 200, 
                    GoldReward = 100, 
                    XPReward = 200 
                }
            },
            GoldReward = 100,
            ExperienceReward = 200
        };

        // Assert
        bossRoom.Type.Should().Be("boss");
        bossRoom.Enemies.Should().HaveCount(1);
        bossRoom.Enemies.First().Health.Should().BeGreaterThan(100);
        bossRoom.GoldReward.Should().BeGreaterThanOrEqualTo(100);
        bossRoom.ExperienceReward.Should().BeGreaterThanOrEqualTo(200);
    }
}
