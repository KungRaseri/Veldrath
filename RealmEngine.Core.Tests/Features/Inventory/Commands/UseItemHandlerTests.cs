using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Inventory.Commands;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Inventory.Commands;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for UseItemHandler.
/// </summary>
public class UseItemHandlerTests
{
    private readonly UseItemHandler _handler = new(NullLogger<UseItemHandler>.Instance);

    private static Character CreatePlayerWithItem(Item item, int health = 50, int maxHealth = 100, int mana = 50, int maxMana = 100)
    {
        var player = new Character
        {
            Health = health,
            MaxHealth = maxHealth,
            Mana = mana,
            MaxMana = maxMana
        };
        player.Inventory.Add(item);
        return player;
    }

    [Fact]
    public async Task Should_Return_Failure_When_Item_Is_Not_Consumable()
    {
        // Arrange
        var weapon = new Item { Name = "Iron Sword", Type = ItemType.Weapon };
        var player = CreatePlayerWithItem(weapon);
        var command = new UseItemCommand { Player = player, Item = weapon };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Iron Sword");
    }

    [Fact]
    public async Task Should_Return_Failure_When_Item_Is_Not_In_Inventory()
    {
        // Arrange
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable, Effect = "heal", Power = 30 };
        var player = new Character { Health = 50, MaxHealth = 100 };
        // Note: item is NOT added to inventory
        var command = new UseItemCommand { Player = player, Item = potion };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Health Potion");
        result.Message.Should().Contain("not in your inventory");
    }

    [Fact]
    public async Task Should_Restore_Health_When_Using_Heal_Item()
    {
        // Arrange
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable, Effect = "heal", Power = 30 };
        var player = CreatePlayerWithItem(potion, health: 50, maxHealth: 100);

        var command = new UseItemCommand { Player = player, Item = potion };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRestored.Should().Be(30);
        result.ManaRestored.Should().Be(0);
        player.Health.Should().Be(80);
    }

    [Fact]
    public async Task Should_Cap_Health_At_MaxHealth_When_Healing()
    {
        // Arrange
        var potion = new Item { Name = "Super Potion", Type = ItemType.Consumable, Effect = "heal", Power = 100 };
        var player = CreatePlayerWithItem(potion, health: 90, maxHealth: 100);

        var command = new UseItemCommand { Player = player, Item = potion };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Health.Should().Be(100);
        result.HealthRestored.Should().Be(10, "only restored up to MaxHealth");
    }

    [Fact]
    public async Task Should_Return_Zero_Health_Restored_When_Already_At_Max_Health()
    {
        // Arrange
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable, Effect = "heal", Power = 30 };
        var player = CreatePlayerWithItem(potion, health: 100, maxHealth: 100);

        var command = new UseItemCommand { Player = player, Item = potion };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue("item is still consumed even at full health");
        result.HealthRestored.Should().Be(0);
    }

    [Fact]
    public async Task Should_Restore_Mana_When_Using_Mana_Item()
    {
        // Arrange
        var manaPotion = new Item { Name = "Mana Potion", Type = ItemType.Consumable, Effect = "mana", Power = 25 };
        var player = CreatePlayerWithItem(manaPotion, mana: 20, maxMana: 100);

        var command = new UseItemCommand { Player = player, Item = manaPotion };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ManaRestored.Should().Be(25);
        result.HealthRestored.Should().Be(0);
        player.Mana.Should().Be(45);
    }

    [Fact]
    public async Task Should_Restore_Mana_Using_Restore_Effect()
    {
        // Arrange
        var elixir = new Item { Name = "Elixir", Type = ItemType.Consumable, Effect = "restore", Power = 40 };
        var player = CreatePlayerWithItem(elixir, mana: 10, maxMana: 100);

        var command = new UseItemCommand { Player = player, Item = elixir };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ManaRestored.Should().Be(40);
        player.Mana.Should().Be(50);
    }

    [Fact]
    public async Task Should_Fully_Restore_Health_And_Mana_When_Using_Full_Restore_Item()
    {
        // Arrange
        var elixir = new Item { Name = "Full Elixir", Type = ItemType.Consumable, Effect = "full_restore", Power = 0 };
        var player = CreatePlayerWithItem(elixir, health: 10, maxHealth: 100, mana: 5, maxMana: 80);

        var command = new UseItemCommand { Player = player, Item = elixir };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Health.Should().Be(100);
        player.Mana.Should().Be(80);
        result.HealthRestored.Should().Be(90);
        result.ManaRestored.Should().Be(75);
    }

    [Fact]
    public async Task Should_Remove_Item_From_Inventory_After_Use()
    {
        // Arrange
        var potion = new Item { Name = "Health Potion", Type = ItemType.Consumable, Effect = "heal", Power = 30 };
        var player = CreatePlayerWithItem(potion);
        player.Inventory.Should().ContainSingle();

        var command = new UseItemCommand { Player = player, Item = potion };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().BeEmpty("item should be consumed on use");
    }

    [Fact]
    public async Task Should_Only_Remove_One_Instance_Of_Item_When_Multiple_Copies_In_Inventory()
    {
        // Arrange - two identical potion instances (different object references)
        var potion1 = new Item { Name = "Health Potion", Type = ItemType.Consumable, Effect = "heal", Power = 30 };
        var potion2 = new Item { Name = "Health Potion", Type = ItemType.Consumable, Effect = "heal", Power = 30 };
        var player = new Character { Health = 50, MaxHealth = 100 };
        player.Inventory.Add(potion1);
        player.Inventory.Add(potion2);

        var command = new UseItemCommand { Player = player, Item = potion1 };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        player.Inventory.Should().ContainSingle("only the used potion should be removed");
        player.Inventory.Should().Contain(potion2);
    }

    [Fact]
    public async Task Should_Succeed_With_Unknown_Effect_And_Still_Consume_Item()
    {
        // Arrange - unknown effect falls through to default branch
        var mysteriousItem = new Item { Name = "Mystery Herb", Type = ItemType.Consumable, Effect = "unknown_effect", Power = 10 };
        var player = CreatePlayerWithItem(mysteriousItem);

        var command = new UseItemCommand { Player = player, Item = mysteriousItem };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue("item is consumed regardless of unknown effect");
        result.HealthRestored.Should().Be(0);
        result.ManaRestored.Should().Be(0);
        player.Inventory.Should().BeEmpty("item should still be removed from inventory");
    }

    [Theory]
    [InlineData("heal")]
    [InlineData("healing")]
    [InlineData("heal_potion")]
    [InlineData("restore_health")]
    public async Task Should_Restore_Health_For_All_Heal_Effect_Variants(string effect)
    {
        // Arrange
        var potion = new Item { Name = "Potion", Type = ItemType.Consumable, Effect = effect, Power = 20 };
        var player = CreatePlayerWithItem(potion, health: 50, maxHealth: 100);

        var command = new UseItemCommand { Player = player, Item = potion };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.HealthRestored.Should().Be(20);
    }

    [Theory]
    [InlineData("mana")]
    [InlineData("mana_potion")]
    [InlineData("restore_mana")]
    [InlineData("restore")]
    public async Task Should_Restore_Mana_For_All_Mana_Effect_Variants(string effect)
    {
        // Arrange
        var elixir = new Item { Name = "Elixir", Type = ItemType.Consumable, Effect = effect, Power = 20 };
        var player = CreatePlayerWithItem(elixir, mana: 30, maxMana: 100);

        var command = new UseItemCommand { Player = player, Item = elixir };

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.ManaRestored.Should().Be(20);
    }
}
