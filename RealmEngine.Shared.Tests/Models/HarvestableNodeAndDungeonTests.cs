using FluentAssertions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;
using Xunit;

namespace RealmEngine.Shared.Tests.Models;

[Trait("Category", "Models")]
public class HarvestableNodeTests
{
    private static HarvestableNode MakeNode(int currentHealth, int maxHealth) => new()
    {
        NodeId = "node_001",
        NodeType = "copper_vein",
        DisplayName = "Copper Vein",
        CurrentHealth = currentHealth,
        MaxHealth = maxHealth,
    };

    // GetNodeState

    [Fact]
    public void GetNodeState_ZeroMaxHealth_ReturnsEmpty()
    {
        var node = MakeNode(0, 0);
        node.GetNodeState().Should().Be(NodeState.Empty);
    }

    [Fact]
    public void GetNodeState_FullHealth_ReturnsHealthy()
    {
        var node = MakeNode(100, 100);
        node.GetNodeState().Should().Be(NodeState.Healthy);
    }

    [Fact]
    public void GetNodeState_EightyPercentHealth_ReturnsHealthy()
    {
        var node = MakeNode(80, 100);
        node.GetNodeState().Should().Be(NodeState.Healthy);
    }

    [Fact]
    public void GetNodeState_SixtyPercentHealth_ReturnsDepleted()
    {
        var node = MakeNode(60, 100);
        node.GetNodeState().Should().Be(NodeState.Depleted);
    }

    [Fact]
    public void GetNodeState_FortyPercentHealth_ReturnsDepleted()
    {
        var node = MakeNode(40, 100);
        node.GetNodeState().Should().Be(NodeState.Depleted);
    }

    [Fact]
    public void GetNodeState_TwentyPercentHealth_ReturnsExhausted()
    {
        var node = MakeNode(20, 100);
        node.GetNodeState().Should().Be(NodeState.Exhausted);
    }

    [Fact]
    public void GetNodeState_TenPercentHealth_ReturnsExhausted()
    {
        var node = MakeNode(10, 100);
        node.GetNodeState().Should().Be(NodeState.Exhausted);
    }

    [Fact]
    public void GetNodeState_BelowTenPercentHealth_ReturnsEmpty()
    {
        var node = MakeNode(5, 100);
        node.GetNodeState().Should().Be(NodeState.Empty);
    }

    [Fact]
    public void GetNodeState_ZeroHealth_ReturnsEmpty()
    {
        var node = MakeNode(0, 100);
        node.GetNodeState().Should().Be(NodeState.Empty);
    }

    // CanHarvest

    [Fact]
    public void CanHarvest_HealthyNode_ReturnsTrue()
    {
        var node = MakeNode(100, 100);
        node.CanHarvest().Should().BeTrue();
    }

    [Fact]
    public void CanHarvest_DepletedNode_ReturnsTrue()
    {
        var node = MakeNode(50, 100);
        node.CanHarvest().Should().BeTrue();
    }

    [Fact]
    public void CanHarvest_ExhaustedNode_ReturnsTrue()
    {
        var node = MakeNode(15, 100);
        node.CanHarvest().Should().BeTrue();
    }

    [Fact]
    public void CanHarvest_EmptyNode_ReturnsFalse()
    {
        var node = MakeNode(0, 100);
        node.CanHarvest().Should().BeFalse();
    }

    [Fact]
    public void CanHarvest_ZeroMaxHealth_ReturnsFalse()
    {
        var node = MakeNode(0, 0);
        node.CanHarvest().Should().BeFalse();
    }

    // GetHealthPercent

    [Fact]
    public void GetHealthPercent_ZeroMaxHealth_ReturnsZero()
    {
        var node = MakeNode(0, 0);
        node.GetHealthPercent().Should().Be(0);
    }

    [Fact]
    public void GetHealthPercent_FullHealth_Returns100()
    {
        var node = MakeNode(100, 100);
        node.GetHealthPercent().Should().Be(100);
    }

    [Fact]
    public void GetHealthPercent_ZeroHealth_ReturnsZero()
    {
        var node = MakeNode(0, 100);
        node.GetHealthPercent().Should().Be(0);
    }

    [Fact]
    public void GetHealthPercent_HalfHealth_Returns50()
    {
        var node = MakeNode(50, 100);
        node.GetHealthPercent().Should().Be(50);
    }

    [Fact]
    public void GetHealthPercent_Rounds_ToNearestInteger()
    {
        // 1/3 of 300 = 100, so 33.33... rounds to 33
        var node = MakeNode(100, 300);
        node.GetHealthPercent().Should().Be(33);
    }
}

[Trait("Category", "Models")]
public class DungeonInstanceTests
{
    private static DungeonRoom MakeRoom(int roomNumber, bool isCleared = false) => new()
    {
        Id = $"room_{roomNumber}",
        RoomNumber = roomNumber,
        Type = "combat",
        IsCleared = isCleared,
    };

    private static DungeonInstance MakeDungeon(int totalRooms, int currentRoom, params DungeonRoom[] rooms) => new()
    {
        Id = "dungeon_001",
        LocationId = "location_001",
        Name = "Test Dungeon",
        Level = 1,
        TotalRooms = totalRooms,
        CurrentRoomNumber = currentRoom,
        Rooms = [.. rooms],
    };

    // GetCurrentRoom

    [Fact]
    public void GetCurrentRoom_MatchingRoom_ReturnsCorrectRoom()
    {
        var room = MakeRoom(2);
        var dungeon = MakeDungeon(3, 2, MakeRoom(1), room, MakeRoom(3));

        dungeon.GetCurrentRoom().Should().BeSameAs(room);
    }

    [Fact]
    public void GetCurrentRoom_NoMatchingRoom_ReturnsNull()
    {
        var dungeon = MakeDungeon(3, 5, MakeRoom(1), MakeRoom(2));

        dungeon.GetCurrentRoom().Should().BeNull();
    }

    [Fact]
    public void GetCurrentRoom_EmptyRooms_ReturnsNull()
    {
        var dungeon = MakeDungeon(3, 1);

        dungeon.GetCurrentRoom().Should().BeNull();
    }

    // CanProceed

    [Fact]
    public void CanProceed_CurrentRoomCleared_ReturnsTrue()
    {
        var dungeon = MakeDungeon(3, 1, MakeRoom(1, isCleared: true));

        dungeon.CanProceed().Should().BeTrue();
    }

    [Fact]
    public void CanProceed_CurrentRoomNotCleared_ReturnsFalse()
    {
        var dungeon = MakeDungeon(3, 1, MakeRoom(1, isCleared: false));

        dungeon.CanProceed().Should().BeFalse();
    }

    [Fact]
    public void CanProceed_NoRooms_ReturnsFalse()
    {
        var dungeon = MakeDungeon(3, 1);

        dungeon.CanProceed().Should().BeFalse();
    }

    [Fact]
    public void CanProceed_NoMatchingCurrentRoom_ReturnsFalse()
    {
        var dungeon = MakeDungeon(3, 99, MakeRoom(1, isCleared: true));

        dungeon.CanProceed().Should().BeFalse();
    }

    // IsFinalRoom

    [Fact]
    public void IsFinalRoom_CurrentEqualsTotal_ReturnsTrue()
    {
        var dungeon = MakeDungeon(5, 5, MakeRoom(5));

        dungeon.IsFinalRoom().Should().BeTrue();
    }

    [Fact]
    public void IsFinalRoom_CurrentLessThanTotal_ReturnsFalse()
    {
        var dungeon = MakeDungeon(5, 3, MakeRoom(3));

        dungeon.IsFinalRoom().Should().BeFalse();
    }

    [Fact]
    public void IsFinalRoom_CurrentGreaterThanTotal_ReturnsTrue()
    {
        // Edge case: current > total still satisfies >= check
        var dungeon = MakeDungeon(3, 5, MakeRoom(5));

        dungeon.IsFinalRoom().Should().BeTrue();
    }

    [Fact]
    public void IsFinalRoom_FirstRoom_ReturnsFalse()
    {
        var dungeon = MakeDungeon(10, 1, MakeRoom(1));

        dungeon.IsFinalRoom().Should().BeFalse();
    }
}
