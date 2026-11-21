using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Shop;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Tests.Features;

/// <summary>Unit tests for <see cref="GetShopCatalogHubCommandHandler"/>.</summary>
public class GetShopCatalogHubCommandHandlerTests
{
    private static readonly Guid CharacterId = Guid.Parse("2b9c1d0e-4a53-4a1b-9a4c-8f1bcf0b9a11");

    private static Character MakeCharacter(string zoneId = "crestfall", string location = "aurelian-market") =>
        new()
        {
            Id = CharacterId,
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            CurrentZoneId = zoneId,
            CurrentZoneLocationSlug = location,
        };

    private static ICharacterRepository CharacterRepo(Character? character = null)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(character ?? MakeCharacter());
        return repo.Object;
    }

    private GetShopCatalogHubCommandHandler MakeHandler(
        ISender mediator,
        ICharacterRepository? characterRepo = null,
        IShopMerchantResolver? merchants = null,
        IZoneStockReader? stock = null) =>
        new(
            characterRepo ?? CharacterRepo(),
            mediator,
            merchants ?? new FakeShopMerchantResolver(),
            stock ?? new FakeZoneStockReader().Offering("sword", "axe"),
            NullLogger<GetShopCatalogHubCommandHandler>.Instance);

    private static IReadOnlyList<Item> CatalogWith(params (string Slug, string Name, int Price)[] items) =>
        items.Select(i => new Item { Slug = i.Slug, Name = i.Name, Price = i.Price }).ToList();

    private static Mock<ISender> MediatorWith(params (string Slug, string Name, int Price)[] items)
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith(items));
        return mediator;
    }

    // ── Guard tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenZoneIdIsEmpty()
    {
        var result = await MakeHandler(MediatorWith([]).Object)
            .Handle(new GetShopCatalogHubCommand(CharacterId, ""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.InvalidRequest);
        result.ErrorMessage.Should().Contain("ZoneId is required");
    }

    [Theory]
    [InlineData("   ")]
    [InlineData("\t")]
    public async Task Handle_ReturnsError_WhenZoneIdIsWhitespace(string zoneId)
    {
        var result = await MakeHandler(MediatorWith([]).Object)
            .Handle(new GetShopCatalogHubCommand(CharacterId, zoneId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.InvalidRequest);
    }

    [Fact]
    public async Task Handle_Refuses_When_The_Character_Does_Not_Exist()
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Character?)null);

        var result = await MakeHandler(MediatorWith([]).Object, repo.Object)
            .Handle(new GetShopCatalogHubCommand(CharacterId, "crestfall"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.CharacterNotFound);
    }

    [Fact]
    public async Task Handle_Refuses_When_No_Merchant_Stands_Here()
    {
        var result = await MakeHandler(MediatorWith([]).Object, merchants: FakeShopMerchantResolver.Nobody())
            .Handle(new GetShopCatalogHubCommand(CharacterId, "crestfall"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.NoMerchant);
        result.ErrorMessage.Should().Be("There is no one to trade with here.");
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Reports_A_Zone_Mismatch_As_Such()
    {
        var merchants = new FakeShopMerchantResolver
        {
            HasMerchant = false,
            Refusal = ShopRefusal.ZoneMismatch,
        };

        var result = await MakeHandler(MediatorWith([]).Object, merchants: merchants)
            .Handle(new GetShopCatalogHubCommand(CharacterId, "skarhold"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.ZoneMismatch);
        result.ErrorMessage.Should().Contain("not standing in that zone");
    }

    // ── Success tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Offers_Only_The_Zones_Stock_List()
    {
        var mediator = MediatorWith(
            ("health-potion", "Health Potion", 15),
            ("fire-ruby", "Fire Ruby", 200),
            ("orb-of-decay", "Orb of Decay", 400));

        var result = await MakeHandler(
                mediator.Object,
                stock: new FakeZoneStockReader().Offering("health-potion"))
            .Handle(new GetShopCatalogHubCommand(CharacterId, "crestfall"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle();
        result.Items[0].ItemRef.Should().Be("health-potion");
    }

    [Fact]
    public async Task Handle_ReturnsItems_WithSellPriceAtTheBandsBuybackRate()
    {
        var result = await MakeHandler(MediatorWith(("sword", "Iron Sword", 100)).Object)
            .Handle(new GetShopCatalogHubCommand(CharacterId, "crestfall"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        var item = result.Items[0];
        item.ItemRef.Should().Be("sword");
        item.DisplayName.Should().Be("Iron Sword");
        item.BuyPrice.Should().Be(100);   // value x 1.0 — the band's markup floor
        item.SellPrice.Should().Be(50);   // value x 0.50 — the band's buyback ceiling
    }

    [Fact]
    public async Task Handle_Never_Quotes_A_Sell_Price_At_Or_Above_The_Buy_Price()
    {
        var result = await MakeHandler(
                MediatorWith(("pebble", "Pebble", 1)).Object,
                stock: new FakeZoneStockReader().Offering("pebble"))
            .Handle(new GetShopCatalogHubCommand(CharacterId, "crestfall"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items[0].BuyPrice.Should().Be(1);
        result.Items[0].SellPrice.Should().Be(0, "a one-gold item must not buy and sell for the same coin");
    }

    [Fact]
    public async Task Handle_ExcludesZeroPriceItems_FromCatalog()
    {
        var mediator = MediatorWith(
            ("sellable", "Sellable Item", 50),
            ("quest", "Quest Item", 0));

        var result = await MakeHandler(
                mediator.Object,
                stock: new FakeZoneStockReader().Offering("sellable", "quest"))
            .Handle(new GetShopCatalogHubCommand(CharacterId, "crestfall"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].ItemRef.Should().Be("sellable");
    }

    [Fact]
    public async Task Handle_ReturnsEmptyList_WhenThe_Zone_Stocks_Nothing()
    {
        var result = await MakeHandler(MediatorWith([]).Object, stock: new FakeZoneStockReader())
            .Handle(new GetShopCatalogHubCommand(CharacterId, "crestfall"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().BeEmpty();
    }
}
