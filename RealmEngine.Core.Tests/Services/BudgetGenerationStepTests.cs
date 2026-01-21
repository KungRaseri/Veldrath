using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json.Linq;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Data.Services;
using Xunit;
using Xunit.Abstractions;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests each step of budget generation individually for diagnostic purposes
/// </summary>
public class BudgetGenerationStepTests
{
    private readonly ITestOutputHelper _output;
    private readonly GameDataCache _dataCache;
    private readonly ReferenceResolverService _referenceResolver;
    private readonly BudgetConfigFactory _configFactory;
    private readonly BudgetCalculator _budgetCalculator;
    private readonly MaterialPoolService _materialPoolService;
    private readonly BudgetConfig _budgetConfig;
    private readonly MaterialPools _materialPools;
    private readonly EnemyTypes _enemyTypes;

    public BudgetGenerationStepTests(ITestOutputHelper output)
    {
        _output = output;
        
        var dataPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "..", "RealmEngine.Data", "Data", "Json");
        dataPath = Path.GetFullPath(dataPath);
        
        _output.WriteLine($"Data path: {dataPath}");
        _output.WriteLine($"Path exists: {Directory.Exists(dataPath)}");
        
        _dataCache = new GameDataCache(dataPath);
        _dataCache.LoadAllData();
        _output.WriteLine($"Data cache loaded");
        
        _referenceResolver = new ReferenceResolverService(_dataCache, NullLogger<ReferenceResolverService>.Instance);
        _configFactory = new BudgetConfigFactory(_dataCache, NullLogger<BudgetConfigFactory>.Instance);
        
        _budgetConfig = _configFactory.GetBudgetConfig();
        _materialPools = _configFactory.GetMaterialPools();
        _enemyTypes = _configFactory.GetEnemyTypes();
        
        _budgetCalculator = new BudgetCalculator(_budgetConfig, NullLogger<BudgetCalculator>.Instance);
        _materialPoolService = new MaterialPoolService(
            _dataCache,
            _referenceResolver,
            _budgetCalculator,
            _materialPools,
            _enemyTypes,
            NullLogger<MaterialPoolService>.Instance);
    }

    [Fact]
    public void Step1_CalculateBaseBudget_Works()
    {
        // Arrange
        int level = 1;
        string enemyType = "goblin";
        
        // Act
        var baseBudget = _budgetCalculator.CalculateBaseBudget(level, isBoss: false, isElite: false);
        var typeMultiplier = _materialPoolService.GetBudgetMultiplier(enemyType);
        var adjustedBaseBudget = (int)Math.Round(baseBudget * typeMultiplier);
        
        // Assert
        _output.WriteLine($"Level {level} base budget: {baseBudget}");
        _output.WriteLine($"Enemy type '{enemyType}' multiplier: {typeMultiplier}");
        _output.WriteLine($"Adjusted base budget: {adjustedBaseBudget}");
        
        baseBudget.Should().BeGreaterThan(0);
        adjustedBaseBudget.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Step2_BudgetSplit_Works()
    {
        // Arrange
        int totalBudget = 5; // Level 1 goblin base budget
        
        // Act
        var materialBudget = _budgetCalculator.CalculateMaterialBudget(totalBudget);
        var componentBudget = _budgetCalculator.CalculateComponentBudget(totalBudget, materialBudget);
        
        // Assert
        _output.WriteLine($"Total budget: {totalBudget}");
        _output.WriteLine($"Material budget (30%): {materialBudget}");
        _output.WriteLine($"Component budget (70%): {componentBudget}");
        
        materialBudget.Should().BeGreaterThan(0);
        componentBudget.Should().BeGreaterThan(0);
        (materialBudget + componentBudget).Should().Be(totalBudget);
    }

    [Fact]
    public async Task Step3_MaterialSelection_Works()
    {
        // Arrange
        string materialType = "metals";
        int materialBudget = 40; // About 40% of 100 budget
        
        _output.WriteLine($"Selecting material from '{materialType}' pool with budget {materialBudget}");
        
        // Act - Use "metals" pool for weapon materials
        var material = await _materialPoolService.SelectMaterialAsync(materialType, materialBudget);
        
        // Assert
        if (material == null)
        {
            _output.WriteLine("FAILED: Material is null");
            
            // Diagnostic info
            _output.WriteLine($"Material pool '{materialType}' exists: {_materialPools.Pools.ContainsKey(materialType)}");
            
            if (_materialPools.Pools.TryGetValue(materialType, out var pool))
            {
                var allMaterials = pool.GetAllMaterials();
                _output.WriteLine($"Materials in pool: {allMaterials.Count()}");
                
                foreach (var matRef in allMaterials.Take(5))
                {
                    _output.WriteLine($"  - {matRef.ItemRef} (weight: {matRef.RarityWeight})");
                    var resolved = await _referenceResolver.ResolveToObjectAsync(matRef.ItemRef);
                    if (resolved != null)
                    {
                        var cost = _budgetCalculator.CalculateMaterialCost(resolved);
                        var affordable = _budgetCalculator.CanAfford(materialBudget, cost);
                        _output.WriteLine($"    Resolved: cost={cost}, affordable={affordable}");
                    }
                    else
                    {
                        _output.WriteLine($"    FAILED to resolve reference");
                    }
                }
            }
        }
        else
        {
            var name = material["name"]?.ToString();
            var cost = _budgetCalculator.CalculateMaterialCost(material);
            _output.WriteLine($"SUCCESS: Selected '{name}' with cost {cost}");
        }
        
        material.Should().NotBeNull("Material selection should succeed for metals pool with 40 budget");
    }

    [Fact]
    public void Step4_BaseItemCatalogExists()
    {
        // Arrange
        string category = "weapons";
        var catalogPath = $"items/{category}/catalog.json";
        
        // Act
        var fileExists = _dataCache.FileExists(catalogPath);
        
        // Assert
        _output.WriteLine($"Checking: {catalogPath}");
        _output.WriteLine($"File exists: {fileExists}");
        
        if (fileExists)
        {
            var file = _dataCache.GetFile(catalogPath);
            _output.WriteLine($"File loaded: {file != null}");
            _output.WriteLine($"JsonData exists: {file?.JsonData != null}");
            
            if (file?.JsonData != null)
            {
                // Try to extract items
                var hasItems = file.JsonData["items"] != null;
                var hasWeaponTypes = file.JsonData["weapon_types"] != null;
                _output.WriteLine($"Has 'items' array: {hasItems}");
                _output.WriteLine($"Has 'weapon_types' object: {hasWeaponTypes}");
                
                if (hasWeaponTypes)
                {
                    var weaponTypes = file.JsonData["weapon_types"] as JObject;
                    _output.WriteLine($"Weapon type categories: {weaponTypes?.Properties().Count()}");
                    foreach (var prop in weaponTypes?.Properties().Take(3) ?? Enumerable.Empty<JProperty>())
                    {
                        var items = prop.Value["items"] as JArray;
                        _output.WriteLine($"  {prop.Name}: {items?.Count ?? 0} items");
                    }
                }
            }
        }
        
        fileExists.Should().BeTrue($"{catalogPath} should exist");
    }

    [Fact]
    public void Step6_AffordableBaseItems_Exist()
    {
        // Arrange
        string category = "weapons";
        int availableBudget = 3; // Component budget for level 1 goblin (70% of 5 = 3.5, rounded to 3)
        
        var catalogPath = $"items/{category}/catalog.json";
        var file = _dataCache.GetFile(catalogPath);
        var weaponTypes = file?.JsonData?["weapon_types"] as JObject;
        var allItems = new List<JToken>();
        
        if (weaponTypes != null)
        {
            foreach (var prop in weaponTypes.Properties())
            {
                var items = prop.Value["items"] as JArray;
                if (items != null) allItems.AddRange(items);
            }
        }
        
        // Act
        var affordableItems = allItems.Where(item =>
        {
            var cost = _budgetCalculator.CalculateMaterialCost(item);
            return _budgetCalculator.CanAfford(availableBudget, cost);
        }).ToList();
        
        // Assert
        _output.WriteLine($"Total items: {allItems.Count}");
        _output.WriteLine($"Budget: {availableBudget}");
        _output.WriteLine($"Affordable items: {affordableItems.Count}");
        
        if (affordableItems.Any())
        {
            _output.WriteLine("Affordable weapons:");
            foreach (var item in affordableItems.Take(5))
            {
                var cost = _budgetCalculator.CalculateMaterialCost(item);
                _output.WriteLine($"  {item["name"]}: calculated cost={cost}, rarityWeight={item["rarityWeight"]}");
            }
        }
        else
        {
            _output.WriteLine("NO AFFORDABLE ITEMS FOUND!");
            _output.WriteLine("Cheapest items:");
            foreach (var item in allItems.OrderBy(i => _budgetCalculator.CalculateMaterialCost(i)).Take(5))
            {
                var cost = _budgetCalculator.CalculateMaterialCost(item);
                _output.WriteLine($"  {item["name"]}: calculated cost={cost}, rarityWeight={item["rarityWeight"]}");
            }
        }
        
        affordableItems.Should().NotBeEmpty($"At least one weapon should be affordable with budget {availableBudget}");
    }

    [Fact]
    public void Step7_PatternFileExists()
    {
        // Arrange
        string category = "weapons";
        var namesPath = $"items/{category}/names.json";
        
        // Act
        var fileExists = _dataCache.FileExists(namesPath);
        
        // Assert
        _output.WriteLine($"Checking: {namesPath}");
        _output.WriteLine($"File exists: {fileExists}");
        
        if (fileExists)
        {
            var file = _dataCache.GetFile(namesPath);
            _output.WriteLine($"File loaded: {file != null}");
            _output.WriteLine($"JsonData exists: {file?.JsonData != null}");
            
            if (file?.JsonData != null)
            {
                var hasPatterns = file.JsonData["patterns"] != null;
                _output.WriteLine($"Has 'patterns' array: {hasPatterns}");
                
                if (hasPatterns)
                {
                    var patterns = file.JsonData["patterns"] as JArray;
                    _output.WriteLine($"Pattern count: {patterns?.Count ?? 0}");
                    
                    if (patterns != null && patterns.Any())
                    {
                        _output.WriteLine("First 3 patterns:");
                        foreach (var pattern in patterns.Take(3))
                        {
                            _output.WriteLine($"  {pattern["pattern"]}: weight={pattern["rarityWeight"]}");
                        }
                    }
                }
            }
        }
        
        fileExists.Should().BeTrue($"{namesPath} should exist");
    }

    [Fact]
    public async Task Step8_EndToEnd_GoblinWeapon()
    {
        // Arrange
        string enemyType = "goblin";
        int level = 1;
        string category = "weapons";
        
        _output.WriteLine($"=== FULL GENERATION TEST: Level {level} {enemyType} {category} ===\n");
        
        // Step 1: Calculate budgets
        var baseBudget = _budgetCalculator.CalculateBaseBudget(level, false, false);
        var typeMultiplier = _materialPoolService.GetBudgetMultiplier(enemyType);
        var adjustedBaseBudget = (int)Math.Round(baseBudget * typeMultiplier);
        
        var materialBudget = _budgetCalculator.CalculateMaterialBudget(adjustedBaseBudget);
        var componentBudget = _budgetCalculator.CalculateComponentBudget(adjustedBaseBudget, materialBudget);
        
        _output.WriteLine($"Step 1 - Budgets:");
        _output.WriteLine($"  Base: {baseBudget}, Adjusted: {adjustedBaseBudget}");
        _output.WriteLine($"  Material (30%): {materialBudget}");
        _output.WriteLine($"  Component (70%): {componentBudget}\n");
        
        // Step 2: Select material (metals for weapons)
        var material = await _materialPoolService.SelectMaterialAsync("metals", materialBudget);
        if (material == null)
        {
            _output.WriteLine("FAILED at Step 2: Material selection returned null\n");
            material.Should().NotBeNull("Material selection failed");
            return;
        }
        
        var materialCost = _budgetCalculator.CalculateMaterialCost(material);
        _output.WriteLine($"Step 2 - Material: {material["name"]} (cost: {materialCost})\n");
        
        // Step 3: Check base item catalog
        var catalogPath = $"items/{category}/catalog.json";
        if (!_dataCache.FileExists(catalogPath))
        {
            _output.WriteLine($"FAILED at Step 3: Catalog file not found at {catalogPath}\n");
            Assert.Fail($"Catalog not found: {catalogPath}");
            return;
        }
        
        var catalogFile = _dataCache.GetFile(catalogPath);
        if (catalogFile?.JsonData == null)
        {
            _output.WriteLine($"FAILED at Step 3: Catalog file loaded but JsonData is null\n");
            Assert.Fail("Catalog JsonData is null");
            return;
        }
        
        _output.WriteLine($"Step 3 - Catalog loaded from {catalogPath}\n");
        
        // Step 4: Find affordable base items
        var weaponTypes = catalogFile.JsonData["weapon_types"] as JObject;
        var allItems = new List<JToken>();
        if (weaponTypes != null)
        {
            foreach (var prop in weaponTypes.Properties())
            {
                var items = prop.Value["items"] as JArray;
                if (items != null) allItems.AddRange(items);
            }
        }
        
        var affordableItems = allItems.Where(item =>
        {
            var cost = _budgetCalculator.CalculateMaterialCost(item);
            return _budgetCalculator.CanAfford(componentBudget, cost);
        }).ToList();
        
        _output.WriteLine($"Step 4 - Base Items: {allItems.Count} total, {affordableItems.Count} affordable");
        if (!affordableItems.Any())
        {
            _output.WriteLine("  NO AFFORDABLE ITEMS - This is the problem!\n");
            affordableItems.Should().NotBeEmpty("Must have at least one affordable weapon");
            return;
        }
        _output.WriteLine("");
        
        // Step 5: Check patterns
        var namesPath = $"items/{category}/names.json";
        if (!_dataCache.FileExists(namesPath))
        {
            _output.WriteLine($"FAILED at Step 5: Names file not found at {namesPath}\n");
            Assert.Fail($"Names file not found: {namesPath}");
            return;
        }
        
        var namesFile = _dataCache.GetFile(namesPath);
        var patterns = namesFile?.JsonData?["patterns"] as JArray;
        if (patterns == null || !patterns.Any())
        {
            _output.WriteLine($"FAILED at Step 5: No patterns found in {namesPath}\n");
            Assert.Fail("No patterns found");
            return;
        }
        
        _output.WriteLine("=== ALL STEPS PASSED ===");
        _output.WriteLine("If generation still fails, the issue is in the generation service logic itself.");
    }
}
