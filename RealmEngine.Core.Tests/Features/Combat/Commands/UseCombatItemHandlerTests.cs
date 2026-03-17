using FluentAssertions;
using RealmEngine.Core.Features.Combat.Commands.UseCombatItem;
using RealmEngine.Shared.Models;
using MediatR;
using Moq;

namespace RealmEngine.Core.Tests.Features.Combat.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for UseCombatItemHandler.
/// </summary>
public class UseCombatItemHandlerTests
{
    private readonly Mock<IMediator> _mockMediator;
    private readonly UseCombatItemHandler _handler;

    public UseCombatItemHandlerTests()
    {
        _mockMediator = new Mock<IMediator>();
        _handler = new UseCombatItemHandler(_mockMediator.Object, NullLogger<UseCombatItemHandler>.Instance);
    }

    [Fact]
    public async Task Handle_Should_Return_Failure_When_Item_Is_Not_Consumable()
    {
        // Arrange
        var player = new Character
        {
            Name = "Hero",
            Health = 50,
            MaxHealth = 100
        };
        var item = new Item
        {
            Name = "Sword",
            Type = ItemType.Weapon
        };
        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("cannot be used in combat");
    }

    [Theory]
    [InlineData(ItemRarity.Common, 25)]
    [InlineData(ItemRarity.Uncommon, 50)]
    [InlineData(ItemRarity.Rare, 75)]
    [InlineData(ItemRarity.Epic, 100)]
    [InlineData(ItemRarity.Legendary, 150)]
    public async Task Handle_Should_Heal_Based_On_Rarity(ItemRarity rarity, int expectedHeal)
    {
        // Arrange
        var player = new Character
        {
            Name = "Warrior",
            Health = 10,
            MaxHealth = 200
        };
        var item = new Item
        {
            Name = "Potion",
            Type = ItemType.Consumable,
            Rarity = rarity
        };
        player.Inventory.Add(item);

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRestored.Should().Be(expectedHeal);
        player.Health.Should().Be(10 + expectedHeal);
    }

    [Fact]
    public async Task Handle_Should_Not_Exceed_MaxHealth()
    {
        // Arrange
        var player = new Character
        {
            Name = "Mage",
            Health = 80,
            MaxHealth = 100
        };
        var item = new Item
        {
            Name = "Major Health Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Legendary // Heals 150
        };
        player.Inventory.Add(item);

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRestored.Should().Be(20); // Can only heal 20 to reach max 100
        player.Health.Should().Be(100);
    }

    [Fact]
    public async Task Handle_Should_Remove_Item_From_Inventory()
    {
        // Arrange
        var player = new Character
        {
            Name = "Rogue",
            Health = 50,
            MaxHealth = 100
        };
        var item = new Item
        {
            Name = "Small Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Common
        };
        player.Inventory.Add(item);

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        player.Inventory.Should().NotContain(item);
    }

    [Fact]
    public async Task Handle_Should_Add_Entry_To_CombatLog()
    {
        // Arrange
        var player = new Character
        {
            Name = "Knight",
            Health = 30,
            MaxHealth = 100
        };
        var item = new Item
        {
            Name = "Healing Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Uncommon
        };
        player.Inventory.Add(item);
        var combatLog = new CombatLog();

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item,
            CombatLog = combatLog
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        combatLog.Entries.Should().ContainSingle();
        combatLog.Entries.Should().Contain(e =>
            e.Message.Contains("Knight") &&
            e.Message.Contains("Healing Potion") &&
            e.Message.Contains("50"));
    }

    [Fact]
    public async Task Handle_Should_Not_Throw_When_CombatLog_Is_Null()
    {
        // Arrange
        var player = new Character
        {
            Name = "Paladin",
            Health = 40,
            MaxHealth = 100
        };
        var item = new Item
        {
            Name = "Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Common
        };
        player.Inventory.Add(item);

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item,
            CombatLog = null
        };

        // Act
        var act = async () => await _handler.Handle(command, CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_Should_Publish_ItemAcquired_Event()
    {
        // Arrange
        var player = new Character
        {
            Name = "Druid",
            Health = 60,
            MaxHealth = 100
        };
        var item = new Item
        {
            Name = "Super Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Rare
        };
        player.Inventory.Add(item);

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item
        };

        // Act
        await _handler.Handle(command, CancellationToken.None);

        // Assert
        _mockMediator.Verify(
            m => m.Publish(
                It.Is<ItemAcquired>(e => e.PlayerName == "Druid" && e.ItemName == "Super Potion"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_Should_Return_Success_Message()
    {
        // Arrange
        var player = new Character
        {
            Name = "Barbarian",
            Health = 20,
            MaxHealth = 150
        };
        var item = new Item
        {
            Name = "Epic Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Epic
        };
        player.Inventory.Add(item);

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Message.Should().Contain("Restored 100 health!");
    }

    [Fact]
    public async Task Handle_Should_Heal_Zero_When_Health_Already_Full()
    {
        // Arrange
        var player = new Character
        {
            Name = "Cleric",
            Health = 100,
            MaxHealth = 100
        };
        var item = new Item
        {
            Name = "Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Common
        };
        player.Inventory.Add(item);

        var command = new UseCombatItemCommand
        {
            Player = player,
            Item = item
        };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRestored.Should().Be(0);
        player.Health.Should().Be(100);
    }
}
