using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Exploration;
using RealmEngine.Core.Features.Exploration.Commands;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Exploration.Commands;

[Trait("Category", "Feature")]
public class VisitShopHandlerTests
{
    // ShopEconomyService has no null-checks in its constructor; safe to pass null for infra deps
    // in tests that exercise early-exit validation paths.
    private static ShopEconomyService FakeShopService() =>
        new(null!, NullLogger<ShopEconomyService>.Instance);

    private static VisitShopHandler CreateHandler(Mock<ExplorationService>? explorationSvc = null) =>
        new(
            (explorationSvc ?? new Mock<ExplorationService>()).Object,
            FakeShopService(),
            NullLogger<VisitShopHandler>.Instance);

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationNotFound()
    {
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([]);

        var result = await CreateHandler(explorationSvc).Handle(
            new VisitShopCommand("ghost-town", ""), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ghost-town");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationHasNoShop()
    {
        var location = new Location { Id = "inn-only", Name = "Rustic Inn", Description = "A rustic inn.", HasShop = false, Type = "towns" };
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([location]);

        var result = await CreateHandler(explorationSvc).Handle(
            new VisitShopCommand("inn-only", ""), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("shop");
    }

    [Fact]
    public async Task Handle_ReturnsFailure_WhenLocationHasNoMerchantNpc()
    {
        var location = new Location
        {
            Id = "empty-market",
            Name = "Abandoned Market",
            Description = "An abandoned market.",
            HasShop = true,
            Type = "towns",
            NpcObjects = [new NPC { Name = "Guard", Occupation = "Guard" }] // Not a merchant
        };
        var explorationSvc = new Mock<ExplorationService>();
        explorationSvc.Setup(s => s.GetKnownLocationsAsync()).ReturnsAsync([location]);

        var result = await CreateHandler(explorationSvc).Handle(
            new VisitShopCommand("empty-market", ""), default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("merchant");
    }
}
