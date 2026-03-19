using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Features.Exploration.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

/// <summary>
/// Injects a <see cref="DungeonInstance"/> into <see cref="EnterDungeonHandler"/>'s
/// internal static dictionary for the duration of a test, then cleans up.
/// </summary>
file sealed class ActiveDungeonScope : IDisposable
{
    private static readonly FieldInfo DictField =
        typeof(EnterDungeonHandler).GetField("_activeDungeons",
            BindingFlags.NonPublic | BindingFlags.Static)!;

    private readonly string _id;

    public ActiveDungeonScope(DungeonInstance dungeon)
    {
        _id = dungeon.Id;
        Dict[_id] = dungeon;
    }

    private static Dictionary<string, DungeonInstance> Dict =>
        (Dictionary<string, DungeonInstance>)DictField.GetValue(null)!;

    public void Dispose() => Dict.Remove(_id);
}

/// <summary>
/// Unit tests for <see cref="EnterDungeonHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class EnterDungeonHandlerTests
{
    private static EnterDungeonHandler CreateHandler(
        IReadOnlyList<Location>? locations = null,
        Character? player = null)
    {
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc
            .Setup(s => s.GetKnownLocationsAsync())
            .ReturnsAsync(locations ?? []);

        var gameState = new Mock<IGameStateService>();
        gameState.Setup(g => g.Player).Returns(player!);

        // DungeonGeneratorService is not virtual — a real instance suffices since
        // all exercised paths fail before GenerateDungeonAsync is called.
        var enemyGen = new RealmEngine.Core.Generators.Modern.EnemyGenerator(
            Mock.Of<IEnemyRepository>(), NullLogger<RealmEngine.Core.Generators.Modern.EnemyGenerator>.Instance);
        var dungeonGen = new DungeonGeneratorService(enemyGen, NullLogger<DungeonGeneratorService>.Instance);

        return new EnterDungeonHandler(
            explorationSvc.Object,
            dungeonGen,
            gameState.Object,
            NullLogger<EnterDungeonHandler>.Instance);
    }

    private static Location MakeDungeon(string id = "dungeon-1", string name = "Dark Crypt") =>
        new() { Id = id, Name = name, Description = "A dark crypt.", Type = "dungeons", Level = 5, DangerRating = 3 };

    private static Location MakeTown(string id = "town-1") =>
        new() { Id = id, Name = "Ironhaven", Description = "A peaceful town.", Type = "town" };

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationNotFound()
    {
        var handler = CreateHandler(locations: [MakeDungeon("other-id")]);
        var result = await handler.Handle(new EnterDungeonCommand("missing-id", "Hero"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationIsNotDungeon()
    {
        var handler = CreateHandler(locations: [MakeTown("town-1")]);
        var result = await handler.Handle(new EnterDungeonCommand("town-1", "Hero"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a dungeon");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActivePlayer()
    {
        var handler = CreateHandler(locations: [MakeDungeon("dungeon-1")], player: null);
        var result = await handler.Handle(new EnterDungeonCommand("dungeon-1", "Hero"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("No active character");
    }
}

/// <summary>
/// Unit tests for <see cref="ProceedToNextRoomHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class ProceedToNextRoomHandlerTests
{
    private static ProceedToNextRoomHandler CreateHandler() =>
        new(NullLogger<ProceedToNextRoomHandler>.Instance);

    private static DungeonInstance MakeDungeon(int totalRooms = 3, int currentRoom = 1, bool firstRoomCleared = false)
    {
        var dungeon = new DungeonInstance
        {
            Id = $"test-dungeon-{Guid.NewGuid()}",
            LocationId = "loc-1",
            Name = "Test Dungeon",
            TotalRooms = totalRooms,
            CurrentRoomNumber = currentRoom
        };
        for (int i = 1; i <= totalRooms; i++)
        {
            dungeon.Rooms.Add(new DungeonRoom
            {
                Id = $"room-{i}",
                RoomNumber = i,
                Type = i == totalRooms ? "boss" : "combat",
                IsCleared = i == 1 && firstRoomCleared
            });
        }
        return dungeon;
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenDungeonNotFound()
    {
        var handler = CreateHandler();
        var result = await handler.Handle(new ProceedToNextRoomCommand("nonexistent-id", "Hero"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenCurrentRoomNotCleared()
    {
        var dungeon = MakeDungeon(totalRooms: 3, currentRoom: 1, firstRoomCleared: false);
        using var scope = new ActiveDungeonScope(dungeon);

        var handler = CreateHandler();
        var result = await handler.Handle(new ProceedToNextRoomCommand(dungeon.Id, "Hero"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("clear the current room");
    }

    [Fact]
    public async Task Handle_AdvancesToNextRoom_WhenRoomIsCleared()
    {
        var dungeon = MakeDungeon(totalRooms: 3, currentRoom: 1, firstRoomCleared: true);
        using var scope = new ActiveDungeonScope(dungeon);

        var handler = CreateHandler();
        var result = await handler.Handle(new ProceedToNextRoomCommand(dungeon.Id, "Hero"), default);

        result.Success.Should().BeTrue();
        result.DungeonCompleted.Should().BeFalse();
        result.CurrentRoom!.RoomNumber.Should().Be(2);
        dungeon.CurrentRoomNumber.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CompletesDungeon_WhenFinalRoomIsCleared()
    {
        var dungeon = MakeDungeon(totalRooms: 1, currentRoom: 1, firstRoomCleared: true);
        using var scope = new ActiveDungeonScope(dungeon);

        var handler = CreateHandler();
        var result = await handler.Handle(new ProceedToNextRoomCommand(dungeon.Id, "Hero"), default);

        result.Success.Should().BeTrue();
        result.DungeonCompleted.Should().BeTrue();
        // Completed dungeon is removed from active dungeons
        EnterDungeonHandler.GetActiveDungeon(dungeon.Id).Should().BeNull();
    }
}

/// <summary>
/// Unit tests for <see cref="ClearDungeonRoomHandler"/>.
/// </summary>
[Trait("Category", "Feature")]
public class ClearDungeonRoomHandlerTests
{
    private static ClearDungeonRoomHandler CreateHandler(Character? player = null)
    {
        var gameState = new Mock<IGameStateService>();
        gameState.Setup(g => g.Player).Returns(player!);
        return new ClearDungeonRoomHandler(gameState.Object, NullLogger<ClearDungeonRoomHandler>.Instance);
    }

    private static DungeonInstance MakeDungeon(int gold = 50, int xp = 100)
    {
        var dungeon = new DungeonInstance
        {
            Id = $"test-dungeon-{Guid.NewGuid()}",
            LocationId = "loc-1",
            Name = "Test Dungeon",
            TotalRooms = 3,
            CurrentRoomNumber = 1
        };
        dungeon.Rooms.Add(new DungeonRoom
        {
            Id = "room-1",
            RoomNumber = 1,
            Type = "combat",
            GoldReward = gold,
            ExperienceReward = xp
        });
        dungeon.Rooms.Add(new DungeonRoom { Id = "room-2", RoomNumber = 2, Type = "combat" });
        dungeon.Rooms.Add(new DungeonRoom { Id = "room-3", RoomNumber = 3, Type = "boss" });
        return dungeon;
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenDungeonNotFound()
    {
        var handler = CreateHandler();
        var result = await handler.Handle(new ClearDungeonRoomCommand("nonexistent-id", 1), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenRoomNotFound()
    {
        var dungeon = MakeDungeon();
        using var scope = new ActiveDungeonScope(dungeon);

        var handler = CreateHandler();
        var result = await handler.Handle(new ClearDungeonRoomCommand(dungeon.Id, 99), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Room 99");
    }

    [Fact]
    public async Task Handle_MarksRoomCleared_OnSuccess()
    {
        var dungeon = MakeDungeon();
        using var scope = new ActiveDungeonScope(dungeon);

        var handler = CreateHandler(new Character { Name = "Hero" });
        var result = await handler.Handle(new ClearDungeonRoomCommand(dungeon.Id, 1), default);

        result.Success.Should().BeTrue();
        dungeon.Rooms[0].IsCleared.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_AwardsGoldAndXp_ToPlayer()
    {
        var player = new Character { Name = "Hero", Gold = 0, Experience = 0 };
        var dungeon = MakeDungeon(gold: 50, xp: 100);
        using var scope = new ActiveDungeonScope(dungeon);

        var handler = CreateHandler(player);
        var result = await handler.Handle(new ClearDungeonRoomCommand(dungeon.Id, 1), default);

        result.Success.Should().BeTrue();
        result.GoldRewarded.Should().Be(50);
        result.ExperienceRewarded.Should().Be(100);
        player.Gold.Should().Be(50);
    }

    [Fact]
    public async Task Handle_SucceedsWithoutAwards_WhenNoPlayer()
    {
        var dungeon = MakeDungeon();
        using var scope = new ActiveDungeonScope(dungeon);

        var handler = CreateHandler(player: null); // no player → awards skipped
        var result = await handler.Handle(new ClearDungeonRoomCommand(dungeon.Id, 1), default);

        result.Success.Should().BeTrue();
        result.GoldRewarded.Should().Be(50);
    }
}
