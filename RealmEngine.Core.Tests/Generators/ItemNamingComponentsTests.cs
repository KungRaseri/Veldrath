using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Data.Services;
using RealmEngine.Shared.Models;
using Xunit;
using Xunit.Abstractions;

namespace RealmEngine.Core.Tests.Generators;

/// <summary>
/// Tests for Item naming component properties to ensure generated items have all naming parts properly separated.
/// </summary>
public class ItemNamingComponentsTests
{
    private readonly ITestOutputHelper _output;
    private readonly string _dataPath;
    private readonly GameDataCache _cache;
    private readonly ItemGenerator _generator;

    public ItemNamingComponentsTests(ITestOutputHelper output)
    {
        _output = output;
        _dataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        _dataPath = Path.GetFullPath(_dataPath);

        var loggerFactory = NullLoggerFactory.Instance;
        var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var cacheLogger = loggerFactory.CreateLogger<GameDataCache>();
        var resolverLogger = loggerFactory.CreateLogger<ReferenceResolverService>();
        var generatorLogger = loggerFactory.CreateLogger<ItemGenerator>();

        _cache = new GameDataCache(_dataPath, memoryCache);
        _cache.LoadAllData();

        var resolver = new ReferenceResolverService(_cache, resolverLogger);
        _generator = new ItemGenerator(_cache, resolver, generatorLogger, loggerFactory);
    }

    [Fact]
    public async Task Generated_Items_Should_Have_BaseName_Property()
    {
        // Act
        var items = await _generator.GenerateItemsAsync("weapons", 10);

        // Assert
        items.Should().NotBeEmpty();
        foreach (var item in items)
        {
            item.BaseName.Should().NotBeNullOrWhiteSpace("all items should have a base name");
            _output.WriteLine($"Item: {item.Name}");
            _output.WriteLine($"  BaseName: {item.BaseName}");
        }
    }

    [Fact(Skip = "Legacy generator doesn't populate materials - budget-based generation tested in BudgetItemGenerationTests")]
    public async Task Items_With_Material_Should_Have_Material_In_Prefixes()
    {
        // Generate many items to increase chance of getting one with material
        var allItems = new List<Item>();
        for (int i = 0; i < 5; i++)
        {
            var items = await _generator.GenerateItemsAsync("weapons", 20);
            allItems.AddRange(items);
        }

        // Find items with materials
        var itemsWithMaterial = allItems.Where(i => i.Material != null).ToList();

        // Assert
        itemsWithMaterial.Should().NotBeEmpty("at least some generated items should have materials");
        
        foreach (var item in itemsWithMaterial.Take(5))
        {
            item.Material.Should().NotBeNull();
            item.Material!.Name.Should().NotBeNullOrWhiteSpace();
            var materialComponent = item.GetPrefixValue("material");
            materialComponent.Should().Be(item.Material.Name, "material component should match Material property");
            item.Name.Should().Contain(item.Material.Name, "item name should contain the material");
            
            _output.WriteLine($"Item: {item.Name}");
            _output.WriteLine($"  Material: {item.Material}");
            _output.WriteLine($"  Material Component: {materialComponent}");
        }
    }

    [Fact]
    public async Task ComposeNameFromComponents_Should_Match_Generated_Name_Without_Sockets()
    {
        // Generate items
        var items = await _generator.GenerateItemsAsync("weapons", 20);

        // Assert
        items.Should().NotBeEmpty();

        foreach (var item in items)
        {
            var composedName = item.ComposeNameFromComponents();
            
            // ComposeNameFromComponents should match the name WITHOUT socket display
            // (sockets are separate from naming components per design decision)
            var expectedName = item.Name;
            var socketsDisplay = item.GetSocketsDisplayText();
            if (!string.IsNullOrWhiteSpace(socketsDisplay))
            {
                expectedName = item.Name.Replace($" [{socketsDisplay}]", "").Trim();
            }
            
            composedName.Should().Be(expectedName, 
                "ComposeNameFromComponents should produce the name without sockets");
            
            _output.WriteLine($"Generated: {item.Name}");
            _output.WriteLine($"Composed:  {composedName}");
            _output.WriteLine($"  Match: {composedName == expectedName}\n");
        }
    }

    [Fact(Skip = "Legacy generator doesn't populate component lists - budget-based generation tested in BudgetItemGenerationTests")]
    public async Task Generated_Items_Should_Populate_Component_Lists()
    {
        // Generate items with enchantments and materials
        var items = await _generator.GenerateItemsAsync("weapons", 50);
        
        // Find items with components
        var itemsWithPrefixes = items.Where(i => i.Prefixes.Any()).ToList();
        var itemsWithSuffixes = items.Where(i => i.Suffixes.Any()).ToList();
        
        // Assert
        itemsWithPrefixes.Should().NotBeEmpty("some items should have prefix components");
        
        foreach (var item in itemsWithPrefixes.Take(5))
        {
            _output.WriteLine($"Item: {item.Name}");
            _output.WriteLine($"  BaseName: {item.BaseName}");
            _output.WriteLine($"  Prefixes ({item.Prefixes.Count}):");
            foreach (var prefix in item.Prefixes)
            {
                _output.WriteLine($"    [{prefix.Token}] = {prefix.Value}");
            }
            _output.WriteLine($"  Suffixes ({item.Suffixes.Count}):");
            foreach (var suffix in item.Suffixes)
            {
                _output.WriteLine($"    [{suffix.Token}] = {suffix.Value}");
            }
            _output.WriteLine("");
            
            // Verify components are accessible
            item.Prefixes.Should().AllSatisfy(p =>
            {
                p.Token.Should().NotBeNullOrEmpty();
                p.Value.Should().NotBeNullOrEmpty();
            });
        }
        
        if (itemsWithSuffixes.Any())
        {
            itemsWithSuffixes.First().Suffixes.Should().AllSatisfy(s =>
            {
                s.Token.Should().NotBeNullOrEmpty();
                s.Value.Should().NotBeNullOrEmpty();
            });
        }
    }

    [Fact(Skip = "Legacy generator doesn't populate materials - budget-based generation tested in BudgetItemGenerationTests")]
    public async Task GetPrefixValue_Should_Return_Component_By_Token()
    {
        // Generate items
        var items = await _generator.GenerateItemsAsync("weapons", 30);
        
        // Find item with material
        var itemWithMaterial = items.FirstOrDefault(i => i.Material != null);
        itemWithMaterial.Should().NotBeNull("at least one item should have a material");
        
        // Test GetPrefixValue
        var material = itemWithMaterial!.GetPrefixValue("material");
        material.Should().Be(itemWithMaterial.Material!.Name, 
            "GetPrefixValue should return the material component");
        
        _output.WriteLine($"Item: {itemWithMaterial.Name}");
        _output.WriteLine($"  Material via property: {itemWithMaterial.Material}");
        _output.WriteLine($"  Material via GetPrefixValue: {material}");
    }

    [Fact]
    public async Task GetSuffixValue_Should_Return_Component_By_Token()
    {
        // Generate many items to find one with suffix enchantment
        var items = await _generator.GenerateItemsAsync("weapons", 50);
        
        // Find item with suffix enchantment
        var itemWithSuffixEnchantment = items.FirstOrDefault(i => 
            i.Suffixes.Any(s => s.Token == "enchantment_suffix"));
        
        if (itemWithSuffixEnchantment != null)
        {
            // Test GetSuffixValue
            var enchantmentSuffix = itemWithSuffixEnchantment.GetSuffixValue("enchantment_suffix");
            enchantmentSuffix.Should().NotBeNullOrEmpty("item has suffix enchantment");
            
            _output.WriteLine($"Item: {itemWithSuffixEnchantment.Name}");
            _output.WriteLine($"  Suffix enchantment: {enchantmentSuffix}");
        }
        else
        {
            _output.WriteLine("No items generated with suffix enchantments in this run");
        }
    }

    [Fact]
    public async Task Generated_Items_Should_Not_Have_Suffix_Enchantments_At_Start_Of_Name()
    {
        // Generate many items
        var allItems = new List<Item>();
        for (int i = 0; i < 5; i++)
        {
            var items = await _generator.GenerateItemsAsync("weapons", 20);
            allItems.AddRange(items);
        }

        // Find items with suffix enchantments that start with "of"
        var itemsWithOfSuffixes = allItems
            .Where(i => i.Suffixes.Any(s => 
                s.Token == "enchantment_suffix" && 
                s.Value.StartsWith("of ", StringComparison.OrdinalIgnoreCase)))
            .ToList();

        _output.WriteLine($"Found {itemsWithOfSuffixes.Count} items with 'of' suffix enchantments");

        // Assert - suffix enchantments should NOT appear at the start of the name
        foreach (var item in itemsWithOfSuffixes.Take(10))
        {
            _output.WriteLine($"\nItem: {item.Name}");
            _output.WriteLine($"  Base: {item.BaseName}");
            if (item.Prefixes.Any())
                _output.WriteLine($"  Prefixes: [{string.Join(", ", item.Prefixes.Select(p => p.Value))}]");
            if (item.Suffixes.Any())
                _output.WriteLine($"  Suffixes: [{string.Join(", ", item.Suffixes.Select(s => s.Value))}]");

            // Verify that "of" suffixes don't appear at the start
            var ofSuffixes = item.Suffixes
                .Where(s => s.Token == "enchantment_suffix" && s.Value.StartsWith("of "))
                .Select(s => s.Value)
                .ToList();
                
            foreach (var suffix in ofSuffixes)
            {
                item.Name.Should().NotStartWith(suffix, 
                    $"suffix enchantment '{suffix}' should not appear at the start of the item name");
                
                // Verify the suffix appears AFTER the base name
                var baseNameIndex = item.Name.IndexOf(item.BaseName, StringComparison.OrdinalIgnoreCase);
                var suffixIndex = item.Name.IndexOf(suffix, StringComparison.OrdinalIgnoreCase);
                
                if (baseNameIndex >= 0 && suffixIndex >= 0)
                {
                    suffixIndex.Should().BeGreaterThan(baseNameIndex,
                        $"suffix '{suffix}' should appear after base name '{item.BaseName}'");
                }
            }
        }
    }
}
