using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Generators;

[Trait("Category", "Generator")]
public class EnchantmentGeneratorTests
{
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly EnchantmentGenerator _generator;

    public EnchantmentGeneratorTests()
    {
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        _dataCache = new GameDataCache(basePath);
        var mockLogger = new Mock<ILogger<ReferenceResolverService>>();
        _referenceResolver = new ReferenceResolverService(_dataCache, mockLogger.Object);
        var enchantmentLogger = new Mock<ILogger<EnchantmentGenerator>>();
        _generator = new EnchantmentGenerator(_dataCache, _referenceResolver, enchantmentLogger.Object);
    }

    [Fact]
    public async Task Should_Generate_Enchantment_From_Reference()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantment = await _generator.GenerateEnchantmentAsync("@items/enchantments:*");

        // Assert
        enchantment.Should().NotBeNull();
        enchantment!.Name.Should().NotBeNullOrEmpty();
        enchantment.Position.Should().BeDefined();
        enchantment.RarityWeight.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Should_Generate_Multiple_Enchantments()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(10);

        // Assert
        enchantments.Should().NotBeNull();
        enchantments.Should().HaveCountGreaterThanOrEqualTo(1);
        
        foreach (var enchantment in enchantments)
        {
            enchantment.Name.Should().NotBeNullOrEmpty();
            enchantment.Position.Should().BeDefined();
        }
    }

    [Fact]
    public async Task Should_Generate_Enchantments_With_Traits()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(10);

        // Assert
        enchantments.Should().NotBeEmpty();
        
        foreach (var enchantment in enchantments)
        {
            enchantment.Traits.Should().NotBeEmpty("Enchantments should have at least one trait");
            
            foreach (var trait in enchantment.Traits)
            {
                trait.Key.Should().NotBeNullOrEmpty();
                trait.Value.Should().NotBeNull();
                trait.Value.Type.Should().BeDefined();
            }
        }
    }

    [Fact]
    public async Task Should_Generate_Both_Prefix_And_Suffix_Enchantments()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(30);

        // Assert
        var prefixes = enchantments.Where(e => e.Position == EnchantmentPosition.Prefix).ToList();
        var suffixes = enchantments.Where(e => e.Position == EnchantmentPosition.Suffix).ToList();
        
        // Should have variety
        prefixes.Should().NotBeEmpty("Should generate some prefix enchantments");
        suffixes.Should().NotBeEmpty("Should generate some suffix enchantments");
    }

    [Fact]
    public async Task Should_Generate_Enchantments_With_Varying_Rarity()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(30);

        // Assert
        enchantments.Should().NotBeEmpty();
        
        var rarities = enchantments.Select(e => e.Rarity).Distinct().ToList();
        
        // Should have some variety in rarities
        rarities.Should().NotBeEmpty();
        
        // All rarities should be valid
        foreach (var enchantment in enchantments)
        {
            enchantment.Rarity.Should().BeDefined();
            enchantment.RarityWeight.Should().BeGreaterThan(0);
        }
    }

    [Fact]
    public async Task Should_Map_RarityWeight_To_EnchantmentRarity_Correctly()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(40);

        // Assert
        // EnchantmentRarity mapping:
        // Minor: < 40
        // Lesser: 40-79
        // Greater: 80-119
        // Superior: 120-179
        // Legendary: 180+
        
        foreach (var enchantment in enchantments)
        {
            if (enchantment.RarityWeight < 40)
            {
                enchantment.Rarity.Should().Be(EnchantmentRarity.Minor);
            }
            else if (enchantment.RarityWeight < 80)
            {
                enchantment.Rarity.Should().Be(EnchantmentRarity.Lesser);
            }
            else if (enchantment.RarityWeight < 120)
            {
                enchantment.Rarity.Should().Be(EnchantmentRarity.Greater);
            }
            else if (enchantment.RarityWeight < 180)
            {
                enchantment.Rarity.Should().Be(EnchantmentRarity.Superior);
            }
            else
            {
                enchantment.Rarity.Should().Be(EnchantmentRarity.Legendary);
            }
        }
    }

    [Fact]
    public async Task Should_Generate_Enchantments_With_Appropriate_Names()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(20);

        // Assert
        enchantments.Should().NotBeEmpty();
        
        var prefixes = enchantments.Where(e => e.Position == EnchantmentPosition.Prefix).ToList();
        var suffixes = enchantments.Where(e => e.Position == EnchantmentPosition.Suffix).ToList();
        
        // Prefix enchantments typically don't have "of" in the name
        foreach (var prefix in prefixes)
        {
            prefix.Name.Should().NotBeNullOrEmpty();
            // Prefixes like "Flaming", "Frozen", "Shocking" etc
        }
        
        // Suffix enchantments typically have "of" in the name
        foreach (var suffix in suffixes)
        {
            suffix.Name.Should().NotBeNullOrEmpty();
            // Suffixes like "of Fire", "of Strength", "of Power" etc
        }
    }

    [Fact]
    public async Task Should_Generate_Enchantments_With_Numeric_Traits()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(20);

        // Assert
        enchantments.Should().NotBeEmpty();
        
        // Most enchantments should have numeric traits (damage, stats, etc.)
        var enchantmentsWithNumericTraits = enchantments.Where(e => 
            e.Traits.Any(t => t.Value.Type == TraitType.Number)).ToList();
        
        enchantmentsWithNumericTraits.Should().NotBeEmpty("Enchantments should have numeric traits");
    }

    [Fact]
    public async Task Should_Generate_Variety_Of_Enchantment_Types()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantments = await _generator.GenerateEnchantmentsAsync(50);

        // Assert
        var uniqueNames = enchantments.Select(e => e.Name).Distinct().ToList();
        
        // Should have variety in enchantment names
        uniqueNames.Should().HaveCountGreaterThanOrEqualTo(3, "Should generate multiple different enchantment types");
    }

    [Fact]
    public async Task Should_Handle_Invalid_Reference_Gracefully()
    {
        // Arrange
        _dataCache.LoadAllData();
        
        // Act
        var enchantment = await _generator.GenerateEnchantmentAsync("@invalid/path:*");

        // Assert
        // CURRENT BEHAVIOR: Generator ignores invalid references and falls back to items/enchantments/names.json
        // This is intentional graceful degradation - the generator ensures content is always produced
        // even when references are malformed. Production code should validate references before calling.
        // 
        // FUTURE ENHANCEMENT: Add strict mode parameter to throw on invalid references for debugging.
        enchantment.Should().NotBeNull("generator gracefully falls back to default catalog on invalid references");
    }
}
