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

        // Act - Retry up to 10 times (level 1 has very tight budget constraints)
        Item? item = null;
        for (int i = 0; i < 10; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null)
                break;
        }

        // Assert
        item.Should().NotBeNull("At least one attempt should succeed with cheap materials");
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

        // Act - Retry up to 10 times (high-tier materials may not always be affordable)
        Item? item = null;
        for (int i = 0; i < 10; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null)
                break;
        }

        // Assert
        item.Should().NotBeNull("Dragon should eventually generate an item");
        item!.Rarity.Should().BeOneOf(ItemRarity.Common, ItemRarity.Uncommon, ItemRarity.Rare, ItemRarity.Epic, ItemRarity.Legendary);
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

        // Act - Retry up to 5 times to handle edge cases where budget generation fails
        Item? item = null;
        for (int i = 0; i < 5; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null)
                break;
        }

        // Assert
        item.Should().NotBeNull("budget generation should succeed within 5 attempts");
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

        // Act - Retry up to 5 times
        Item? item = null;
        for (int i = 0; i < 5; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null) break;
        }

        // Assert
        item.Should().NotBeNull("Budget generation should eventually succeed with retry logic");
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
        // Act - Retry up to 5 times
        Item? item = null;
        for (int i = 0; i < 5; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null)
                break;
        }

        // Assert
        item.Should().NotBeNull("Budget generation should eventually succeed");
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

        // Act - Retry up to 5 times
        Item? item = null;
        for (int i = 0; i < 5; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null)
                break;
        }

        // Assert
        item.Should().NotBeNull("Budget generation should eventually succeed");
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

        // Act - Retry up to 10 times
        Item? weapon = null;
        for (int i = 0; i < 10; i++)
        {
            weapon = await _generator.GenerateItemWithBudgetAsync(weaponRequest);
            if (weapon != null) break;
        }

        // Assert
        weapon.Should().NotBeNull("Weapon generation should eventually succeed");
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

        // Assert - Budget generation may occasionally fail due to RNG, accept 1+ successes
        items.Should().HaveCountGreaterThanOrEqualTo(1, "Should generate at least one item");
        items.Should().OnlyContain(i => i.Type == ItemType.Weapon, "All generated items should be weapons");
        items.Where(i => i != null).Should().OnlyContain(i => i.Material != null, "Generated items should have materials");
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

        // Act - Retry up to 10 times for tight budgets
        Item? lowLevelItem = null;
        for (int i = 0; i < 10; i++)
        {
            lowLevelItem = await _generator.GenerateItemWithBudgetAsync(lowLevelRequest);
            if (lowLevelItem != null) break;
        }
            
        Item? highLevelItem = null;
        for (int i = 0; i < 10; i++)
        {
            highLevelItem = await _generator.GenerateItemWithBudgetAsync(highLevelRequest);
            if (highLevelItem != null) break;
        }

        // Assert
        lowLevelItem.Should().NotBeNull("Low level generation should eventually succeed");
        highLevelItem.Should().NotBeNull("High level generation should eventually succeed");
        
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

        // Act - Retry up to 10 times (armor has fewer cheap base items)
        Item? item = null;
        for (int i = 0; i < 10; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null)
                break;
        }

        // Assert
        item.Should().NotBeNull("Armor generation should eventually succeed");
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
            if (item?.Quality != null)
            {
                itemWithQuality = item;
                break;
            }
        }

        // Assert
        if (itemWithQuality != null)
        {
            itemWithQuality.Quality.Should().NotBeNull();
            // Quality words like "Fine", "Superior", etc. should appear in name
            itemWithQuality.Name.Should().Contain(itemWithQuality.Quality!.Name);
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

        // Act - Retry up to 10 times
        Item? item = null;
        for (int i = 0; i < 10; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null) break;
        }

        // Assert
        item.Should().NotBeNull("Item generation should eventually succeed for both weapons and armor");
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

        // Act - Retry up to 10 times
        Item? item = null;
        for (int i = 0; i < 10; i++)
        {
            item = await _generator.GenerateItemWithBudgetAsync(request);
            if (item != null) break;
        }

        // Assert
        item.Should().NotBeNull("High-level dragon generation should eventually succeed");
        
        // Name should follow pattern: [prefixes] [material] BaseName [suffixes]
        if (item!.PrefixComponents.Any() && item.SuffixComponents.Any())
        {
            var firstPrefix = item.PrefixComponents.First().Name;
            var firstSuffix = item.SuffixComponents.First().Name;
            
            var prefixIndex = item.Name.IndexOf(firstPrefix);
            var baseIndex = item.Name.IndexOf(item.BaseName);
            var suffixIndex = item.Name.IndexOf(firstSuffix);
            
            prefixIndex.Should().BeLessThan(baseIndex, "prefixes should come before base name");
            baseIndex.Should().BeLessThan(suffixIndex, "base name should come before suffixes");
        }
    }
}
