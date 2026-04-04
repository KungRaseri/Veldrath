using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.ItemCatalog.Queries;
using RealmEngine.Shared.Models;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Features.Shop;
using Character = RealmUnbound.Server.Data.Entities.Character;

namespace RealmUnbound.Server.Tests.Features;

/// <summary>Unit tests for <see cref="BuyItemHubCommandHandler"/>.</summary>
public class BuyItemHubCommandHandlerTests
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    private static BuyItemHubCommandHandler MakeHandler(
        ICharacterRepository charRepo,
        ISender mediator) =>
        new(charRepo, mediator, NullLogger<BuyItemHubCommandHandler>.Instance);

    private static Character MakeCharacter(Guid id, int gold = 200, string inventoryBlob = "[]")
    {
        var attrs = JsonSerializer.Serialize(new Dictionary<string, int> { ["Gold"] = gold });
        return new Character
        {
            Id            = id,
            AccountId     = Guid.NewGuid(),
            Name         = "TestHero",
            ClassName    = "Warrior",
            SlotIndex    = 1,
            Attributes   = attrs,
            InventoryBlob = inventoryBlob,
        };
    }

    private static IReadOnlyList<Item> CatalogWith(string slug, int price) =>
        [new Item { Slug = slug, Name = slug, Price = price }];

    // ── Guard tests ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ReturnsError_WhenItemRefIsEmpty()
    {
        var result = await MakeHandler(Mock.Of<ICharacterRepository>(), Mock.Of<ISender>())
            .Handle(new BuyItemHubCommand(Guid.NewGuid(), "zone", ""), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("ItemRef is required");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenCharacterNotFound()
    {
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((Character?)null);

        var result = await MakeHandler(charRepo.Object, Mock.Of<ISender>())
            .Handle(new BuyItemHubCommand(Guid.NewGuid(), "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenItemNotInCatalog()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId));

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(new List<Item>());   // empty catalog

        var result = await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new BuyItemHubCommand(charId, "zone", "unknown-item"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("not found in catalog");
    }

    [Fact]
    public async Task Handle_ReturnsError_WhenNotEnoughGold()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId, gold: 10));  // only 10 gold

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("sword", price: 100));  // costs 100

        var result = await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new BuyItemHubCommand(charId, "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Contain("Not enough gold");
    }

    // ── Success tests ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_DeductsGoldAndAddsItem_OnSuccess()
    {
        var charId    = Guid.NewGuid();
        var character = MakeCharacter(charId, gold: 200);
        var charRepo  = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(character);
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("sword", price: 100));

        var result = await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new BuyItemHubCommand(charId, "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.ItemRef.Should().Be("sword");
        result.GoldSpent.Should().Be(100);
        result.RemainingGold.Should().Be(100);  // 200 - 100
    }

    [Fact]
    public async Task Handle_StacksExistingInventorySlot_WhenItemAlreadyPresent()
    {
        var charId = Guid.NewGuid();
        var existingInventory = JsonSerializer.Serialize(
            new[] { new { itemRef = "sword", quantity = 1, durability = (int?)null } });
        var character = MakeCharacter(charId, gold: 200, inventoryBlob: existingInventory);

        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(character);
        Character? saved = null;
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Callback<Character, CancellationToken>((c, _) => saved = c)
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("sword", price: 50));

        var result = await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new BuyItemHubCommand(charId, "zone", "sword"), CancellationToken.None);

        result.Success.Should().BeTrue();
        // The inventory blob should contain exactly 1 slot with quantity 2
        var inv = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(saved!.InventoryBlob);
        inv.Should().HaveCount(1);
        inv![0]["Quantity"].GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Handle_PersistsCharacter_OnSuccess()
    {
        var charId   = Guid.NewGuid();
        var charRepo = new Mock<ICharacterRepository>();
        charRepo.Setup(r => r.GetByIdAsync(charId, It.IsAny<CancellationToken>()))
                .ReturnsAsync(MakeCharacter(charId));
        charRepo.Setup(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()))
                .Returns(Task.CompletedTask);

        var mediator = new Mock<ISender>();
        mediator.Setup(m => m.Send(It.IsAny<GetItemCatalogQuery>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(CatalogWith("potion", price: 10));

        await MakeHandler(charRepo.Object, mediator.Object)
            .Handle(new BuyItemHubCommand(charId, "zone", "potion"), CancellationToken.None);

        charRepo.Verify(r => r.UpdateAsync(It.IsAny<Character>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
