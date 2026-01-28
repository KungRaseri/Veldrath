using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Data.Services;
using Xunit;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for CategoryDiscoveryService.
/// </summary>
public class CategoryDiscoveryServiceTests : IDisposable
{
    private readonly GameDataCache _dataCache;
    private readonly CategoryDiscoveryService _service;
    private readonly string _testDataPath;

    public CategoryDiscoveryServiceTests()
    {
        // Use relative path to test data
        _testDataPath = Path.Combine(
            Directory.GetCurrentDirectory(),
            "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");

        _dataCache = new GameDataCache(_testDataPath, null);
        _dataCache.LoadAllData();
        
        var logger = NullLogger<CategoryDiscoveryService>.Instance;
        _service = new CategoryDiscoveryService(_dataCache, logger);
    }

    [Fact]
    public void Initialize_ShouldDiscoverAllLeafCategories()
    {
        // Act
        _service.Initialize();
        var stats = _service.GetStatistics();

        // Assert
        stats.IsInitialized.Should().BeTrue();
        stats.TotalDomains.Should().BeGreaterThan(0);
        stats.TotalCategories.Should().BeGreaterThan(0);
    }

    [Fact]
    public void GetLeafCategories_ForItems_ShouldReturnOnlyLeafCategories()
    {
        // Arrange
        _service.Initialize();

        // Act
        var categories = _service.GetLeafCategories("items");

        // Assert
        categories.Should().NotBeEmpty();
        categories.Should().OnlyContain(cat => !string.IsNullOrWhiteSpace(cat));
        
        // Verify all returned categories have catalog files
        foreach (var category in categories)
        {
            var catalogPath = $"items/{category}/catalog.json";
            _dataCache.FileExists(catalogPath).Should().BeTrue(
                $"Category '{category}' should have a catalog.json file");
        }
    }

    [Fact]
    public void FindCategories_WithWildcard_ShouldMatchPattern()
    {
        // Arrange
        _service.Initialize();

        // Act
        var materialsCategories = _service.FindCategories("items", "materials/*");

        // Assert
        materialsCategories.Should().NotBeEmpty();
        materialsCategories.Should().OnlyContain(cat => cat.StartsWith("materials/"));
    }

    [Fact]
    public void FindCategories_WithAsterisk_ShouldReturnAllCategories()
    {
        // Arrange
        _service.Initialize();

        // Act
        var allCategories = _service.FindCategories("items", "*");
        var directCategories = _service.GetLeafCategories("items");

        // Assert
        allCategories.Should().BeEquivalentTo(directCategories);
    }

    [Fact]
    public void FindCategories_WithExactMatch_ShouldReturnSingleCategory()
    {
        // Arrange
        _service.Initialize();
        var allCategories = _service.GetLeafCategories("items");
        var firstCategory = allCategories.First();

        // Act
        var result = _service.FindCategories("items", firstCategory);

        // Assert
        result.Should().ContainSingle();
        result.First().Should().Be(firstCategory);
    }

    [Fact]
    public void IsLeafCategory_WithValidLeafCategory_ShouldReturnTrue()
    {
        // Arrange
        _service.Initialize();
        var leafCategory = _service.GetLeafCategories("items").First();

        // Act
        var isLeaf = _service.IsLeafCategory("items", leafCategory);

        // Assert
        isLeaf.Should().BeTrue();
    }

    [Fact]
    public void IsLeafCategory_WithParentCategory_ShouldReturnFalse()
    {
        // Arrange
        _service.Initialize();

        // Act - "materials" is likely a parent, not a leaf
        var isLeaf = _service.IsLeafCategory("items", "materials");

        // Assert
        isLeaf.Should().BeFalse();
    }

    [Fact]
    public void GetCategoryInfo_WithValidCategory_ShouldReturnInfo()
    {
        // Arrange
        _service.Initialize();
        var leafCategory = _service.GetLeafCategories("items").First();

        // Act
        var info = _service.GetCategoryInfo("items", leafCategory);

        // Assert
        info.Should().NotBeNull();
        info!.Domain.Should().Be("items");
        info.Path.Should().Be(leafCategory);
        info.IsLeaf.Should().BeTrue();
    }

    [Fact]
    public void GetStatistics_ShouldReturnAccurateCounts()
    {
        // Arrange
        _service.Initialize();

        // Act
        var stats = _service.GetStatistics();

        // Assert
        stats.CategoriesByDomain.Should().ContainKey("items");
        stats.CategoriesByDomain["items"].Should().BeGreaterThan(0);
        
        var manualCount = _service.GetLeafCategories("items").Count;
        stats.CategoriesByDomain["items"].Should().Be(manualCount);
    }

    [Fact]
    public void Initialize_CalledTwice_ShouldNotReinitialize()
    {
        // Arrange
        _service.Initialize();
        var firstStats = _service.GetStatistics();

        // Act
        _service.Initialize(); // Second call
        var secondStats = _service.GetStatistics();

        // Assert
        secondStats.TotalCategories.Should().Be(firstStats.TotalCategories);
    }

    [Fact]
    public void GetLeafCategories_BeforeInitialize_ShouldAutoInitialize()
    {
        // Act - Don't call Initialize() manually
        var categories = _service.GetLeafCategories("items");

        // Assert
        categories.Should().NotBeEmpty();
        _service.GetStatistics().IsInitialized.Should().BeTrue();
    }

    public void Dispose()
    {
        // Cleanup if needed
    }
}
