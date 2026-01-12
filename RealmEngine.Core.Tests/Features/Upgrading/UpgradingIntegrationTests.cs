using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core.Features.Upgrading.Commands;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Upgrading;

/// <summary>
/// Integration tests for the item upgrade system.
/// Tests safe upgrades (+1 to +5) and risky upgrades (+6 to +10).
/// </summary>
public class UpgradingIntegrationTests
{
    private readonly IMediator _mediator;

    public UpgradingIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(UpgradeItemHandler).Assembly));
        
        var serviceProvider = services.BuildServiceProvider();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task UpgradeItem_Level0To1_AlwaysSucceeds()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Common);
        item.UpgradeLevel = 0;
        var essences = CreateEssences("Weapon", "Minor", 1);

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessRate.Should().Be(100.0);
        result.EssencesConsumed.Should().BeTrue();
        result.NewUpgradeLevel.Should().Be(1);
        result.OldUpgradeLevel.Should().Be(0);
        result.StatMultiplier.Should().BeApproximately(1.11, 0.01); // 1 + 0.1 + 0.01
        item.UpgradeLevel.Should().Be(1);
    }

    [Fact]
    public async Task UpgradeItem_Level4To5_AlwaysSucceeds()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Common);
        item.UpgradeLevel = 4;
        var essences = CreateEssences("Weapon", "Greater", 2);

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessRate.Should().Be(100.0); // Safe zone
        result.NewUpgradeLevel.Should().Be(5);
        result.StatMultiplier.Should().BeApproximately(1.75, 0.01); // 1 + 0.5 + 0.25
        item.UpgradeLevel.Should().Be(5);
    }

    [Fact]
    public async Task UpgradeItem_Level5To6_RiskyUpgrade95Percent()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Rare);
        item.UpgradeLevel = 5;
        var essences = CreateEssences("Weapon", "Greater", 3);

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.SuccessRate.Should().Be(95.0);
        result.EssencesConsumed.Should().BeTrue();
        // Success depends on RNG, but rate should be 95%
    }

    [Fact]
    public async Task UpgradeItem_Level9To10_RiskyUpgrade50Percent()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Legendary);
        item.UpgradeLevel = 9;
        var essences = CreateEssences("Weapon", "Perfect", 1)
            .Concat(CreateEssences("Weapon", "Superior", 3))
            .ToList();

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.SuccessRate.Should().Be(50.0);
        result.EssencesConsumed.Should().BeTrue();
        result.OldUpgradeLevel.Should().Be(9);
        // New level is either 10 (success) or 8 (failure)
    }

    [Fact]
    public async Task UpgradeItem_AlreadyAtMax_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Common);
        item.UpgradeLevel = 5; // Common max
        var essences = CreateEssences("Weapon", "Greater", 3);

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.EssencesConsumed.Should().BeFalse();
        result.Message.Should().Contain("already at maximum upgrade level (+5)");
        item.UpgradeLevel.Should().Be(5);
    }

    [Fact]
    public async Task UpgradeItem_WrongEssenceType_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Common);
        item.UpgradeLevel = 0;
        var essences = CreateEssences("Armor", "Minor", 1); // Wrong type!

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.EssencesConsumed.Should().BeFalse();
        result.Message.Should().Contain("Requires Weapon Essence");
        item.UpgradeLevel.Should().Be(0);
    }

    [Fact]
    public async Task UpgradeItem_InsufficientEssences_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Common);
        item.UpgradeLevel = 2;
        var essences = CreateEssences("Weapon", "Minor", 1); // Need 3 for +3

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.EssencesConsumed.Should().BeFalse();
        result.Message.Should().Contain("Insufficient essences");
        item.UpgradeLevel.Should().Be(2);
    }

    [Fact]
    public async Task UpgradeItem_ArmorPiece_UsesArmorEssence()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestArmor(ItemRarity.Common);
        item.UpgradeLevel = 0;
        var essences = CreateEssences("Armor", "Minor", 1);

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.NewUpgradeLevel.Should().Be(1);
        item.UpgradeLevel.Should().Be(1);
    }

    [Fact]
    public async Task UpgradeItem_Jewelry_UsesAccessoryEssence()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestRing(ItemRarity.Rare);
        item.UpgradeLevel = 0;
        var essences = CreateEssences("Accessory", "Minor", 1);

        var command = new UpgradeItemCommand
        {
            Character = character,
            Item = item,
            Essences = essences
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.NewUpgradeLevel.Should().Be(1);
        item.UpgradeLevel.Should().Be(1);
    }

    [Fact]
    public async Task UpgradeItem_StatMultiplier_CalculatedCorrectly()
    {
        // Arrange
        var character = CreateTestCharacter();
        var item = CreateTestWeapon(ItemRarity.Epic);
        
        // Test multiple levels - formula: base + (level * 2)
        var testCases = new Dictionary<int, double>
        {
            { 1, 22.0 },   // 20 + 2*1 = 22
            { 5, 30.0 },   // 20 + 2*5 = 30
            { 8, 36.0 },   // 20 + 2*8 = 36
            { 10, 40.0 }   // 20 + 2*10 = 40
        };

        foreach (var (level, expectedDamage) in testCases)
        {
            // Set level manually for testing
            item.UpgradeLevel = level;
            
            // Calculate using GetTotalTraits (which applies the bonus)
            var baseDamage = 20.0;
            item.Traits["Damage"] = new TraitValue(baseDamage, TraitType.Number);
            var totalTraits = item.GetTotalTraits();
            var actualDamage = totalTraits["Damage"].AsDouble();
            
            actualDamage.Should().BeApproximately(expectedDamage, 0.01);
        }
    }

    [Fact]
    public async Task UpgradeItem_RarityMaxLevels_EnforcedCorrectly()
    {
        // Arrange
        var character = CreateTestCharacter();
        
        // Test max levels for each rarity
        var testCases = new Dictionary<ItemRarity, int>
        {
            { ItemRarity.Common, 5 },
            { ItemRarity.Uncommon, 5 },
            { ItemRarity.Rare, 7 },
            { ItemRarity.Epic, 9 },
            { ItemRarity.Legendary, 10 }
        };

        foreach (var (rarity, maxLevel) in testCases)
        {
            var item = CreateTestWeapon(rarity);
            item.GetMaxUpgradeLevel().Should().Be(maxLevel);
        }
    }

    // Helper methods
    private Character CreateTestCharacter()
    {
        return new Character
        {
            Name = "Test Character",
            Skills = new Dictionary<string, CharacterSkill>()
        };
    }

    private Item CreateTestWeapon(ItemRarity rarity)
    {
        return new Item
        {
            Name = $"Test {rarity} Sword",
            Rarity = rarity,
            Type = ItemType.Weapon,
            Traits = new Dictionary<string, TraitValue>
            {
                ["Damage"] = new TraitValue(20, TraitType.Number)
            }
        };
    }

    private Item CreateTestArmor(ItemRarity rarity)
    {
        return new Item
        {
            Name = $"Test {rarity} Chestplate",
            Rarity = rarity,
            Type = ItemType.Chest,
            Traits = new Dictionary<string, TraitValue>
            {
                ["Defense"] = new TraitValue(15, TraitType.Number)
            }
        };
    }

    private Item CreateTestRing(ItemRarity rarity)
    {
        return new Item
        {
            Name = $"Test {rarity} Ring",
            Rarity = rarity,
            Type = ItemType.Ring,
            Traits = new Dictionary<string, TraitValue>
            {
                ["Intelligence"] = new TraitValue(5, TraitType.Number)
            }
        };
    }

    private List<Item> CreateEssences(string type, string tier, int count)
    {
        var essences = new List<Item>();
        for (int i = 0; i < count; i++)
        {
            essences.Add(new Item
            {
                Name = $"{tier} {type} Essence",
                Type = ItemType.Material
            });
        }
        return essences;
    }
}
