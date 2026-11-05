using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Core.Features.Socketing.Commands;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Socketing;

/// <summary>
/// Execution-path tests for <see cref="RemoveSocketedItemHandler"/>: the gold gate, the socket
/// validation hand-off, gold deduction + persistence, event publication and the error boundary.
/// Wave 1 covered only the argument/session guards.
/// </summary>
[Trait("Category", "Feature")]
public class RemoveSocketedItemHandlerExecutionTests
{
    private static SocketService MakeSocketService() => new(NullLogger<SocketService>.Instance);

    private static Gem MakeGem(string name = "Ruby") => new()
    {
        Id = "gem-1",
        Name = name,
        SocketType = SocketType.Gem,
        Traits = new Dictionary<string, TraitValue> { ["Strength"] = new TraitValue(5.0, TraitType.Number) }
    };

    private sealed record Harness(
        RemoveSocketedItemHandler Handler,
        Mock<ISaveGameService> SaveGameService,
        List<INotification> Published,
        SaveGame SaveGame);

    private static Harness CreateHarness(SaveGame saveGame)
    {
        var saveGameService = new Mock<ISaveGameService>();
        saveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        var published = new List<INotification>();
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .Callback<INotification, CancellationToken>((notification, _) => published.Add(notification))
                 .Returns(Task.CompletedTask);

        var handler = new RemoveSocketedItemHandler(
            saveGameService.Object,
            MakeSocketService(),
            publisher.Object,
            NullLogger<RemoveSocketedItemHandler>.Instance);

        return new Harness(handler, saveGameService, published, saveGame);
    }

    private static Item MakeSwordWithGem(Socket socket, string id = "sword-01")
    {
        var item = new Item { Id = id, Name = "Iron Sword" };
        item.Sockets[SocketType.Gem] = [socket];
        return item;
    }

    [Fact]
    public async Task Handle_Removes_The_Socketed_Item_Deducts_Gold_And_Persists()
    {
        // Arrange
        var gem = MakeGem();
        var socket = new Socket { Type = SocketType.Gem, Content = gem };
        var sword = MakeSwordWithGem(socket);
        var character = new Character { Name = "Hero", Gold = 100, EquippedMainHand = sword };
        var saveGame = new SaveGame { Character = character };
        var harness = CreateHarness(saveGame);

        // Act
        var result = await harness.Handler.Handle(new RemoveSocketedItemCommand("sword-01", 0, 20), default);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Ruby").And.Contain("20g");
        result.RemovedItem.Should().BeSameAs(gem);
        result.RemovedTraits.Should().ContainKey("Strength").WhoseValue.AsDouble().Should().Be(5.0);
        result.GoldPaid.Should().Be(20);
        socket.Content.Should().BeNull();
        character.Gold.Should().Be(80, "the removal cost is deducted from the character");
        harness.SaveGameService.Verify(s => s.SaveGame(saveGame), Times.Once);

        var notification = harness.Published.Should().ContainSingle().Which.Should().BeOfType<ItemUnsocketed>().Subject;
        notification.ItemId.Should().Be("sword-01");
        notification.SocketableItemName.Should().Be("Ruby");
        notification.SocketType.Should().Be(SocketType.Gem);
        notification.SocketIndex.Should().Be(0);
        notification.GoldCost.Should().Be(20);
    }

    [Fact]
    public async Task Handle_Skips_The_Gold_Gate_When_Removal_Is_Free()
    {
        // Arrange — a broke character can still remove a gem when the cost is zero.
        var socket = new Socket { Type = SocketType.Gem, Content = MakeGem() };
        var character = new Character { Name = "Hero", Gold = 0, EquippedMainHand = MakeSwordWithGem(socket) };
        var harness = CreateHarness(new SaveGame { Character = character });

        // Act
        var result = await harness.Handler.Handle(new RemoveSocketedItemCommand("sword-01", 0), default);

        // Assert
        result.Success.Should().BeTrue();
        result.GoldPaid.Should().Be(0);
        character.Gold.Should().Be(0);
        socket.Content.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Character_Cannot_Afford_The_Removal()
    {
        var socket = new Socket { Type = SocketType.Gem, Content = MakeGem() };
        var character = new Character { Name = "Hero", Gold = 5, EquippedMainHand = MakeSwordWithGem(socket) };
        var harness = CreateHarness(new SaveGame { Character = character });

        var result = await harness.Handler.Handle(new RemoveSocketedItemCommand("sword-01", 0, 20), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Insufficient gold").And.Contain("20g").And.Contain("5g");
        result.RemovedItem.Should().BeNull();
        socket.Content.Should().NotBeNull("a failed removal must not clear the socket");
        character.Gold.Should().Be(5);
        harness.SaveGameService.Verify(s => s.SaveGame(It.IsAny<SaveGame>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Socket_Is_Out_Of_Range()
    {
        var sword = MakeSwordWithGem(new Socket { Type = SocketType.Gem, Content = MakeGem() });
        var harness = CreateHarness(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        var result = await harness.Handler.Handle(new RemoveSocketedItemCommand("sword-01", 4), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("out of range");
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Item_Is_Not_Found()
    {
        var character = new Character { Name = "Hero", Gold = 100 };
        var harness = CreateHarness(new SaveGame { Character = character });

        var result = await harness.Handler.Handle(new RemoveSocketedItemCommand("missing-item", 0, 20), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("missing-item");
        result.RemovedItem.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Socket_Is_Empty()
    {
        var socket = new Socket { Type = SocketType.Gem };
        var sword = MakeSwordWithGem(socket);
        var character = new Character { Name = "Hero", Gold = 100, EquippedMainHand = sword };
        var harness = CreateHarness(new SaveGame { Character = character });

        var result = await harness.Handler.Handle(new RemoveSocketedItemCommand("sword-01", 0, 20), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already empty");
        character.Gold.Should().Be(100, "nothing was removed, so nothing is charged");
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Socket_Is_Locked()
    {
        var gem = MakeGem();
        var socket = new Socket { Type = SocketType.Gem, Content = gem, IsLocked = true };
        var sword = MakeSwordWithGem(socket);
        var character = new Character { Name = "Hero", Gold = 100, EquippedMainHand = sword };
        var harness = CreateHarness(new SaveGame { Character = character });

        var result = await harness.Handler.Handle(new RemoveSocketedItemCommand("sword-01", 0, 20), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
        socket.Content.Should().BeSameAs(gem);
        character.Gold.Should().Be(100);
    }

    [Fact]
    public async Task Handle_Returns_An_Error_Result_When_Publishing_Throws()
    {
        var socket = new Socket { Type = SocketType.Gem, Content = MakeGem() };
        var character = new Character { Name = "Hero", Gold = 100, EquippedMainHand = MakeSwordWithGem(socket) };
        var saveGameService = new Mock<ISaveGameService>();
        saveGameService.Setup(s => s.GetCurrentSave()).Returns(new SaveGame { Character = character });
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("bus offline"));
        var handler = new RemoveSocketedItemHandler(
            saveGameService.Object, MakeSocketService(), publisher.Object,
            NullLogger<RemoveSocketedItemHandler>.Instance);

        var result = await handler.Handle(new RemoveSocketedItemCommand("sword-01", 0, 20), default);

        result.Success.Should().BeFalse();
        result.Message.Should().StartWith("Error: ").And.Contain("bus offline");
    }
}
