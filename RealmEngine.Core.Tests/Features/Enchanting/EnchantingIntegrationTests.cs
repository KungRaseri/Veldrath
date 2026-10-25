using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core.Features.Enchanting.Commands;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Enchanting;

/// <summary>
/// Integration tests for the enchanting system.
/// Tests apply, add slot, and remove enchantment commands.
/// </summary>
public class EnchantingIntegrationTests
{
    private readonly IMediator _mediator;

    public EnchantingIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(ApplyEnchantmentHandler).Assembly));

        var serviceProvider = services.BuildServiceProvider();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task ApplyEnchantment_FirstSlot_AlwaysSucceeds()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 0);
        var item = CreateTestItem(ItemRarity.Common);
        item.MaxPlayerEnchantments = 1;
        var scroll = CreateEnchantmentScroll("Fire Damage", 5);

        var command = new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = scroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.SuccessRate.Should().Be(100.0);
        result.ScrollConsumed.Should().BeTrue();
        result.AppliedEnchantment.Should().NotBeNull();
        item.PlayerEnchantments.Should().HaveCount(1);
        item.PlayerEnchantments[0].Name.Should().Be("Fire Damage");
    }

    [Fact]
    public async Task ApplyEnchantment_SecondSlot_BaseRate75Percent()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 0);
        var item = CreateTestItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 2;

        // Apply first enchantment manually
        item.PlayerEnchantments.Add(CreateEnchantment("Frost Damage", 3));

        var scroll = CreateEnchantmentScroll("Fire Damage", 5);

        var command = new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = scroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.SuccessRate.Should().Be(75.0);
        result.ScrollConsumed.Should().BeTrue();
        // Success/failure depends on RNG, but scroll is always consumed
    }

    [Fact]
    public async Task ApplyEnchantment_SecondSlot_WithHighSkill_IncreasedRate()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 50);
        var item = CreateTestItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 2;
        item.PlayerEnchantments.Add(CreateEnchantment("Frost Damage", 3));

        var scroll = CreateEnchantmentScroll("Fire Damage", 5);

        var command = new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = scroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        // 75% base + (50 * 0.3%) = 75% + 15% = 90%
        result.SuccessRate.Should().Be(90.0);
        result.ScrollConsumed.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyEnchantment_ThirdSlot_BaseRate50Percent()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 0);
        var item = CreateTestItem(ItemRarity.Legendary);
        item.MaxPlayerEnchantments = 3;
        item.PlayerEnchantments.Add(CreateEnchantment("Frost Damage", 3));
        item.PlayerEnchantments.Add(CreateEnchantment("Lightning Damage", 4));

        var scroll = CreateEnchantmentScroll("Fire Damage", 5);

        var command = new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = scroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.SuccessRate.Should().Be(50.0);
        result.ScrollConsumed.Should().BeTrue();
    }

    [Fact]
    public async Task ApplyEnchantment_ThirdSlot_WithMaxSkill_CappedAt100Percent()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 200); // Very high skill
        var item = CreateTestItem(ItemRarity.Legendary);
        item.MaxPlayerEnchantments = 3;
        item.PlayerEnchantments.Add(CreateEnchantment("Frost Damage", 3));
        item.PlayerEnchantments.Add(CreateEnchantment("Lightning Damage", 4));

        var scroll = CreateEnchantmentScroll("Fire Damage", 5);

        var command = new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = scroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        // 50% base + (200 * 0.3%) = 50% + 60% = 110%, capped at 100%
        result.SuccessRate.Should().Be(100.0);
        result.ScrollConsumed.Should().BeTrue();
        result.Success.Should().BeTrue(); // Should always succeed at 100%
    }

    [Fact]
    public async Task ApplyEnchantment_NoSlotsAvailable_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 50);
        var item = CreateTestItem(ItemRarity.Common);
        item.MaxPlayerEnchantments = 1;
        item.PlayerEnchantments.Add(CreateEnchantment("Existing Enchantment", 5));

        var scroll = CreateEnchantmentScroll("Fire Damage", 5);

        var command = new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = scroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ScrollConsumed.Should().BeFalse();
        result.Message.Should().Contain("no available enchantment slots");
    }

    [Fact]
    public async Task ApplyEnchantment_InvalidScroll_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 50);
        var item = CreateTestItem(ItemRarity.Common);
        item.MaxPlayerEnchantments = 1;

        var scroll = new Item { Name = "Empty Scroll" }; // No enchantment

        var command = new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = scroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ScrollConsumed.Should().BeFalse();
        result.Message.Should().Contain("does not contain a valid enchantment");
    }

    [Fact]
    public async Task AddEnchantmentSlot_CommonItem_CannotAddSecondSlot()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 50);
        var item = CreateTestItem(ItemRarity.Common);
        item.MaxPlayerEnchantments = 1;

        var crystal = new Item { Name = "Socket Crystal" };

        var command = new AddEnchantmentSlotCommand
        {
            Character = character,
            Item = item,
            SocketCrystal = crystal
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.CrystalConsumed.Should().BeFalse();
        result.Message.Should().Contain("already has maximum enchantment slots");
        item.MaxPlayerEnchantments.Should().Be(1);
    }

    [Fact]
    public async Task AddEnchantmentSlot_RareItem_CanAddSecondSlot()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 50);
        var item = CreateTestItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 1;

        var crystal = new Item { Name = "Socket Crystal" };

        var command = new AddEnchantmentSlotCommand
        {
            Character = character,
            Item = item,
            SocketCrystal = crystal
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.CrystalConsumed.Should().BeTrue();
        result.NewMaxSlots.Should().Be(2);
        result.Message.Should().Contain("Successfully added enchantment slot 2");
        item.MaxPlayerEnchantments.Should().Be(2);
    }

    [Fact]
    public async Task AddEnchantmentSlot_InsufficientSkill_ReturnsError()
    {
        // Arrange - Need skill 25 for second slot
        var character = CreateTestCharacter(enchantingSkill: 10);
        var item = CreateTestItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 1;

        var crystal = new Item { Name = "Socket Crystal" };

        var command = new AddEnchantmentSlotCommand
        {
            Character = character,
            Item = item,
            SocketCrystal = crystal
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.CrystalConsumed.Should().BeFalse();
        result.Message.Should().Contain("Requires Enchanting skill 25");
        item.MaxPlayerEnchantments.Should().Be(1);
    }

    [Fact]
    public async Task AddEnchantmentSlot_LegendaryItem_CanAddThirdSlot()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 50);
        var item = CreateTestItem(ItemRarity.Legendary);
        item.MaxPlayerEnchantments = 2;

        var crystal = new Item { Name = "Socket Crystal" };

        var command = new AddEnchantmentSlotCommand
        {
            Character = character,
            Item = item,
            SocketCrystal = crystal
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.CrystalConsumed.Should().BeTrue();
        result.NewMaxSlots.Should().Be(3);
        item.MaxPlayerEnchantments.Should().Be(3);
    }

    [Fact]
    public async Task AddEnchantmentSlot_AlreadyAtHardCap_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 100);
        var item = CreateTestItem(ItemRarity.Legendary);
        item.MaxPlayerEnchantments = 3; // Already at max

        var crystal = new Item { Name = "Socket Crystal" };

        var command = new AddEnchantmentSlotCommand
        {
            Character = character,
            Item = item,
            SocketCrystal = crystal
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.CrystalConsumed.Should().BeFalse();
        result.Message.Should().Contain("maximum possible enchantment slots (3)");
    }

    [Fact]
    public async Task RemoveEnchantment_ValidIndex_RemovesEnchantment()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 0);
        var item = CreateTestItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 2;
        item.PlayerEnchantments.Add(CreateEnchantment("Fire Damage", 5));
        item.PlayerEnchantments.Add(CreateEnchantment("Frost Damage", 3));

        var removalScroll = new Item { Name = "Enchantment Removal Scroll" };

        var command = new RemoveEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentIndex = 0, // Remove first enchantment
            RemovalScroll = removalScroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.ScrollConsumed.Should().BeTrue();
        result.RemovedEnchantment.Should().NotBeNull();
        result.RemovedEnchantment!.Name.Should().Be("Fire Damage");
        item.PlayerEnchantments.Should().HaveCount(1);
        item.PlayerEnchantments[0].Name.Should().Be("Frost Damage");
    }

    [Fact]
    public async Task RemoveEnchantment_NoEnchantments_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 0);
        var item = CreateTestItem(ItemRarity.Common);
        item.MaxPlayerEnchantments = 1;

        var removalScroll = new Item { Name = "Enchantment Removal Scroll" };

        var command = new RemoveEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentIndex = 0,
            RemovalScroll = removalScroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ScrollConsumed.Should().BeFalse();
        result.Message.Should().Contain("has no player-applied enchantments");
    }

    [Fact]
    public async Task RemoveEnchantment_InvalidIndex_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 0);
        var item = CreateTestItem(ItemRarity.Common);
        item.MaxPlayerEnchantments = 1;
        item.PlayerEnchantments.Add(CreateEnchantment("Fire Damage", 5));

        var removalScroll = new Item { Name = "Enchantment Removal Scroll" };

        var command = new RemoveEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentIndex = 5, // Invalid index
            RemovalScroll = removalScroll
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ScrollConsumed.Should().BeFalse();
        result.Message.Should().Contain("Invalid enchantment index");
    }

    [Fact]
    public async Task EnchantingWorkflow_CompleteFlow_WorksCorrectly()
    {
        // Arrange
        var character = CreateTestCharacter(enchantingSkill: 50);
        var item = CreateTestItem(ItemRarity.Rare);
        item.MaxPlayerEnchantments = 1;

        // Step 1: Add a second slot
        var addSlotResult = await _mediator.Send(new AddEnchantmentSlotCommand
        {
            Character = character,
            Item = item,
            SocketCrystal = new Item { Name = "Socket Crystal" }
        });

        addSlotResult.Success.Should().BeTrue();
        item.MaxPlayerEnchantments.Should().Be(2);

        // Step 2: Apply first enchantment
        var firstEnchantResult = await _mediator.Send(new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = CreateEnchantmentScroll("Fire Damage", 5)
        });

        firstEnchantResult.Success.Should().BeTrue();
        item.PlayerEnchantments.Should().HaveCount(1);

        // Step 3: Apply second enchantment (75% + 15% = 90% success rate)
        var secondEnchantResult = await _mediator.Send(new ApplyEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentScroll = CreateEnchantmentScroll("Frost Damage", 3)
        });

        secondEnchantResult.SuccessRate.Should().Be(90.0);

        // Step 4: Remove first enchantment
        var removeResult = await _mediator.Send(new RemoveEnchantmentCommand
        {
            Character = character,
            Item = item,
            EnchantmentIndex = 0,
            RemovalScroll = new Item { Name = "Removal Scroll" }
        });

        removeResult.Success.Should().BeTrue();
        // Should have 0 or 1 enchantment depending on second application success
    }

    // Helper methods
    private Character CreateTestCharacter(int enchantingSkill)
    {
        var character = new Character
        {
            Name = "Test Character",
            Skills = new Dictionary<string, CharacterSkill>()
        };

        if (enchantingSkill > 0)
        {
            character.Skills["Enchanting"] = new CharacterSkill
            {
                SkillId = "enchanting",
                Name = "Enchanting",
                Category = "Crafting",
                CurrentRank = enchantingSkill,
                CurrentXP = 0,
                XPToNextRank = 100
            };
        }

        return character;
    }

    private Item CreateTestItem(ItemRarity rarity)
    {
        return new Item
        {
            Name = $"Test {rarity} Item",
            Rarity = rarity,
            Type = ItemType.Weapon,
            Traits = new Dictionary<string, TraitValue>
            {
                ["Damage"] = new TraitValue(20, TraitType.Number)
            }
        };
    }

    private Item CreateEnchantmentScroll(string enchantmentName, int damageBonus)
    {
        var enchantment = CreateEnchantment(enchantmentName, damageBonus);

        return new Item
        {
            Name = $"{enchantmentName} Scroll",
            Type = ItemType.Consumable,
            Enchantments = new List<Enchantment> { enchantment }
        };
    }

    private Enchantment CreateEnchantment(string name, int damageBonus)
    {
        return new Enchantment
        {
            Name = name,
            Description = $"Adds {damageBonus} {name}",
            Rarity = EnchantmentRarity.Lesser,
            Traits = new Dictionary<string, TraitValue>
            {
                [name.Replace(" ", "")] = new TraitValue(damageBonus, TraitType.Number)
            },
            Position = EnchantmentPosition.Suffix
        };
    }
}
