using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Tests.Services;

/// <summary>
/// Tests for <see cref="BudgetItemGenerationService"/>: budget allocation, base-item and material
/// selection, quality/pattern/component spending, the rarity tiers and the minimal-item fallback.
/// All collaborators are driven from in-memory EF content (a fresh context per factory call, because
/// the pipeline disposes what it creates) and stub configuration, so every arm of the forward-building
/// pipeline is reachable and deterministic.
/// </summary>
[Trait("Category", "Services")]
public class BudgetItemGenerationServiceTests
{
    // ── Stubs ─────────────────────────────────────────────────────────────────

    private sealed class StubConfigService : GameConfigService
    {
        public Dictionary<string, string?> Entries { get; } = new(StringComparer.Ordinal);

        public override string? GetData(string key) =>
            Entries.TryGetValue(key, out var value) ? value : null;

        public void Set<T>(string key, T value) => Entries[key] = JsonSerializer.Serialize(value);
    }

    private sealed class StubNamePatternRepository(IReadOnlyList<NamePatternSet> sets) : INamePatternRepository
    {
        public Task<IEnumerable<NamePatternSet>> GetAllAsync() =>
            Task.FromResult<IEnumerable<NamePatternSet>>(sets);

        public Task<NamePatternSet?> GetByEntityPathAsync(string entityPath) =>
            Task.FromResult(sets.FirstOrDefault(s => s.EntityPath == entityPath));
    }

    private sealed class StubDbContextFactory(string databaseName) : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() => CreateDb(databaseName);
    }

    // ── Fixtures ──────────────────────────────────────────────────────────────

    private static ContentDbContext CreateDb(string databaseName) =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    /// <summary>Seeds a fresh in-memory content database and returns its name.</summary>
    private static string Seed(Action<ContentDbContext>? seed = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = CreateDb(databaseName);
        seed?.Invoke(db);
        db.SaveChanges();
        return databaseName;
    }

    private static void SeedWeapon(ContentDbContext db, string slug = "iron-sword", int rarityWeight = 100) =>
        db.Items.Add(new RealmEngine.Data.Entities.Item
        {
            Slug = slug,
            TypeKey = "weapons",
            ItemType = "weapon",
            WeaponType = "sword",
            IsActive = true,
            RarityWeight = rarityWeight
        });

    private static void SeedMaterial(ContentDbContext db, string slug = "iron", int rarityWeight = 100) =>
        db.Materials.Add(new Material
        {
            Slug = slug,
            DisplayName = "Iron",
            MaterialFamily = "metal",
            CostScale = 1.0f,
            RarityWeight = rarityWeight,
            IsActive = true
        });

    /// <summary>Seeds a weapon, a material and returns the database name.</summary>
    private static string SeedStandardContent() => Seed(db =>
    {
        SeedWeapon(db);
        SeedMaterial(db);
    });

    private static StubConfigService BaseConfig(
        double budgetMultiplier = 2.0,
        double? materialPercentage = 0.5,
        string[]? allowedMaterials = null,
        int patternCost = 2)
    {
        var config = new StubConfigService();
        config.Set("budget-config", new BudgetConfig
        {
            PatternCosts = new Dictionary<string, int> { ["{prefix} {base}"] = patternCost }
        });
        config.Set("enemy-types", new EnemyTypes
        {
            Types = new Dictionary<string, EnemyTypeConfig>
            {
                ["humanoid"] = new() { BudgetMultiplier = budgetMultiplier, MaterialPercentage = materialPercentage }
            }
        });
        config.Set("material-pools", new MaterialPools
        {
            Pools = new Dictionary<string, MaterialPool>
            {
                ["metals"] = new()
                {
                    Common = [new MaterialReference { ItemRef = "@items/materials/metals:iron", RarityWeight = 50 }]
                }
            }
        });

        var materials = allowedMaterials ?? ["metals"];
        config.Set("material-filters", new MaterialFilterConfig
        {
            Categories = new Dictionary<string, CategoryMaterialFilter>
            {
                ["weapons"] = new()
                {
                    DefaultMaterials = materials.ToList(),
                    Types = new Dictionary<string, TypeMaterialFilter>
                    {
                        ["sword"] = new() { AllowedMaterials = materials.ToList() }
                    }
                }
            },
            Defaults = new Dictionary<string, DefaultMaterialFilter>
            {
                ["unknown"] = new() { AllowedMaterials = materials.ToList() }
            }
        });

        return config;
    }

    private static NamePatternSet WeaponPatterns() => new()
    {
        EntityPath = "items/weapons",
        Patterns = [new NamePattern { Template = "{prefix} {base}", RarityWeight = 10 }],
        Components =
        [
            new RealmEngine.Data.Entities.NameComponent { ComponentKey = "prefix", Value = "Rusted", RarityWeight = 100 },
            new RealmEngine.Data.Entities.NameComponent { ComponentKey = "base", Value = "Sword", RarityWeight = 100 }
        ]
    };

    private static NamePatternSet MaterialPatterns(bool includeQuality = true, string? qualityKey = "quality") => new()
    {
        EntityPath = "items/materials",
        Patterns = [new NamePattern { Template = "{base}", RarityWeight = 10 }],
        Components = includeQuality
            ?
            [
                new RealmEngine.Data.Entities.NameComponent
                {
                    ComponentKey = qualityKey!,
                    Value = "Masterwork",
                    RarityWeight = 100,
                    BudgetModifier = 0.4
                }
            ]
            : []
    };

    private static async Task<BudgetItemGenerationService> CreateAsync(
        string databaseName,
        StubConfigService config,
        IReadOnlyList<NamePatternSet>? patternSets = null)
    {
        var factory = new StubDbContextFactory(databaseName);
        var configFactory = new BudgetConfigFactory(config, NullLogger<BudgetConfigFactory>.Instance);
        var calculator = new BudgetCalculator(configFactory.GetBudgetConfig(), NullLogger<BudgetCalculator>.Instance);
        var patternService = new NamePatternService(
            new StubNamePatternRepository(patternSets ?? [WeaponPatterns(), MaterialPatterns()]),
            NullLogger<NamePatternService>.Instance);
        await patternService.InitializeAsync();

        return new BudgetItemGenerationService(
            calculator,
            new MaterialPoolService(
                factory,
                calculator,
                configFactory.GetMaterialPools(),
                configFactory.GetEnemyTypes(),
                NullLogger<MaterialPoolService>.Instance,
                FakeRandomSource.Returning(new int[32])),
            configFactory,
            patternService,
            new ItemDataService(factory, NullLogger<ItemDataService>.Instance),
            NullLogger<BudgetItemGenerationService>.Instance);
    }

    private static BudgetItemRequest WeaponRequest(int level = 30) => new()
    {
        EnemyType = "humanoid",
        EnemyLevel = level,
        ItemCategory = "weapons",
        AllowQuality = true
    };

    // ── Happy path ────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateItemAsync_Should_Spend_The_Budget_Across_Material_Quality_Pattern_And_Components()
    {
        // Arrange — level 30 * 5 * 2.0 = 300 budget, half of it for the material.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig());

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.BaseBudget.Should().Be(300);
        result.AdjustedBudget.Should().Be(300);
        result.MaterialBudget.Should().Be(150);
        result.ComponentBudget.Should().Be(150);

        result.BaseItem.Should().NotBeNull();
        result.BaseItem!.Slug.Should().Be("iron-sword");
        result.BaseItemCost.Should().Be(60); // 6000 / 100 rarity weight

        result.Material.Should().NotBeNull();
        result.Material!.Slug.Should().Be("iron");
        result.MaterialCost.Should().Be(60);

        result.Quality.Should().NotBeNull();
        result.Quality!.Value.Should().Be("Masterwork");
        result.QualityModifier.Should().Be(0.4);

        result.Pattern.Should().Be("{prefix} {base}");
        result.PatternCost.Should().Be(2);

        result.Components.Should().ContainSingle().Which.Value.Should().Be("Rusted");
        result.ComponentCosts.Should().ContainKey("Rusted").WhoseValue.Should().Be(1);

        // 60 material + 60 base item + 2 quality + 2 pattern + 1 prefix.
        result.SpentBudget.Should().Be(125);
        result.Sockets.Should().BeEmpty();
    }

    [Theory]
    [InlineData(1, 10)]     // 1 * 5 * 2.0
    [InlineData(10, 100)]   // 10 * 5 * 2.0
    [InlineData(30, 300)]   // 30 * 5 * 2.0
    [InlineData(50, 500)]   // 50 * 5 * 2.0
    public async Task GenerateItemAsync_Should_Scale_The_Base_Budget_With_The_Enemy_Level(int level, int expectedBudget)
    {
        // Arrange
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig());

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest(level));

        // Assert
        result.BaseBudget.Should().Be(expectedBudget);
        result.AdjustedBudget.Should().Be(expectedBudget);
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Fall_Back_To_A_Unit_Multiplier_For_Unknown_Enemy_Types()
    {
        // Arrange — "dragon" is not in the enemy-type config.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig());
        var request = WeaponRequest(level: 20);
        request.EnemyType = "dragon";

        // Act
        var result = await service.GenerateItemAsync(request);

        // Assert — 20 * 5 * 1.0, and the default 30% material share.
        result.BaseBudget.Should().Be(100);
        result.MaterialBudget.Should().Be(30);
    }

    [Theory]
    [InlineData(false, false, 50)]  // 10 * 5 * 1.0
    [InlineData(true, false, 75)]   // elite: 50 * 1.5
    [InlineData(false, true, 125)]  // boss: 50 * 2.5
    public async Task GenerateItemAsync_Should_Apply_Boss_And_Elite_Budgets(
        bool isElite,
        bool isBoss,
        int expectedBudget)
    {
        // Arrange
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig(budgetMultiplier: 1.0, materialPercentage: null));
        var request = WeaponRequest(level: 10);
        request.IsElite = isElite;
        request.IsBoss = isBoss;

        // Act
        var result = await service.GenerateItemAsync(request);

        // Assert
        result.BaseBudget.Should().Be(expectedBudget);
    }

    // ── Base item selection ───────────────────────────────────────────────────

    [Fact]
    public async Task GenerateItemAsync_Should_Use_A_Fallback_Item_When_The_Catalog_Is_Empty()
    {
        // Arrange — no items seeded for "weapons".
        var databaseName = Seed(db => SeedMaterial(db));
        var service = await CreateAsync(databaseName, BaseConfig());

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.BaseItem.Should().NotBeNull();
        result.BaseItem!.Name.Should().Be("Basic Item");
        result.BaseItem.Slug.Should().Be("basic-item");
        result.BaseItemCost.Should().Be(60);
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Pick_The_Cheapest_Base_Item()
    {
        // Arrange — two candidates; the higher-weight one costs less (6000 / weight).
        var databaseName = Seed(db =>
        {
            SeedWeapon(db, "common-sword", rarityWeight: 50);   // cost 120
            SeedWeapon(db, "fine-sword", rarityWeight: 200);    // cost 30
            SeedMaterial(db);
        });
        var service = await CreateAsync(databaseName, BaseConfig());

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.BaseItem!.Slug.Should().Be("fine-sword");
        result.BaseItemCost.Should().Be(30);
    }

    // ── Material selection ────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateItemAsync_Should_Use_The_Cheapest_Material_When_None_Is_Affordable()
    {
        // Arrange — level 1 leaves a 2-credit material budget against a 60-credit material.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig(budgetMultiplier: 1.0, materialPercentage: null));

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest(level: 1));

        // Assert
        result.MaterialBudget.Should().Be(2);
        result.Material.Should().NotBeNull();
        result.Material!.Slug.Should().Be("iron");
        result.MaterialCost.Should().Be(60);
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Fall_Through_To_A_Later_Material_Type()
    {
        // Arrange — the first allowed pool does not exist.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig(allowedMaterials: ["mystery", "metals"]));

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.Material.Should().NotBeNull();
        result.Material!.Slug.Should().Be("iron");
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Survive_When_No_Material_Can_Be_Resolved()
    {
        // Arrange — the only allowed pool is missing, so the fallback lookup fails too.
        var databaseName = Seed(db => SeedWeapon(db));
        var service = await CreateAsync(databaseName, BaseConfig(allowedMaterials: ["mystery"]));

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.Material.Should().BeNull();
        result.MaterialCost.Should().Be(0);
        result.BaseItem.Should().NotBeNull();
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Skip_Material_Selection_When_No_Types_Are_Configured()
    {
        // Arrange — empty material filters leave no material type to try.
        var databaseName = SeedStandardContent();
        var config = BaseConfig();
        config.Set("material-filters", new MaterialFilterConfig());

        var service = await CreateAsync(databaseName, config);

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.Material.Should().BeNull();
        result.MaterialCost.Should().Be(0);
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Use_Default_Materials_For_A_Category_Without_Type_Rules()
    {
        // Arrange — category "accessories" exists but has no per-type entry.
        var databaseName = SeedStandardContent();
        var config = BaseConfig();
        config.Set("material-filters", new MaterialFilterConfig
        {
            Categories = new Dictionary<string, CategoryMaterialFilter>
            {
                ["accessories"] = new() { DefaultMaterials = ["metals"] }
            }
        });
        var service = await CreateAsync(databaseName, config);
        var request = WeaponRequest();
        request.ItemCategory = "accessories";

        // Act
        var result = await service.GenerateItemAsync(request);

        // Assert
        result.Material.Should().NotBeNull();
        result.Material!.Slug.Should().Be("iron");
    }

    // ── Quality ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateItemAsync_Should_Skip_Quality_When_It_Is_Not_Allowed()
    {
        // Arrange
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig());
        var request = WeaponRequest();
        request.AllowQuality = false;

        // Act
        var result = await service.GenerateItemAsync(request);

        // Assert
        result.Quality.Should().BeNull();
        result.QualityModifier.Should().Be(0);
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Skip_Quality_When_The_Materials_Set_Is_Missing()
    {
        // Arrange — only the weapon pattern set is registered.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig(), [WeaponPatterns()]);

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.Quality.Should().BeNull();
        result.Pattern.Should().Be("{prefix} {base}");
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Skip_Quality_When_The_Pool_Has_No_Quality_Components()
    {
        // Arrange
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig(), [WeaponPatterns(), MaterialPatterns(includeQuality: false)]);

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.Quality.Should().BeNull();
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Skip_Quality_When_Every_Option_Is_Too_Expensive()
    {
        // Arrange — a level-6 budget leaves a 30-credit remainder against a 150-credit quality pool.
        var databaseName = SeedStandardContent();
        var qualitySet = MaterialPatterns();
        qualitySet.Components.First().RarityWeight = 1; // 150 / 1 = 150 credits

        var service = await CreateAsync(databaseName, BaseConfig(), [WeaponPatterns(), qualitySet]);

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest(level: 6));

        // Assert
        result.Quality.Should().BeNull();
    }

    // ── Patterns and components ───────────────────────────────────────────────

    [Fact]
    public async Task GenerateItemAsync_Should_Default_To_A_Base_Only_Pattern_When_No_Set_Exists()
    {
        // Arrange — neither set is registered.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig(), []);

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.Pattern.Should().Be("{base}");
        result.PatternCost.Should().Be(0);
        result.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Skip_Prefix_Components_For_Low_Rarity_Tiers()
    {
        // Arrange — a level-5 budget (50) maps to the "uncommon" tier, which allows no prefixes.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(databaseName, BaseConfig(budgetMultiplier: 1.0, materialPercentage: null));

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest(level: 5));

        // Assert
        result.Pattern.Should().Be("{prefix} {base}");
        result.Components.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateItemAsync_Should_Ignore_Tokens_With_No_Component_Pool()
    {
        // Arrange — the pattern references a token the set never defines.
        var databaseName = SeedStandardContent();
        var patternSet = WeaponPatterns();
        patternSet.Patterns = [new NamePattern { Template = "{suffix} {base}", RarityWeight = 10 }];

        var service = await CreateAsync(databaseName, BaseConfig(), [patternSet, MaterialPatterns()]);

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.Pattern.Should().Be("{suffix} {base}");
        result.Components.Should().BeEmpty();
    }

    // ── Failure fallback ──────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateItemAsync_Should_Return_A_Minimal_Item_When_Generation_Throws()
    {
        // Arrange — a null component key makes the quality lookup throw inside the pipeline.
        var databaseName = SeedStandardContent();
        var service = await CreateAsync(
            databaseName,
            BaseConfig(),
            [WeaponPatterns(), MaterialPatterns(qualityKey: null)]);

        // Act
        var result = await service.GenerateItemAsync(WeaponRequest());

        // Assert
        result.BaseBudget.Should().Be(10);
        result.AdjustedBudget.Should().Be(10);
        result.MaterialBudget.Should().Be(5);
        result.ComponentBudget.Should().Be(5);
        result.SpentBudget.Should().Be(10);
        result.Material.Should().BeNull();
        result.MaterialCost.Should().Be(5);
        result.Quality.Should().BeNull();
        result.QualityModifier.Should().Be(0);
        result.BaseItem!.Name.Should().Be("Sword");
        result.BaseItem.Slug.Should().Be("sword");
        result.BaseItemCost.Should().Be(5);
        result.Pattern.Should().Be("{base}");
        result.PatternCost.Should().Be(0);
        result.Components.Should().BeEmpty();
        result.ComponentCosts.Should().BeEmpty();
    }
}
