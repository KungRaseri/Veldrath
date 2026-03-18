using FluentAssertions;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using RealmEngine.Core.Features.Salvaging.Commands;
using RealmEngine.Core.Features.Salvaging.Queries;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Salvaging;

/// <summary>
/// Integration tests for the item salvaging system.
/// Tests scrap generation, yield rates, and type-based material mapping.
/// </summary>
public class SalvagingIntegrationTests
{
    private readonly IMediator _mediator;

    public SalvagingIntegrationTests()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SalvageItemHandler).Assembly));
        
        var serviceProvider = services.BuildServiceProvider();
        _mediator = serviceProvider.GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task SalvageItem_CommonWeapon_ReturnsScrapMetal()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 0);
        var item = CreateTestWeapon(ItemRarity.Common);

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "forge"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.ItemDestroyed.Should().BeTrue();
        result.SkillUsed.Should().Be("Blacksmithing");
        result.YieldRate.Should().Be(40.0); // 40% base + (0 * 0.3%)
        result.ScrapMaterials.Should().ContainKey("Scrap Metal");
        result.ScrapMaterials["Scrap Metal"].Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SalvageItem_WithHighSkill_IncreasedYield()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 100);
        var item = CreateTestWeapon(ItemRarity.Common);

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "forge"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        // 40% base + (100 * 0.3%) = 70%
        result.YieldRate.Should().Be(70.0);
        // Should get more scraps than zero skill
    }

    [Fact]
    public async Task SalvageItem_MaxSkill_CappedAt100Percent()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 200); // Very high skill
        var item = CreateTestWeapon(ItemRarity.Common);

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "forge"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        // 40% base + (200 * 0.3%) = 100% (capped)
        result.YieldRate.Should().Be(100.0);
    }

    [Fact]
    public async Task SalvageItem_RareItem_MoreScrap()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 50);
        var commonItem = CreateTestWeapon(ItemRarity.Common);
        var rareItem = CreateTestWeapon(ItemRarity.Rare);

        // Act
        var commonResult = await _mediator.Send(new SalvageItemCommand
        {
            Character = character,
            Item = commonItem,
            StationId = "forge"
        });

        var rareResult = await _mediator.Send(new SalvageItemCommand
        {
            Character = character,
            Item = rareItem,
            StationId = "forge"
        });

        // Assert
        var commonTotal = commonResult.ScrapMaterials.Values.Sum();
        var rareTotal = rareResult.ScrapMaterials.Values.Sum();
        
        rareTotal.Should().BeGreaterThan(commonTotal);
    }

    [Fact]
    public async Task SalvageItem_UpgradedItem_BonusScrap()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 50);
        var baseItem = CreateTestWeapon(ItemRarity.Common);
        baseItem.UpgradeLevel = 0;
        
        var upgradedItem = CreateTestWeapon(ItemRarity.Common);
        upgradedItem.UpgradeLevel = 5;

        // Act
        var baseResult = await _mediator.Send(new SalvageItemCommand
        {
            Character = character,
            Item = baseItem,
            StationId = "forge"
        });

        var upgradedResult = await _mediator.Send(new SalvageItemCommand
        {
            Character = character,
            Item = upgradedItem,
            StationId = "forge"
        });

        // Assert
        var baseTotal = baseResult.ScrapMaterials.Values.Sum();
        var upgradedTotal = upgradedResult.ScrapMaterials.Values.Sum();
        
        upgradedTotal.Should().BeGreaterThan(baseTotal);
        // Upgrade bonus adds to base before yield calculation (55% rate)
        // Base: 3 * 0.55 = 2, Upgraded: (3+5) * 0.55 = 5
        upgradedTotal.Should().BeGreaterThanOrEqualTo(baseTotal + 2);
    }

    [Fact]
    public async Task SalvageItem_LeatherArmor_ReturnsScrapLeather()
    {
        // Arrange
        var character = CreateTestCharacter("Leatherworking", 50);
        var item = new Item
        {
            Name = "Leather Gloves",
            Type = ItemType.Gloves,
            Rarity = ItemRarity.Common
        };

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "leatherworking-station"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.SkillUsed.Should().Be("Leatherworking");
        result.ScrapMaterials.Should().ContainKey("Scrap Leather");
    }

    [Fact]
    public async Task SalvageItem_Jewelry_ReturnsGemstoneFragments()
    {
        // Arrange
        var character = CreateTestCharacter("Jewelcrafting", 50);
        var item = new Item
        {
            Name = "Gold Ring",
            Type = ItemType.Ring,
            Rarity = ItemRarity.Rare
        };

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "jewelcrafting-bench"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        result.SkillUsed.Should().Be("Jewelcrafting");
        result.ScrapMaterials.Should().ContainKey("Gemstone Fragments");
        result.ScrapMaterials.Should().ContainKey("Scrap Metal");
    }

    [Fact]
    public async Task SalvageItem_Consumable_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter("Alchemy", 50);
        var item = new Item
        {
            Name = "Health Potion",
            Type = ItemType.Consumable,
            Rarity = ItemRarity.Common
        };

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "alchemy-station"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ItemDestroyed.Should().BeFalse();
        result.Message.Should().Contain("Cannot salvage Consumable items");
    }

    [Fact]
    public async Task SalvageItem_QuestItem_ReturnsError()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 50);
        var item = new Item
        {
            Name = "Ancient Artifact",
            Type = ItemType.QuestItem,
            Rarity = ItemRarity.Legendary
        };

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "forge"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeFalse();
        result.ItemDestroyed.Should().BeFalse();
        result.Message.Should().Contain("Cannot salvage QuestItem items");
    }

    [Fact]
    public async Task SalvageItem_YieldRateCalculation_MatchesFormula()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 0);
        var item = CreateTestWeapon(ItemRarity.Common);

        // Test various skill levels
        var testCases = new Dictionary<int, double>
        {
            { 0, 40.0 },     // 40% base
            { 50, 55.0 },    // 40% + 15%
            { 100, 70.0 },   // 40% + 30%
            { 150, 85.0 },   // 40% + 45%
            { 200, 100.0 }   // 40% + 60% (capped at 100%)
        };

        foreach (var (skillLevel, expectedYield) in testCases)
        {
            // Update character skill
            character.Skills["Blacksmithing"] = new CharacterSkill
            {
                SkillId = "blacksmithing",
                Name = "Blacksmithing",
                Category = "Crafting",
                CurrentRank = skillLevel,
                CurrentXP = 0,
                XPToNextRank = 100
            };

            var command = new SalvageItemCommand
            {
                Character = character,
                Item = item,
                StationId = "forge"
            };

            // Act
            var result = await _mediator.Send(command);

            // Assert
            result.YieldRate.Should().Be(expectedYield);
        }
    }

    [Fact]
    public async Task SalvageItem_LegendaryItem_MaximumScrap()
    {
        // Arrange
        var character = CreateTestCharacter("Blacksmithing", 100);
        var item = CreateTestWeapon(ItemRarity.Legendary);
        item.UpgradeLevel = 10; // Max upgrade

        var command = new SalvageItemCommand
        {
            Character = character,
            Item = item,
            StationId = "forge"
        };

        // Act
        var result = await _mediator.Send(command);

        // Assert
        result.Success.Should().BeTrue();
        // Legendary base (10) + upgrade (10) = 20, then 70% yield = 14 scraps
        var totalScrap = result.ScrapMaterials.Values.Sum();
        totalScrap.Should().BeGreaterThanOrEqualTo(14); // At 70% yield rate
    }

    // Helper methods
    private Character CreateTestCharacter(string skillName, int skillLevel)
    {
        var character = new Character
        {
            Name = "Test Character",
            Skills = new Dictionary<string, CharacterSkill>()
        };

        if (skillLevel > 0 || skillName != "")
        {
            character.Skills[skillName] = new CharacterSkill
            {
                SkillId = skillName.ToLower(),
                Name = skillName,
                Category = "Crafting",
                CurrentRank = skillLevel,
                CurrentXP = 0,
                XPToNextRank = 100
            };
        }

        return character;
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
}

/// <summary>Tests for the salvage preview query.</summary>
[Trait("Category", "Feature")]
public class GetSalvagePreviewHandlerTests
{
    private readonly IMediator _mediator;

    public GetSalvagePreviewHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(SalvageItemHandler).Assembly));
        _mediator = services.BuildServiceProvider().GetRequiredService<IMediator>();
    }

    [Fact]
    public async Task Preview_Weapon_ReturnsCorrectSkillAndYield()
    {
        var character = CreateCharacterWithSkill("Blacksmithing", 100);
        var item = new Item { Name = "Iron Sword", Type = ItemType.Weapon, Rarity = ItemRarity.Common };

        var result = await _mediator.Send(new GetSalvagePreviewQuery { Character = character, Item = item });

        result.CanSalvage.Should().BeTrue();
        result.SkillName.Should().Be("Blacksmithing");
        result.SkillLevel.Should().Be(100);
        // 40% base + (100 * 0.3%) = 70%
        result.YieldRate.Should().Be(70.0);
        result.ExpectedScrap.Should().ContainKey("Scrap Metal");
        result.BlockedReason.Should().BeNull();
    }

    [Fact]
    public async Task Preview_Consumable_ReturnsCantSalvage()
    {
        var character = CreateCharacterWithSkill("Salvaging", 0);
        var item = new Item { Name = "Health Potion", Type = ItemType.Consumable, Rarity = ItemRarity.Common };

        var result = await _mediator.Send(new GetSalvagePreviewQuery { Character = character, Item = item });

        result.CanSalvage.Should().BeFalse();
        result.BlockedReason.Should().NotBeNullOrWhiteSpace();
        result.ExpectedScrap.Should().BeEmpty();
    }

    [Fact]
    public async Task Preview_MatchesSalvageCommandResult()
    {
        // Preview and actual salvage should produce identical scrap quantities and yield rate
        var character = CreateCharacterWithSkill("Jewelcrafting", 60);
        var item = new Item { Name = "Gold Ring", Type = ItemType.Ring, Rarity = ItemRarity.Rare };

        var preview = await _mediator.Send(new GetSalvagePreviewQuery { Character = character, Item = item });
        var actual  = await _mediator.Send(new SalvageItemCommand { Character = character, Item = item, StationId = "jeweler" });

        preview.CanSalvage.Should().BeTrue();
        actual.Success.Should().BeTrue();
        preview.YieldRate.Should().Be(actual.YieldRate);
        preview.ExpectedScrap.Should().BeEquivalentTo(actual.ScrapMaterials);
    }

    [Fact]
    public async Task Preview_DoesNotDestroyItem()
    {
        // Sending the preview twice should return the same result — item is never consumed
        var character = CreateCharacterWithSkill("Blacksmithing", 0);
        var item = new Item { Name = "Rusty Axe", Type = ItemType.Weapon, Rarity = ItemRarity.Common };

        var first  = await _mediator.Send(new GetSalvagePreviewQuery { Character = character, Item = item });
        var second = await _mediator.Send(new GetSalvagePreviewQuery { Character = character, Item = item });

        first.YieldRate.Should().Be(second.YieldRate);
        first.ExpectedScrap.Should().BeEquivalentTo(second.ExpectedScrap);
    }

    private static Character CreateCharacterWithSkill(string skillName, int rank) => new()
    {
        Name   = "Test Hero",
        Skills = new Dictionary<string, CharacterSkill>
        {
            [skillName] = new CharacterSkill
            {
                SkillId     = skillName.ToLower(),
                Name        = skillName,
                Category    = "Crafting",
                CurrentRank = rank,
                CurrentXP   = 0,
                XPToNextRank = 100
            }
        }
    };
}

