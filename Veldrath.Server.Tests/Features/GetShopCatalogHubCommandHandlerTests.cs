using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using RealmEngine.Shared.Models;
using Veldrath.Server.Features.Shop;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="GetShopCatalogHubCommandHandler"/>.</summary>
public class GetShopCatalogHubCommandHandlerTests
{
    private GetShopCatalogHubCommandHandler MakeHandler(ISender mediator) =>
        new(mediator, NullLogger<GetShopCatalogHubCommandHandler>.Instance);

    private static IReadOnlyList<Item> CatalogWith(params (string Slug, string Name, int Price)[] items) =>
        items.Select(i => new Item { Slug = i.Slug, Name = i.Name, Price = i.Price }).ToList();

    // ── Guard tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenZoneIdIsEmpty()
    {
        var mediator = Mock.Of<ISender>();
        var handler  = MakeHandler(mediator);

        var result = await handler.Handle(new GetShopCatalogHubCommand(""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ZoneId is required");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Handle_ReturnsError_WhenZoneIdIsWhitespace(string zoneId)
    {
        var mediator = Mock.Of<ISender>();
        var handler  = MakeHandler(mediator);

        var result = await handler.Handle(new GetShopCatalogHubCommand(zoneId), CancellationToken.None);

        result.Success.Should().BeFalse();
    }

    // ── Success tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsCatalogItems_WithSellPriceAtHalfBuyRoundedDown()
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith(("sword", "Iron Sword", 100)));

        var result = await MakeHandler(mediator.Object).Handle(
            new GetShopCatalogHubCommand("fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        var item = result.Items[0];
        item.ItemRef.Should().Be("sword");
        item.DisplayName.Should().Be("Iron Sword");
        item.BuyPrice.Should().Be(100);
        item.SellPrice.Should().Be(50);  // 100 / 2
    }

    [Fact]
    public async Task Handle_ClampsSellPrice_ToMinimumOne()
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith(("pebble", "Pebble", 1)));

        var result = await MakeHandler(mediator.Object).Handle(
            new GetShopCatalogHubCommand("fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items[0].SellPrice.Should().Be(1);  // floor(1/2)=0 → clamped to 1
    }

    [Fact]
    public async Task Handle_ExcludesZeroPriceItems_FromCatalog()
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith(
                    ("sellable", "Sellable Item", 50),
                    ("quest",    "Quest Item",     0)));

        var result = await MakeHandler(mediator.Object).Handle(
            new GetShopCatalogHubCommand("fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].ItemRef.Should().Be("sellable");
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenCatalogIsEmpty()
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Item>());

        var result = await MakeHandler(mediator.Object).Handle(
            new GetShopCatalogHubCommand("fenwick-crossing"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_PassesZoneIdThrough_WithoutFilteringByZone()
    {
        // The handler currently returns the full catalog regardless of zone — verify zone ID is accepted.
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith(("axe", "Battle Axe", 200)));

        var result = await MakeHandler(mediator.Object).Handle(
            new GetShopCatalogHubCommand("kaldrek-maw"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
    }
}
