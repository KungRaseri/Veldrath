using FluentAssertions;
using Moq;
using RealmEngine.Shared.Models;
using RealmEngine.Core.Features.Inventory;
using Microsoft.Extensions.Logging.Abstractions;
using MediatR;
using System.Threading.Tasks;

namespace RealmEngine.Core.Tests.Features.Inventory;

[Trait("Category", "Service")]
/// <summary>
/// Tests for InventoryService.
/// </summary>
public class InventoryServiceTests
{
    private readonly Mock<IMediator> _mockMediator;

    public InventoryServiceTests()
    {
        _mockMediator = new Mock<IMediator>();
    }

    [Fact]
    public async Task AddItemAsync_Should_Add_Item_To_Inventory()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var item = new Item { Name = "Sword", Type = ItemType.Weapon };

        // Act
        var result = await service.AddItemAsync(item, "Hero");

        // Assert
        result.Should().BeTrue();
        service.Count.Should().Be(1);
        service.GetAllItems().Should().Contain(item);
    }

    [Fact]
    public async Task AddItemAsync_Should_Publish_ItemAcquired_Event()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var item = new Item { Name = "Potion", Type = ItemType.Consumable };

        // Act
        await service.AddItemAsync(item, "Hero");

        // Assert
        _mockMediator.Verify(m => m.Publish(
            It.Is<ItemAcquired>(e => e.PlayerName == "Hero" && e.ItemName == "Potion"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AddItemAsync_Should_Return_False_For_Null_Item()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);

        // Act
        var result = await service.AddItemAsync(null!, "Hero");

        // Assert
        result.Should().BeFalse();
        service.Count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveItem_Should_Remove_Item_By_Id()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var item = new Item { Id = "item-123", Name = "Sword", Type = ItemType.Weapon };
        await service.AddItemAsync(item, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        var result = service.RemoveItem("item-123");

        // Assert
        result.Should().BeTrue();
        service.Count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveItem_Should_Return_False_For_Nonexistent_Id()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);

        // Act
        var result = service.RemoveItem("nonexistent-id");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveItem_Should_Remove_Item_By_Reference()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var item = new Item { Name = "Helmet", Type = ItemType.Helmet };
        await service.AddItemAsync(item, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        var result = service.RemoveItem(item);

        // Assert
        result.Should().BeTrue();
        service.Count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveItem_Should_Return_False_For_Null_Item()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);

        // Act
        var result = service.RemoveItem((Item)null!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task GetItemsByType_Should_Filter_By_Type()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var weapon1 = new Item { Name = "Sword", Type = ItemType.Weapon };
        var weapon2 = new Item { Name = "Axe", Type = ItemType.Weapon };
        var armor = new Item { Name = "Shield", Type = ItemType.Shield };
        await service.AddItemAsync(weapon1, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(weapon2, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(armor, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        var weapons = service.GetItemsByType(ItemType.Weapon);

        // Assert
        weapons.Should().HaveCount(2);
        weapons.Should().Contain(weapon1);
        weapons.Should().Contain(weapon2);
        weapons.Should().NotContain(armor);
    }

    [Fact]
    public async Task GetItemsByRarity_Should_Filter_By_Rarity()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var common = new Item { Name = "Iron Sword", Rarity = ItemRarity.Common };
        var rare = new Item { Name = "Excalibur", Rarity = ItemRarity.Rare };
        var legendary = new Item { Name = "Dragon Blade", Rarity = ItemRarity.Legendary };
        await service.AddItemAsync(common, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(rare, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(legendary, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        var rares = service.GetItemsByRarity(ItemRarity.Rare);

        // Assert
        rares.Should().HaveCount(1);
        rares.Should().Contain(rare);
    }

    [Fact]
    public async Task FindItemById_Should_Return_Item_When_Found()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var item = new Item { Id = "unique-123", Name = "Magic Ring" };
        await service.AddItemAsync(item, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        var found = service.FindItemById("unique-123");

        // Assert
        found.Should().NotBeNull();
        found.Should().Be(item);
    }

    [Fact]
    public void FindItemById_Should_Return_Null_When_Not_Found()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);

        // Act
        var found = service.FindItemById("nonexistent");

        // Assert
        found.Should().BeNull();
    }

    [Fact]
    public async Task UseItemAsync_Should_Apply_Consumable_Effects()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var character = new Character { Name = "Hero", Health = 50, MaxHealth = 100 };
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable };
        await service.AddItemAsync(potion, "Hero");

        // Act
        var result = await service.UseItemAsync(potion, character, "Hero");

        // Assert
        result.Should().BeTrue();
        service.Count.Should().Be(0); // Item removed after use
        character.Health.Should().BeGreaterThan(50); // Health increased
    }

    [Fact]
    public async Task UseItemAsync_Should_Return_False_For_Non_Consumable()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var character = new Character { Name = "Hero", Health = 50, MaxHealth = 100 };
        var weapon = new Item { Name = "Sword", Type = ItemType.Weapon };
        await service.AddItemAsync(weapon, "Hero");

        // Act
        var result = await service.UseItemAsync(weapon, character, "Hero");

        // Assert
        result.Should().BeFalse();
        service.Count.Should().Be(1); // Item not removed
    }

    [Fact]
    public async Task UseItemAsync_Should_Return_False_For_Item_Not_In_Inventory()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var character = new Character { Name = "Hero", Health = 50, MaxHealth = 100 };
        var potion = new Item { Name = "Potion", Type = ItemType.Consumable };

        // Act
        var result = await service.UseItemAsync(potion, character, "Hero");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task HasItemOfType_Should_Return_True_When_Type_Exists()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        var weapon = new Item { Name = "Sword", Type = ItemType.Weapon };
        await service.AddItemAsync(weapon, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        var result = service.HasItemOfType(ItemType.Weapon);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasItemOfType_Should_Return_False_When_Type_Not_Exists()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);

        // Act
        var result = service.HasItemOfType(ItemType.Consumable);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task Clear_Should_Remove_All_Items()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        await service.AddItemAsync(new Item { Name = "Item1" }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Item2" }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Item3" }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        service.Clear();

        // Assert
        service.Count.Should().Be(0);
        service.GetAllItems().Should().BeEmpty();
    }

    [Fact]
    public async Task SortByName_Should_Order_Items_Alphabetically()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        await service.AddItemAsync(new Item { Name = "Zebra Sword" }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Alpha Shield" }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Beta Potion" }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        service.SortByName();

        // Assert
        var items = service.GetAllItems();
        items[0].Name.Should().Be("Alpha Shield");
        items[1].Name.Should().Be("Beta Potion");
        items[2].Name.Should().Be("Zebra Sword");
    }

    [Fact]
    public async Task SortByRarity_Should_Order_Items_By_Rarity()
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        await service.AddItemAsync(new Item { Name = "Common", Rarity = ItemRarity.Common }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Legendary", Rarity = ItemRarity.Legendary }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Rare", Rarity = ItemRarity.Rare }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        service.SortByRarity();

        // Assert - Sorted descending (Legendary first)
        var items = service.GetAllItems();
        items[0].Rarity.Should().Be(ItemRarity.Legendary);
        items[1].Rarity.Should().Be(ItemRarity.Rare);
        items[2].Rarity.Should().Be(ItemRarity.Common);
    }

    [Fact]
    public void Constructor_Should_Accept_Existing_Inventory()
    {
        // Arrange
        var existingItems = new List<Item>
        {
            new Item { Name = "Sword" },
            new Item { Name = "Shield" }
        };

        // Act
        var service = new InventoryService(_mockMediator.Object, existingItems, NullLogger<InventoryService>.Instance);

        // Assert
        service.Count.Should().Be(2);
        service.GetAllItems().Should().Contain(i => i.Name == "Sword");
        service.GetAllItems().Should().Contain(i => i.Name == "Shield");
    }

    [Theory]
    [InlineData(ItemType.Weapon, 2)]
    [InlineData(ItemType.Shield, 1)]
    [InlineData(ItemType.Consumable, 0)]
    public async Task GetItemsByType_Should_Return_Correct_Count(ItemType type, int expectedCount)
    {
        // Arrange
        var service = new InventoryService(_mockMediator.Object, NullLogger<InventoryService>.Instance);
        await service.AddItemAsync(new Item { Name = "Sword", Type = ItemType.Weapon }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Axe", Type = ItemType.Weapon }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));
        await service.AddItemAsync(new Item { Name = "Shield", Type = ItemType.Shield }, "Hero").WaitAsync(TimeSpan.FromMilliseconds(500));

        // Act
        var items = service.GetItemsByType(type);

        // Assert
        items.Should().HaveCount(expectedCount);
    }
}
