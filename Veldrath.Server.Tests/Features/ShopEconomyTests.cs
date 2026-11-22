using System.Text.Json;
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

/// <summary>
/// The shop's economy invariants: a buy followed by a sell must lose gold, and the two prices must stay inside the
/// approved band.
/// </summary>
/// <remarks>
/// This is the test the card asks for by name. The two handlers are exercised against one character so the assertion is
/// about the actual pair of transactions, not about two independent price formulas that happen to look right.
/// </remarks>
public class ShopEconomyTests
{
    private static Character MakeCharacter(Guid id, int gold, string inventoryBlob = "[]")
    {
        var attrs = JsonSerializer.Serialize(new Dictionary<string, int> { ["Gold"] = gold });
        return new Character
        {
            Id = id,
            AccountId = Guid.NewGuid(),
            Name = "Shopper",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = attrs,
            InventoryBlob = inventoryBlob,
            CurrentZoneId = "crestfall",
            CurrentZoneLocationSlug = "aurelian-market",
        };
    }

    private static ICharacterRepository RepoFor(Character character)
    {
        var repo = new Mock<ICharacterRepository>();
        repo.Setup(r => r.GetByIdAsync(character.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(character);
        repo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        return repo.Object;
    }

    private static ISender CatalogFor(string slug, int price)
    {
        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Item> { new() { Slug = slug, Name = slug, Price = price } });
        return mediator.Object;
    }

    private static int GoldOf(Character character)
    {
        var attrs = JsonSerializer.Deserialize<Dictionary<string, int>>(character.Attributes)!;
        return attrs["Gold"];
    }

    [Fact]
    public async Task Buy_Then_Sell_Is_A_Net_Loss()
    {
        var character = MakeCharacter(Guid.NewGuid(), gold: 100);
        var repo = RepoFor(character);
        var catalog = CatalogFor("iron-sword", price: 25);

        var buy = new BuyItemHubCommandHandler(
            repo,
            catalog,
            new FakeShopMerchantResolver(),
            new FakeZoneStockReader().Offering("iron-sword"),
            NullLogger<BuyItemHubCommandHandler>.Instance);

        var sell = new SellItemHubCommandHandler(
            repo,
            catalog,
            new FakeShopMerchantResolver(),
            NullLogger<SellItemHubCommandHandler>.Instance);

        var purchase = await buy.Handle(
            new BuyItemHubCommand(character.Id, "crestfall", "iron-sword"),
            CancellationToken.None);

        purchase.Success.Should().BeTrue();
        purchase.GoldSpent.Should().Be(25);
        GoldOf(character).Should().Be(75);

        var sale = await sell.Handle(
            new SellItemHubCommand(character.Id, "crestfall", "iron-sword"),
            CancellationToken.None);

        sale.Success.Should().BeTrue();
        sale.GoldReceived.Should().Be(12);

        GoldOf(character).Should().Be(
            100 - 25 + 12,
            "the buyback rate is below the buy markup, so the loop returns less than it cost");
        GoldOf(character).Should().BeLessThan(
            100,
            "a buy -> sell loop must never print gold");
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(80)]
    [InlineData(101)]
    [InlineData(400)]
    public void Every_Price_Buys_Above_What_The_Merchant_Pays_Back(int value)
    {
        var buy = ShopPricing.BuyPrice(value);
        var sell = ShopPricing.SellPrice(value);

        buy.Should().BeGreaterThanOrEqualTo(value, "the markup band's floor is 1.0");
        sell.Should().BeLessThan(buy, "a merchant may not buy its own goods back for as much as it sells them");
        sell.Should().BeLessThanOrEqualTo(
            value * MerchantBands.BuybackRatePercent / 100,
            "the buyback never exceeds the band's ceiling");
    }
}
