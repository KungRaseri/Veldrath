using System.Threading.Tasks;
using MediatR;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Features.Inventory;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Inventory.Services;

/// <summary>
/// Complements <c>InventoryServiceTests</c> with the stacking, consumable-effect and sorting
/// branches: stack merging, mana vs health vs unknown potions, null arguments, type/value sorting.
/// </summary>
[Trait("Category", "Service")]
public class InventoryServiceBehaviourTests
{
    private static InventoryService CreateService(out Mock<IMediator> mediator)
    {
        mediator = new Mock<IMediator>();
        return new InventoryService(mediator.Object, NullLogger<InventoryService>.Instance);
    }

    private static Item MakePotion(string name, ItemRarity rarity = ItemRarity.Common) => new()
    {
        Id = Guid.NewGuid().ToString(),
        Name = name,
        Type = ItemType.Consumable,
        Rarity = rarity
    };

    [Fact]
    public async Task AddItemAsync_MergesStackableItemsOfTheSameNameAndType()
    {
        var service = CreateService(out var mediator);
        var first = MakePotion("Healing Draught");
        first.IsStackable = true;
        first.Quantity = 3;
        var second = MakePotion("Healing Draught");
        second.IsStackable = true;
        second.Quantity = 2;

        await service.AddItemAsync(first, "Hero");
        var result = await service.AddItemAsync(second, "Hero");

        result.Should().BeTrue();
        service.Count.Should().Be(1, "the second stack merges into the first");
        service.GetAllItems()[0].Quantity.Should().Be(5);
        mediator.Verify(m => m.Publish(It.IsAny<ItemAcquired>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task AddItemAsync_AddsASeparateEntry_WhenStackNamesDiffer()
    {
        var service = CreateService(out _);
        var first = MakePotion("Healing Draught");
        first.IsStackable = true;
        first.Quantity = 1;
        var second = MakePotion("Mana Draught");
        second.IsStackable = true;
        second.Quantity = 1;

        await service.AddItemAsync(first, "Hero");
        await service.AddItemAsync(second, "Hero");

        service.Count.Should().Be(2);
    }

    [Fact]
    public async Task UseItemAsync_ReturnsFalse_ForNullItemOrCharacter()
    {
        var service = CreateService(out _);
        var item = MakePotion("Health Potion");
        await service.AddItemAsync(item, "Hero");

        (await service.UseItemAsync(null!, new Character { Name = "Hero" }, "Hero")).Should().BeFalse();
        (await service.UseItemAsync(item, null!, "Hero")).Should().BeFalse();
        service.Count.Should().Be(1, "invalid calls must not consume the item");
    }

    [Theory]
    [InlineData(ItemRarity.Common, 20)]
    [InlineData(ItemRarity.Uncommon, 35)]
    [InlineData(ItemRarity.Rare, 50)]
    [InlineData(ItemRarity.Epic, 75)]
    [InlineData(ItemRarity.Legendary, 100)]
    public async Task UseItemAsync_RestoresMana_ByRarity(ItemRarity rarity, int expectedMana)
    {
        var service = CreateService(out _);
        var potion = MakePotion("Mana Potion", rarity);
        var character = new Character { Name = "Hero", Mana = 0, MaxMana = 500 };
        await service.AddItemAsync(potion, "Hero");

        var result = await service.UseItemAsync(potion, character, "Hero");

        result.Should().BeTrue();
        character.Mana.Should().Be(expectedMana);
        service.Count.Should().Be(0);
    }

    [Theory]
    [InlineData(ItemRarity.Common, 30)]
    [InlineData(ItemRarity.Uncommon, 50)]
    [InlineData(ItemRarity.Rare, 75)]
    [InlineData(ItemRarity.Epic, 100)]
    [InlineData(ItemRarity.Legendary, 150)]
    public async Task UseItemAsync_Heals_ByRarity(ItemRarity rarity, int expectedHeal)
    {
        var service = CreateService(out _);
        var potion = MakePotion("Health Potion", rarity);
        var character = new Character { Name = "Hero", Health = 0, MaxHealth = 500 };
        await service.AddItemAsync(potion, "Hero");

        await service.UseItemAsync(potion, character, "Hero");

        character.Health.Should().Be(expectedHeal);
    }

    [Fact]
    public async Task UseItemAsync_ClampsHealingAtMaxHealth()
    {
        var service = CreateService(out _);
        var potion = MakePotion("Health Potion", ItemRarity.Legendary);
        var character = new Character { Name = "Hero", Health = 99, MaxHealth = 100 };
        await service.AddItemAsync(potion, "Hero");

        await service.UseItemAsync(potion, character, "Hero");

        character.Health.Should().Be(100);
    }

    [Fact]
    public async Task UseItemAsync_AppliesDefaultEffect_ForUnrecognisedConsumables()
    {
        var service = CreateService(out _);
        var potion = MakePotion("Mysterious Berry");
        var character = new Character { Name = "Hero", Health = 50, MaxHealth = 100 };
        await service.AddItemAsync(potion, "Hero");

        await service.UseItemAsync(potion, character, "Hero");

        character.Health.Should().Be(60, "unknown consumables restore a flat 10 HP");
    }

    [Fact]
    public async Task SortByType_OrdersItemsByItemTypeEnum()
    {
        var service = CreateService(out _);
        await service.AddItemAsync(new Item { Name = "Sword", Type = ItemType.Consumable }, "Hero");
        await service.AddItemAsync(new Item { Name = "Axe", Type = ItemType.Weapon }, "Hero");

        service.SortByType();

        service.GetAllItems().Select(i => i.Type).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task SortByValue_OrdersMostExpensiveFirst()
    {
        var service = CreateService(out _);
        await service.AddItemAsync(new Item { Name = "Cheap", Price = 5 }, "Hero");
        await service.AddItemAsync(new Item { Name = "Priceless", Price = 500 }, "Hero");
        await service.AddItemAsync(new Item { Name = "Middling", Price = 50 }, "Hero");

        service.SortByValue();

        service.GetAllItems().Select(i => i.Name).Should().ContainInOrder("Priceless", "Middling", "Cheap");
    }

    [Fact]
    public async Task GetTotalValue_SumsItemPrices()
    {
        var service = CreateService(out _);
        await service.AddItemAsync(new Item { Name = "A", Price = 10 }, "Hero");
        await service.AddItemAsync(new Item { Name = "B", Price = 32 }, "Hero");

        service.GetTotalValue().Should().Be(42);
    }

    [Fact]
    public void Constructor_WithNullInventory_StartsEmpty()
    {
        var service = new InventoryService(new Mock<IMediator>().Object, null!, NullLogger<InventoryService>.Instance);

        service.Count.Should().Be(0);
    }
}
