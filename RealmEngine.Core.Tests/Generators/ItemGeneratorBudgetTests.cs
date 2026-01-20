using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Xunit;

namespace RealmEngine.Core.Tests.Generators;

public class ItemGeneratorBudgetTests
{
    private readonly ItemGenerator _generator;

    public ItemGeneratorBudgetTests()
    {
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        dataPath = Path.GetFullPath(dataPath);
        
        var dataCache = new GameDataCache(dataPath);
        dataCache.LoadAllData();
        var referenceResolver = new ReferenceResolverService(dataCache, NullLogger<ReferenceResolverService>.Instance);
        var loggerFactory = NullLoggerFactory.Instance;

        _generator = new ItemGenerator(
            dataCache,
            referenceResolver,
            NullLogger<ItemGenerator>.Instance,
            loggerFactory);
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_GoblinLevel1_CreatesValidItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 1,
            ItemCategory = "weapons"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Name.Should().NotBeNullOrEmpty();
        item.BaseName.Should().NotBeNullOrEmpty();
        item.Type.Should().Be(ItemType.Weapon);
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_DragonLevel20_CreatesLegendaryItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "dragon",
            EnemyLevel = 20,
            ItemCategory = "weapons"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Rarity.Should().BeOneOf(ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary);
        item.Material.Should().NotBeNull("dragons should drop items with materials");
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_ContainsMaterial()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Material.Should().NotBeNull();
        item.Name.Should().Contain(item.Material!.Name);
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_NameIncludesBaseName()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "orc",
            EnemyLevel = 5,
            ItemCategory = "weapons"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Name.Should().Contain(item.BaseName);
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_StoresBudgetMetadata()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 5,
            ItemCategory = "weapons"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Traits.Should().ContainKey("Budget.Total");
        item.Traits.Should().ContainKey("Budget.Spent");
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_AppliesMaterialTraits()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Traits.Keys.Should().Contain(k => k.StartsWith("Material."));
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_AppliesBaseItemStats()
    {
        // Arrange
        var weaponRequest = new BudgetItemRequest
        {
            EnemyType = "orc",
            EnemyLevel = 5,
            ItemCategory = "weapons"
        };

        // Act
        var weapon = await _generator.GenerateItemWithBudgetAsync(weaponRequest);

        // Assert
        weapon.Should().NotBeNull();
        weapon!.Traits.Should().ContainKey("Damage");
    }

    [Fact]
    public async Task GenerateItemsWithBudgetAsync_GeneratesMultipleItems()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 5,
            ItemCategory = "weapons"
        };

        // Act
        var items = await _generator.GenerateItemsWithBudgetAsync(request, count: 5);

        // Assert
        items.Should().HaveCount(5);
        items.Should().OnlyContain(i => i.Type == ItemType.Weapon);
        items.Should().OnlyContain(i => i.Material != null);
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_HigherLevel_HigherRarity()
    {
        // Arrange
        var lowLevelRequest = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 1,
            ItemCategory = "weapons"
        };

        var highLevelRequest = new BudgetItemRequest
        {
            EnemyType = "goblin",
            EnemyLevel = 20,
            ItemCategory = "weapons"
        };

        // Act
        var lowLevelItem = await _generator.GenerateItemWithBudgetAsync(lowLevelRequest);
        var highLevelItem = await _generator.GenerateItemWithBudgetAsync(highLevelRequest);

        // Assert
        lowLevelItem.Should().NotBeNull();
        highLevelItem.Should().NotBeNull();
        
        // High level items should have at least same or better rarity
        highLevelItem!.Rarity.Should().BeOneOf(
            lowLevelItem!.Rarity,
            ItemRarity.Rare, 
            ItemRarity.Epic, 
            ItemRarity.Legendary
        );
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_Armor_CreatesArmorItem()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "armor"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Type.Should().Be(ItemType.Chest);
        item.Traits.Should().ContainKey("Defense");
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_QualityModifier_AffectsName()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "knight",
            EnemyLevel = 10,
            ItemCategory = "weapons",
            AllowQuality = true
        };

        // Act - Generate multiple to increase chance of quality
        Item? itemWithQuality = null;
        for (int i = 0; i < 10; i++)
        {
            var item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item!.Prefixes.Any(p => p.Token == "quality"))
            {
                itemWithQuality = item;
                break;
            }
        }

        // Assert
        if (itemWithQuality != null)
        {
            itemWithQuality.Prefixes.Should().Contain(p => p.Token == "quality");
            // Quality words like "Fine", "Superior", etc. should appear in name
            var qualityPrefix = itemWithQuality.Prefixes.First(p => p.Token == "quality");
            itemWithQuality.Name.Should().Contain(qualityPrefix.Value);
        }
    }

    [Theory]
    [InlineData("weapons", ItemType.Weapon)]
    [InlineData("armor", ItemType.Chest)]
    public async Task GenerateItemWithBudgetAsync_DifferentCategories_CorrectItemType(string category, ItemType expectedType)
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "orc",
            EnemyLevel = 5,
            ItemCategory = category
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        item!.Type.Should().Be(expectedType);
    }

    [Fact]
    public async Task GenerateItemWithBudgetAsync_PrefixesAndSuffixes_AppearInOrder()
    {
        // Arrange
        var request = new BudgetItemRequest
        {
            EnemyType = "dragon",
            EnemyLevel = 20,
            ItemCategory = "weapons"
        };

        // Act
        var item = await _generator.GenerateItemWithBudgetAsync(request);

        // Assert
        item.Should().NotBeNull();
        
        // Name should follow pattern: [prefixes] [material] BaseName [suffixes]
        if (item!.Prefixes.Any() && item.Suffixes.Any())
        {
            var firstPrefix = item.Prefixes.First().Value;
            var firstSuffix = item.Suffixes.First().Value;
            
            var prefixIndex = item.Name.IndexOf(firstPrefix);
            var baseIndex = item.Name.IndexOf(item.BaseName);
            var suffixIndex = item.Name.IndexOf(firstSuffix);
            
            prefixIndex.Should().BeLessThan(baseIndex, "prefixes should come before base name");
            baseIndex.Should().BeLessThan(suffixIndex, "base name should come before suffixes");
        }
    }
}
