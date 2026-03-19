using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Core.Features.Shop.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Shop;

[Trait("Category", "Feature")]
public class RefreshMerchantInventoryHandlerTests
{
    // ShopEconomyService has no null-checks in constructor — safe to pass null for infra dependencies
    // in tests that exercise early-exit validation paths.
    private static ShopEconomyService FakeShopService() =>
        new(null!, NullLogger<ShopEconomyService>.Instance);

    private static RefreshMerchantInventoryCommandHandler CreateHandler(
        Mock<ISaveGameService>? saveSvc = null) =>
        new(
            (saveSvc ?? new Mock<ISaveGameService>()).Object,
            FakeShopService(),
            NullLogger<RefreshMerchantInventoryCommandHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNoActiveSave()
    {
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns((SaveGame?)null);

        var result = await CreateHandler(saveSvc).Handle(
            new RefreshMerchantInventoryCommand("merchant-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("game session");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenMerchantNotInSave()
    {
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new RefreshMerchantInventoryCommand("nonexistent-merchant"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Merchant not found");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenNpcIsNotAMerchant()
    {
        var guard = new NPC
        {
            Id = "guard-1",
            Name = "Gate Guard",
            Occupation = "Guard",
            Traits = new Dictionary<string, TraitValue>() // No "isMerchant" trait
        };
        var save = new SaveGame { PlayerName = "Hero", KnownNPCs = [guard] };
        var saveSvc = new Mock<ISaveGameService>();
        saveSvc.Setup(s => s.GetCurrentSave()).Returns(save);

        var result = await CreateHandler(saveSvc).Handle(
            new RefreshMerchantInventoryCommand("guard-1"), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not a merchant");
    }
}
