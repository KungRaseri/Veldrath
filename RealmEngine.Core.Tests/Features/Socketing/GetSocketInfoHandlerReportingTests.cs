using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Socketing.Queries;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Socketing;

/// <summary>
/// Reporting tests for <see cref="GetSocketInfoHandler"/>: flat indexing, per-type grouping, filled /
/// empty accounting, linked-group aggregation and the error boundary.
/// </summary>
[Trait("Category", "Feature")]
public class GetSocketInfoHandlerReportingTests
{
    private static GetSocketInfoHandler CreateHandler(ISaveGameService saveGameService) =>
        new(saveGameService, NullLogger<GetSocketInfoHandler>.Instance);

    private static GetSocketInfoHandler CreateHandler(SaveGame? saveGame)
    {
        var service = new Mock<ISaveGameService>();
        service.Setup(s => s.GetCurrentSave()).Returns(saveGame);
        return CreateHandler(service.Object);
    }

    private static Gem MakeGem(string name = "Ruby") =>
        new() { Id = "gem-1", Name = name, SocketType = SocketType.Gem };

    [Fact]
    public async Task Handle_Reports_Per_Type_Groups_And_Socket_State()
    {
        // Arrange — two gem sockets (one filled) and one empty rune socket.
        var filled = new Socket { Type = SocketType.Gem, Content = MakeGem(), IsLocked = true, LinkGroup = 3 };
        var empty = new Socket { Type = SocketType.Gem };
        var runeSocket = new Socket { Type = SocketType.Rune };
        var sword = new Item { Id = "sword-01", Name = "Iron Sword" };
        sword.Sockets[SocketType.Gem] = [filled, empty];
        sword.Sockets[SocketType.Rune] = [runeSocket];
        var handler = CreateHandler(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        // Act
        var result = await handler.Handle(new GetSocketInfoQuery("sword-01"), default);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Be("Socket information retrieved successfully");
        result.TotalSockets.Should().Be(3);
        result.FilledSockets.Should().Be(1);
        result.EmptySockets.Should().Be(2);

        result.SocketsByType.Keys.Should().BeEquivalentTo([SocketType.Gem, SocketType.Rune]);
        result.SocketsByType[SocketType.Gem].Should().HaveCount(2);

        var gemDetails = result.SocketsByType[SocketType.Gem];
        gemDetails[0].Index.Should().Be(0, "gem sockets are numbered before rune sockets");
        gemDetails[0].IsEmpty.Should().BeFalse();
        gemDetails[0].IsLocked.Should().BeTrue();
        gemDetails[0].LinkGroup.Should().Be(3);
        gemDetails[0].Content.Should().NotBeNull();

        gemDetails[1].Index.Should().Be(1);
        gemDetails[1].IsEmpty.Should().BeTrue();
        gemDetails[1].IsLocked.Should().BeFalse();
        gemDetails[1].Content.Should().BeNull();

        result.SocketsByType[SocketType.Rune].Single().Index.Should().Be(2);
        result.SocketsByType[SocketType.Rune].Single().IsEmpty.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_Aggregates_Linked_Socket_Groups_With_A_Size_Bonus()
    {
        // Arrange — two sockets in link group 1, one unlinked.
        var sword = new Item { Id = "sword-01", Name = "Iron Sword" };
        sword.Sockets[SocketType.Gem] =
        [
            new Socket { Type = SocketType.Gem, LinkGroup = 1 },
            new Socket { Type = SocketType.Gem, LinkGroup = 1 },
            new Socket { Type = SocketType.Gem, LinkGroup = -1 }
        ];
        var handler = CreateHandler(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        // Act
        var result = await handler.Handle(new GetSocketInfoQuery("sword-01"), default);

        // Assert
        var group = result.LinkedGroups.Should().ContainSingle().Subject;
        group.LinkGroupId.Should().Be(1);
        group.SocketIndices.Should().Equal(0, 1);
        group.LinkSize.Should().Be(2);
        group.BonusMultiplier.Should().BeApproximately(1.2, 0.0001);
    }

    [Fact]
    public async Task Handle_Resolves_Items_From_Equipped_Slots()
    {
        var helmetSocket = new Socket { Type = SocketType.Crystal, Content = new Crystal { Name = "Life Crystal" } };
        var helmet = new Item { Id = "helmet-05", Name = "Iron Helm" };
        helmet.Sockets[SocketType.Crystal] = [helmetSocket];
        var handler = CreateHandler(new SaveGame
        {
            Character = new Character { Name = "Hero", EquippedHelmet = helmet }
        });

        var result = await handler.Handle(new GetSocketInfoQuery("helmet-05"), default);

        result.Success.Should().BeTrue();
        result.FilledSockets.Should().Be(1);
        result.SocketsByType[SocketType.Crystal].Single().Content.Should().BeSameAs(helmetSocket.Content);
    }

    [Fact]
    public async Task Handle_Reports_Zero_Counts_For_An_Item_Without_Sockets()
    {
        var sword = new Item { Id = "sword-01", Name = "Iron Sword" };
        var handler = CreateHandler(new SaveGame { Character = new Character { EquippedMainHand = sword } });

        var result = await handler.Handle(new GetSocketInfoQuery("sword-01"), default);

        result.Success.Should().BeTrue();
        result.TotalSockets.Should().Be(0);
        result.FilledSockets.Should().Be(0);
        result.EmptySockets.Should().Be(0);
        result.SocketsByType.Should().BeEmpty();
        result.LinkedGroups.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Returns_An_Error_Result_When_The_Save_Lookup_Throws()
    {
        var service = new Mock<ISaveGameService>();
        service.Setup(s => s.GetCurrentSave()).Throws(new InvalidOperationException("save store offline"));
        var handler = CreateHandler(service.Object);

        var result = await handler.Handle(new GetSocketInfoQuery("sword-01"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().StartWith("Error: ").And.Contain("save store offline");
        result.TotalSockets.Should().Be(0);
    }
}
