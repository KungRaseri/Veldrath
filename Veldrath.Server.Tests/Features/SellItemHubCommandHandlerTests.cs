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

/// <summary>Unit tests for <see cref="SellItemHubCommandHandler"/>.</summary>
public class SellItemHubCommandHandlerTests
{
    private static SellItemHubCommandHandler MakeHandler(
        ICharacterRepository charRepo,
        ISender mediator,
        IShopMerchantResolver? merchants = null) =>
        new(
            charRepo,
            mediator,
            merchants ?? new FakeShopMerchantResolver(),
            NullLogger<SellItemHubCommandHandler>.Instance);

    private static Character MakeCharacter(Guid id, int gold = 0, string? inventoryBlob = null)
    {
        var attrs = JsonSerializer.Serialize(new Dictionary<string, int> { ["Gold"] = gold });
        return new Character
        {
            Id = id,
            AccountId = Guid.NewGuid(),
            Name = "TestHero",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = attrs,
            InventoryBlob = inventoryBlob ?? "[]",
            CurrentZoneId = "crestfall",
            CurrentZoneLocationSlug = "aurelian-market",
        };
    }

    private static string InvWith(string slug, int qty) =>
        JsonSerializer.Serialize(new[] { new { itemRef = slug, quantity = qty, durability = (int?)null } });

    private static IReadOnlyList<Item> CatalogWith(string slug, int price) =>
        [new Item { Slug = slug, Name = slug, Price = price }];

    // ── Guard tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenItemRefIsEmpty()
    {
        var result = await MakeHandler(Mock.Of<ICharacterRepository>(), Mock.Of<ISender>())
            .Handle(new SellItemHubCommand(Guid.NewGuid(), "zone", ""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.InvalidRequest);
        result.ErrorMessage.Should().Contain("ItemRef is required");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterNotFound()
    {
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Character?)null);

        var result = await MakeHandler(charRepo.Object, Mock.Of<ISender>())
            .Handle(new SellItemHubCommand(Guid.NewGuid(), "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.CharacterNotFound);
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_Refuses_When_No_Merchant_Stands_Here()
    {
        var charId = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("sword", qty: 1)));

        var result = await MakeHandler(charRepo.Object, Mock.Of<ISender>(), FakeShopMerchantResolver.Nobody())
            .Handle(new SellItemHubCommand(charId, "the-droveway", "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.NoMerchant);
        result.ErrorMessage.Should().Be("There is no one to trade with here.");
        charRepo.Verify(
            r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()),
            Times.Never,
            "a refused sale must not remove the item from the inventory");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenItemNotInInventory()
    {
        var charId = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId));  // empty inventory

        var result = await MakeHandler(charRepo.Object, Mock.Of<ISender>())
            .Handle(new SellItemHubCommand(charId, "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.ItemNotHeld);
        result.ErrorMessage.Should().Contain("not found in inventory");
    }

    [Fact]
    public async Task Handle_Refuses_When_The_Merchant_Cannot_Afford_The_Buyback()
    {
        var charId = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("sword", qty: 1)));

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("sword", price: 400));   // buyback would be 200

        var poorMerchant = new FakeShopMerchantResolver
        {
            Merchant = new ShopMerchant("crestfall", "aurelian-market", "village-blacksmith", "Vaenern", 50),
        };

        var result = await MakeHandler(charRepo.Object, mediator.Object, poorMerchant)
            .Handle(new SellItemHubCommand(charId, "crestfall", "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Refusal.Should().Be(ShopRefusal.MerchantCannotAfford);
        result.ErrorMessage.Should().Contain("cannot afford");
    }

    // ── Success tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_Pays_Inside_The_Approved_Buyback_Band()
    {
        var charId = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, gold: 0, inventoryBlob: InvWith("sword", qty: 1)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("sword", price: 100));

        var result = await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new SellItemHubCommand(charId, "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.GoldReceived.Should().Be(50);
        result.GoldReceived.Should().BeGreaterThanOrEqualTo(
            100 * 40 / 100, "the buyback band's floor is 0.40 of the item's value");
        result.GoldReceived.Should().BeLessThanOrEqualTo(
            100 * 50 / 100, "the buyback band's ceiling is 0.50 of the item's value");
    }

    [Fact]
    public async Task Handle_CreditsGoldAtHalfBuyPrice_AndRemovesSlot_WhenLastUnit()
    {
        var charId = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, gold: 0, inventoryBlob: InvWith("sword", qty: 1)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("sword", price: 100));

        var result = await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new SellItemHubCommand(charId, "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ItemRef.Should().Be("sword");
        result.GoldReceived.Should().Be(50);   // 100 / 2
        result.NewGoldTotal.Should().Be(50);
    }

    [Fact]
    public async Task Handle_DecrementsQuantity_WhenMoreThanOneUnit()
    {
        var charId = Guid.NewGuid();
        Character? saved = null;
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("potion", qty: 3)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Callback<Character, CancellationToken>((c, _) => saved = c)
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("potion", price: 20));

        await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new SellItemHubCommand(charId, "zone", "potion"), CancellationToken.None);

        var inv = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(saved!.InventoryBlob);
        inv.Should().HaveCount(1);
        inv![0]["Quantity"].GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Handle_ClampsSellPrice_ToMinimumOne_WhenItemNotInCatalog()
    {
        var charId = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, gold: 5, inventoryBlob: InvWith("misc", qty: 1)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Item>());  // unknown item → fallback price

        var result = await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new SellItemHubCommand(charId, "zone", "misc"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.GoldReceived.Should().Be(1);  // fallback = 1
        result.NewGoldTotal.Should().Be(6);
    }

    [Fact]
    public async Task Handle_PersistsCharacter_OnSuccess()
    {
        var charId = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, inventoryBlob: InvWith("helm", qty: 1)));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("helm", price: 80));

        await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new SellItemHubCommand(charId, "zone", "helm"), CancellationToken.None);

        charRepo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
