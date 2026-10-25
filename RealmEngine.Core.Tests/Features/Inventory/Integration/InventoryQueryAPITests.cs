using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.Inventory.Queries.CheckItemEquipped;
using RealmEngine.Core.Features.Inventory.Queries.GetEquippedItems;
using RealmEngine.Core.Features.Inventory.Queries.GetInventoryValue;
using RealmEngine.Core.Features.Inventory.Queries.GetPlayerInventory;
using RealmEngine.Core.Features.SaveLoad;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Features.Inventory.Integration;

/// <summary>
/// Integration tests for inventory query APIs.
/// </summary>
public class InventoryQueryAPITests
{
    private readonly ServiceProvider _serviceProvider;
    private readonly IMediator _mediator;
    private readonly SaveGameService _saveGameService;
    private readonly SaveGame _testSaveGame;

    public InventoryQueryAPITests()
    {
        var services = new ServiceCollection();

        // Mock repository and apocalypse timer
        var mockRepository = new Mock<ISaveGameRepository>();
        mockRepository.Setup(r => r.SaveGame(It.IsAny<SaveGame>())).Callback<SaveGame>(s => { });

        var mockApocalypseTimer = new Mock<IApocalypseTimer>();
        mockApocalypseTimer.Setup(t => t.GetBonusMinutes()).Returns(0);

        // Register services
        services.AddLogging();
        services.AddSingleton(mockRepository.Object);
        services.AddSingleton(mockApocalypseTimer.Object);
        services.AddSingleton<SaveGameService>();
        services.AddSingleton<ISaveGameService>(sp => sp.GetRequiredService<SaveGameService>());
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(GetPlayerInventoryQuery).Assembly));

        _serviceProvider = services.BuildServiceProvider();
        _mediator = _serviceProvider.GetRequiredService<IMediator>();
        _saveGameService = _serviceProvider.GetRequiredService<SaveGameService>();

        // Create test save game with inventory
        _testSaveGame = CreateTestSaveGame();
        _saveGameService.SetCurrentSave(_testSaveGame);
    }

    private static SaveGame CreateTestSaveGame()
    {
        var character = new Character
        {
            Name = "TestHero",
            Level = 10,
            Gold = 5000
        };

        // Create test items
        var sword = new Item { Id = "sword1", Name = "Iron Sword", Type = ItemType.Weapon, Rarity = ItemRarity.Common, Price = 100 };
        var helm = new Item { Id = "helm1", Name = "Steel Helm", Type = ItemType.Helmet, Rarity = ItemRarity.Uncommon, Price = 150 };
        var potion = new Item { Id = "potion1", Name = "Health Potion", Type = ItemType.Consumable, Rarity = ItemRarity.Common, Price = 25 };
        var ring = new Item { Id = "ring1", Name = "Gold Ring", Type = ItemType.Ring, Rarity = ItemRarity.Rare, Price = 500 };
        var legendary = new Item { Id = "legendary1", Name = "Excalibur", Type = ItemType.Weapon, Rarity = ItemRarity.Legendary, Price = 5000 };

        character.Inventory.AddRange(new[] { sword, helm, potion, ring, legendary });

        // Equip some items
        character.EquippedMainHand = sword;
        character.EquippedHelmet = helm;
        character.EquippedRing1 = ring;

        return new SaveGame
        {
            Character = character
        };
    }

    [Fact]
    public async Task Should_Get_All_Inventory_Items()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().NotBeNull();
        result.Items!.Count.Should().Be(5);
        result.Summary.Should().NotBeNull();
        result.Summary!.TotalItems.Should().Be(5);
    }

    [Fact]
    public async Task Should_Filter_Inventory_By_Item_Type()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery
        {
            ItemTypeFilter = "Weapon"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().NotBeNull();
        result.Items!.Count.Should().Be(2); // Sword and Excalibur
        result.Items.Should().AllSatisfy(item => item.ItemType.Should().Be("Weapon"));
    }

    [Fact]
    public async Task Should_Filter_Inventory_By_Rarity()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery
        {
            RarityFilter = "Common"
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().NotBeNull();
        result.Items!.Count.Should().Be(2); // Sword and Potion
        result.Items.Should().AllSatisfy(item => item.Rarity.Should().Be("Common"));
    }

    [Fact]
    public async Task Should_Filter_Inventory_By_Value_Range()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery
        {
            MinValue = 100,
            MaxValue = 500
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().NotBeNull();
        result.Items!.Count.Should().Be(3); // Sword (100), Helm (150), Ring (500)
        result.Items.Should().AllSatisfy(item =>
        {
            item.Value.Should().BeGreaterThanOrEqualTo(100);
            item.Value.Should().BeLessThanOrEqualTo(500);
        });
    }

    [Fact]
    public async Task Should_Sort_Inventory_By_Value_Descending()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery
        {
            SortBy = "value",
            SortDescending = true
        });

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().NotBeNull();
        result.Items!.First().Name.Should().Be("Excalibur"); // Most expensive
        result.Items.Last().Name.Should().Be("Health Potion"); // Least expensive
    }

    [Fact]
    public async Task Should_Include_Equipped_Status_In_Inventory()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.Items.Should().NotBeNull();

        var sword = result.Items!.First(i => i.Name == "Iron Sword");
        sword.IsEquipped.Should().BeTrue();

        var potion = result.Items!.First(i => i.Name == "Health Potion");
        potion.IsEquipped.Should().BeFalse();
    }

    [Fact]
    public async Task Should_Get_All_Equipped_Items()
    {
        // Act
        var result = await _mediator.Send(new GetEquippedItemsQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.Equipment.Should().NotBeNull();
        result.Equipment!.MainHand.Should().NotBeNull();
        result.Equipment.MainHand!.Name.Should().Be("Iron Sword");
        result.Equipment.Helm.Should().NotBeNull();
        result.Equipment.Helm!.Name.Should().Be("Steel Helm");
        result.Equipment.Ring1.Should().NotBeNull();
        result.Equipment.Ring1!.Name.Should().Be("Gold Ring");
    }

    [Fact]
    public async Task Should_Calculate_Equipment_Stats()
    {
        // Act
        var result = await _mediator.Send(new GetEquippedItemsQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.Stats.Should().NotBeNull();
        result.Stats!.TotalEquippedItems.Should().Be(3); // Sword, Helm, Ring
        result.Stats.TotalValue.Should().Be(750); // 100 + 150 + 500
    }

    [Fact]
    public async Task Should_Check_If_Item_Is_Equipped()
    {
        // Act
        var equippedResult = await _mediator.Send(new CheckItemEquippedQuery { ItemId = "sword1" });
        var unequippedResult = await _mediator.Send(new CheckItemEquippedQuery { ItemId = "potion1" });

        // Assert
        equippedResult.Success.Should().BeTrue();
        equippedResult.IsEquipped.Should().BeTrue();
        equippedResult.EquipSlot.Should().Be("MainHand");

        unequippedResult.Success.Should().BeTrue();
        unequippedResult.IsEquipped.Should().BeFalse();
        unequippedResult.EquipSlot.Should().BeNull();
    }

    [Fact]
    public async Task Should_Calculate_Total_Inventory_Value()
    {
        // Act
        var result = await _mediator.Send(new GetInventoryValueQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.TotalValue.Should().Be(5775); // Sum of all items
        result.EquippedValue.Should().Be(750); // Sword + Helm + Ring
        result.UnequippedValue.Should().Be(5025); // Potion + Excalibur
    }

    [Fact]
    public async Task Should_Identify_Most_Valuable_Item()
    {
        // Act
        var result = await _mediator.Send(new GetInventoryValueQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.MostValuableItemName.Should().Be("Excalibur");
        result.MostValuableItemPrice.Should().Be(5000);
    }

    [Fact]
    public async Task Should_Determine_Wealth_Category()
    {
        // Act
        var result = await _mediator.Send(new GetInventoryValueQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.WealthCategory.Should().Be(WealthCategory.Wealthy); // 5775g total
    }

    [Fact]
    public async Task Should_Exclude_Equipped_Items_From_Value_When_Requested()
    {
        // Act
        var result = await _mediator.Send(new GetInventoryValueQuery
        {
            IncludeEquipped = false
        });

        // Assert
        result.Success.Should().BeTrue();
        result.TotalValue.Should().Be(5025); // Only unequipped items
    }

    [Fact]
    public async Task Should_Generate_Inventory_Summary_By_Type()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.Summary.Should().NotBeNull();
        result.Summary!.ItemsByType.Should().ContainKey("Weapon");
        result.Summary.ItemsByType["Weapon"].Should().Be(2);
        result.Summary.ItemsByType["Helmet"].Should().Be(1);
        result.Summary.ItemsByType["Consumable"].Should().Be(1);
        result.Summary.ItemsByType["Ring"].Should().Be(1);
    }

    [Fact]
    public async Task Should_Generate_Inventory_Summary_By_Rarity()
    {
        // Act
        var result = await _mediator.Send(new GetPlayerInventoryQuery());

        // Assert
        result.Success.Should().BeTrue();
        result.Summary.Should().NotBeNull();
        result.Summary!.ItemsByRarity.Should().ContainKey("Common");
        result.Summary.ItemsByRarity["Common"].Should().Be(2);
        result.Summary.ItemsByRarity["Uncommon"].Should().Be(1);
        result.Summary.ItemsByRarity["Rare"].Should().Be(1);
        result.Summary.ItemsByRarity["Legendary"].Should().Be(1);
    }
}
