using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Socketing;
using RealmEngine.Core.Features.Socketing.Commands;
using RealmEngine.Core.Features.Socketing.Queries;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Socketing;

[Trait("Category", "Feature")]
public class SocketItemHandlerTests
{
    private static SocketService MakeSocketService() =>
        new(NullLogger<SocketService>.Instance);

    private static SocketItemHandler CreateHandler(
        ISaveGameService? saveGameService = null,
        IPublisher? publisher = null) =>
        new(
            saveGameService ?? Mock.Of<ISaveGameService>(),
            MakeSocketService(),
            publisher ?? Mock.Of<IPublisher>(),
            NullLogger<SocketItemHandler>.Instance);

    private static Gem MakeGem(string name = "Ruby") =>
        new() { Id = Guid.NewGuid().ToString(), Name = name, SocketType = SocketType.Gem };

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSocketableItemIsNull()
    {
        var result = await CreateHandler().Handle(new SocketItemCommand("sword-01", 0, null!), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("null");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSocketIndexIsNegative()
    {
        var result = await CreateHandler().Handle(new SocketItemCommand("sword-01", -1, MakeGem()), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("-1");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var gem = MakeGem("Sapphire");

        var result = await CreateHandler(mockSaveSvc.Object).Handle(new SocketItemCommand("sword-01", 0, gem), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No active game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenEquipmentItemNotFound()
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(new SaveGame { Character = new Character { Name = "Hero" } });
        var gem = MakeGem("Emerald");

        var result = await CreateHandler(mockSaveSvc.Object).Handle(new SocketItemCommand("missing-id", 0, gem), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("missing-id");
    }
}

[Trait("Category", "Feature")]
public class RemoveSocketedItemHandlerTests
{
    private static RemoveSocketedItemHandler CreateHandler(ISaveGameService? saveGameService = null) =>
        new(
            saveGameService ?? Mock.Of<ISaveGameService>(),
            new SocketService(NullLogger<SocketService>.Instance),
            Mock.Of<IPublisher>(),
            NullLogger<RemoveSocketedItemHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenSocketIndexIsNegative()
    {
        var result = await CreateHandler().Handle(new RemoveSocketedItemCommand("sword-01", -1, 20), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("-1");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenGoldCostIsNegative()
    {
        var result = await CreateHandler().Handle(new RemoveSocketedItemCommand("sword-01", 0, -5), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("negative");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(mockSaveSvc.Object).Handle(new RemoveSocketedItemCommand("sword-01", 0, 20), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No active game session");
    }
}

[Trait("Category", "Feature")]
public class GetSocketInfoHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);
        var handler = new GetSocketInfoHandler(mockSaveSvc.Object, NullLogger<GetSocketInfoHandler>.Instance);

        var result = await handler.Handle(new GetSocketInfoQuery("helmet-05"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("No active game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenItemNotFound()
    {
        var mockSaveSvc = new Mock<ISaveGameService>();
        mockSaveSvc.Setup(s => s.GetCurrentSave()).Returns(new SaveGame { Character = new Character { Name = "Hero" } });
        var handler = new GetSocketInfoHandler(mockSaveSvc.Object, NullLogger<GetSocketInfoHandler>.Instance);

        var result = await handler.Handle(new GetSocketInfoQuery("helmet-05"), default);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("helmet-05");
    }
}

[Trait("Category", "Feature")]
public class GetSocketCostHandlerTests
{
    private static GetSocketCostHandler Handler =>
        new(NullLogger<GetSocketCostHandler>.Instance, Mock.Of<ISaveGameService>());

    [Fact]
    public async Task Handle_ReturnsBaseCost_ForSocketOperation_AtIndexZero()
    {
        var result = await Handler.Handle(new GetSocketCostQuery("sword-01", SocketCostType.Socket, 0), default);

        result.Success.Should().BeTrue();
        result.GoldCost.Should().Be(10);
        result.BaseCost.Should().Be(10);
        result.Modifiers.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ReturnsBaseCost_ForRemoveOperation_AtIndexZero()
    {
        var result = await Handler.Handle(new GetSocketCostQuery("sword-01", SocketCostType.Remove, 0), default);

        result.GoldCost.Should().Be(20);
        result.BaseCost.Should().Be(20);
    }

    [Fact]
    public async Task Handle_ReturnsBaseCost_ForUnlockOperation()
    {
        var result = await Handler.Handle(new GetSocketCostQuery("sword-01", SocketCostType.Unlock, 0), default);

        result.GoldCost.Should().Be(100);
    }

    [Fact]
    public async Task Handle_AppliesPositionMultiplier_ForHigherSocketIndex()
    {
        // Socket index 1 → multiplier 1.5, base 10 → 15
        var result = await Handler.Handle(new GetSocketCostQuery("sword-01", SocketCostType.Socket, 1), default);

        result.Modifiers.Should().HaveCount(1);
        result.GoldCost.Should().Be(15);
    }
}
