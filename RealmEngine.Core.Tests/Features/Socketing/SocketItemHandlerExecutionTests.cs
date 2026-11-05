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
/// Execution-path tests for <see cref="SocketItemHandler"/>: flat socket indexing across socket types,
/// the validation hand-off to <see cref="SocketService"/>, persistence, event publication and the
/// error boundary. Wave 1 covered only the argument/session guards.
/// </summary>
[Trait("Category", "Feature")]
public class SocketItemHandlerExecutionTests
{
    private static SocketService MakeSocketService() => new(NullLogger<SocketService>.Instance);

    private static Gem MakeGem(string name = "Ruby") => new()
    {
        Id = "gem-1",
        Name = name,
        SocketType = SocketType.Gem,
        Traits = new Dictionary<string, TraitValue> { ["Strength"] = new TraitValue(5.0, TraitType.Number) }
    };

    private static Rune MakeRune(string name = "Shael") =>
        new() { Id = "rune-1", Name = name, SocketType = SocketType.Rune };

    private sealed record Harness(
        SocketItemHandler Handler,
        Mock<ISaveGameService> SaveGameService,
        Mock<IPublisher> Publisher,
        SaveGame SaveGame,
        List<INotification> Published);

    private static Harness CreateHarness(SaveGame saveGame, SocketService? socketService = null)
    {
        var saveGameService = new Mock<ISaveGameService>();
        saveGameService.Setup(s => s.GetCurrentSave()).Returns(saveGame);

        var published = new List<INotification>();
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .Callback<INotification, CancellationToken>((notification, _) => published.Add(notification))
                 .Returns(Task.CompletedTask);

        var handler = new SocketItemHandler(
            saveGameService.Object,
            socketService ?? MakeSocketService(),
            publisher.Object,
            NullLogger<SocketItemHandler>.Instance);

        return new Harness(handler, saveGameService, publisher, saveGame, published);
    }

    private static Item MakeSword(params (SocketType Type, Socket Socket)[] sockets)
    {
        var item = new Item { Id = "sword-01", Name = "Iron Sword" };
        foreach (var (type, socket) in sockets)
        {
            if (!item.Sockets.TryGetValue(type, out var list))
            {
                list = [];
                item.Sockets[type] = list;
            }
            list.Add(socket);
        }
        return item;
    }

    [Fact]
    public async Task Handle_Sockets_An_Item_Into_An_Empty_Socket_And_Persists()
    {
        // Arrange
        var socket = new Socket { Type = SocketType.Gem, LinkGroup = 2 };
        var sword = MakeSword((SocketType.Gem, socket));
        var saveGame = new SaveGame { Character = new Character { Name = "Hero", EquippedMainHand = sword } };
        var harness = CreateHarness(saveGame);
        var gem = MakeGem();

        // Act
        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 0, gem), default);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Ruby");
        result.SocketedItem.Should().BeSameAs(gem);
        result.AppliedTraits.Should().ContainKey("Strength").WhoseValue.AsDouble().Should().Be(5.0);
        result.IsLinked.Should().BeTrue("the socket belongs to link group 2");
        socket.Content.Should().BeSameAs(gem);
        harness.SaveGameService.Verify(s => s.SaveGame(saveGame), Times.Once);

        var notification = harness.Published.Should().ContainSingle().Which.Should().BeOfType<ItemSocketed>().Subject;
        notification.ItemId.Should().Be("sword-01");
        notification.SocketableItemName.Should().Be("Ruby");
        notification.SocketType.Should().Be(SocketType.Gem);
        notification.SocketIndex.Should().Be(0);
        notification.AppliedTraits.Should().ContainKey("Strength");
    }

    [Fact]
    public async Task Handle_Reports_An_Unlinked_Socket_As_Not_Linked()
    {
        var socket = new Socket { Type = SocketType.Gem };
        var sword = MakeSword((SocketType.Gem, socket));
        var harness = CreateHarness(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 0, MakeGem()), default);

        result.Success.Should().BeTrue();
        result.IsLinked.Should().BeFalse("LinkGroup defaults to -1");
        result.LinkBonusMultiplier.Should().Be(1.0);
    }

    [Fact]
    public async Task Handle_Resolves_The_Item_From_Inventory_When_It_Is_Not_Equipped()
    {
        var socket = new Socket { Type = SocketType.Gem };
        var sword = MakeSword((SocketType.Gem, socket));
        var character = new Character { Name = "Hero" };
        character.Inventory.Add(sword);
        var harness = CreateHarness(new SaveGame { Character = character });

        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 0, MakeGem()), default);

        result.Success.Should().BeTrue();
        socket.Content.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_Addresses_Sockets_By_Flat_Index_Across_Socket_Types()
    {
        // Gems are ordered before runes, so flat index 1 is the second gem and index 2 is the rune.
        var firstGem = new Socket { Type = SocketType.Gem };
        var secondGem = new Socket { Type = SocketType.Gem };
        var runeSocket = new Socket { Type = SocketType.Rune };
        var sword = MakeSword((SocketType.Gem, firstGem), (SocketType.Gem, secondGem), (SocketType.Rune, runeSocket));
        var harness = CreateHarness(new SaveGame { Character = new Character { EquippedMainHand = sword } });
        var rune = MakeRune();

        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 2, rune), default);

        result.Success.Should().BeTrue();
        runeSocket.Content.Should().BeSameAs(rune);
        firstGem.Content.Should().BeNull();
        secondGem.Content.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Socket_Index_Is_Out_Of_Range()
    {
        var sword = MakeSword((SocketType.Gem, new Socket { Type = SocketType.Gem }));
        var harness = CreateHarness(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 3, MakeGem()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("out of range");
        harness.SaveGameService.Verify(s => s.SaveGame(It.IsAny<SaveGame>()), Times.Never);
        harness.Published.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Target_Socket_Is_Locked()
    {
        var socket = new Socket { Type = SocketType.Gem, IsLocked = true };
        var sword = MakeSword((SocketType.Gem, socket));
        var harness = CreateHarness(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 0, MakeGem()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("locked");
        socket.Content.Should().BeNull();
        harness.SaveGameService.Verify(s => s.SaveGame(It.IsAny<SaveGame>()), Times.Never);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Target_Socket_Is_Already_Filled()
    {
        var existing = MakeGem("Sapphire");
        var socket = new Socket { Type = SocketType.Gem, Content = existing };
        var sword = MakeSword((SocketType.Gem, socket));
        var harness = CreateHarness(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 0, MakeGem()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("already filled");
        socket.Content.Should().BeSameAs(existing);
    }

    [Fact]
    public async Task Handle_Returns_Failure_When_The_Item_Type_Does_Not_Match_The_Socket()
    {
        var socket = new Socket { Type = SocketType.Gem };
        var sword = MakeSword((SocketType.Gem, socket));
        var harness = CreateHarness(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        var result = await harness.Handler.Handle(new SocketItemCommand("sword-01", 0, MakeRune()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("mismatch");
        socket.Content.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Returns_An_Error_Result_When_Publishing_Throws()
    {
        // Arrange — the socket is filled before the event is published; the handler reports the failure.
        var socket = new Socket { Type = SocketType.Gem };
        var sword = MakeSword((SocketType.Gem, socket));
        var saveGameService = new Mock<ISaveGameService>();
        saveGameService.Setup(s => s.GetCurrentSave())
                       .Returns(new SaveGame { Character = new Character { EquippedMainHand = sword } });
        var publisher = new Mock<IPublisher>();
        publisher.Setup(p => p.Publish(It.IsAny<INotification>(), It.IsAny<CancellationToken>()))
                 .ThrowsAsync(new InvalidOperationException("bus offline"));
        var handler = new SocketItemHandler(
            saveGameService.Object, MakeSocketService(), publisher.Object,
            NullLogger<SocketItemHandler>.Instance);

        // Act
        var result = await handler.Handle(new SocketItemCommand("sword-01", 0, MakeGem()), default);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().StartWith("Error: ").And.Contain("bus offline");
    }
}
