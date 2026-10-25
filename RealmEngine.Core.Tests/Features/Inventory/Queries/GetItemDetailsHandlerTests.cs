using FluentAssertions;
using RealmEngine.Core.Features.Inventory.Queries;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Inventory.Queries;

[Trait("Category", "Feature")]
/// <summary>
/// Tests for GetItemDetailsHandler.
/// </summary>
public class GetItemDetailsHandlerTests
{
    [Fact]
    public async Task Handle_Should_Return_All_Item_Details()
    {
        // Arrange
        var handler = new GetItemDetailsHandler();
        var item = new Item
        {
            Name = "Legendary Sword",
            Description = "A powerful weapon",
            Type = ItemType.Weapon,
            Rarity = ItemRarity.Legendary,
            Price = 1000,
            UpgradeLevel = 5
        };
        var query = new GetItemDetailsQuery { Item = item };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Name.Should().Be("Legendary Sword");
        result.Description.Should().Be("A powerful weapon");
        result.Type.Should().Be(ItemType.Weapon);
        result.Rarity.Should().Be(ItemRarity.Legendary);
        result.Price.Should().Be(1000);
        result.UpgradeLevel.Should().Be(5);
    }

    [Fact]
    public async Task Handle_Should_Return_Traits_Dictionary()
    {
        // Arrange
        var handler = new GetItemDetailsHandler();
        var item = new Item
        {
            Name = "Fire Sword",
            Traits = new Dictionary<string, TraitValue>
            {
                { "FireDamage", new TraitValue(10, TraitType.Number) },
                { "CriticalHit", new TraitValue(true, TraitType.Boolean) }
            }
        };
        var query = new GetItemDetailsQuery { Item = item };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Traits.Should().ContainKey("FireDamage");
        result.Traits.Should().ContainKey("CriticalHit");
        result.Traits.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_Should_Return_Empty_Traits_When_None()
    {
        // Arrange
        var handler = new GetItemDetailsHandler();
        var item = new Item { Name = "Basic Sword" };
        var query = new GetItemDetailsQuery { Item = item };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Traits.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_Should_Return_Enchantments()
    {
        // Arrange
        var handler = new GetItemDetailsHandler();
        var enchantment1 = new Enchantment
        {
            Name = "Strength Boost",
            Traits = new Dictionary<string, TraitValue> { { "Strength", new TraitValue(10, TraitType.Number) } }
        };
        var enchantment2 = new Enchantment
        {
            Name = "Dexterity Boost",
            Traits = new Dictionary<string, TraitValue> { { "Dexterity", new TraitValue(5, TraitType.Number) } }
        };
        var item = new Item
        {
            Name = "Enchanted Sword",
            Enchantments = new List<Enchantment> { enchantment1, enchantment2 }
        };
        var query = new GetItemDetailsQuery { Item = item };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.Enchantments.Should().HaveCount(2);
        result.Enchantments.Should().Contain(e => e.Name == "Strength Boost");
        result.Enchantments.Should().Contain(e => e.Name == "Dexterity Boost");
    }

    [Fact]
    public async Task Handle_Should_Return_SetName()
    {
        // Arrange
        var handler = new GetItemDetailsHandler();
        var item = new Item
        {
            Name = "Dragon Scale Helmet",
            SetName = "Dragon Scale Set"
        };
        var query = new GetItemDetailsQuery { Item = item };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SetName.Should().Be("Dragon Scale Set");
    }

    [Fact]
    public async Task Handle_Should_Handle_Null_SetName()
    {
        // Arrange
        var handler = new GetItemDetailsHandler();
        var item = new Item { Name = "Basic Sword", SetName = null };
        var query = new GetItemDetailsQuery { Item = item };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.SetName.Should().BeNull();
    }

    [Fact]
    public async Task Handle_Should_Return_Default_Values_For_New_Item()
    {
        // Arrange
        var handler = new GetItemDetailsHandler();
        var item = new Item();
        var query = new GetItemDetailsQuery { Item = item };

        // Act
        var result = await handler.Handle(query, CancellationToken.None);

        // Assert
        result.UpgradeLevel.Should().Be(0);
        result.Price.Should().Be(0);
        result.Traits.Should().BeEmpty();
        result.Enchantments.Should().BeEmpty();
    }
}
