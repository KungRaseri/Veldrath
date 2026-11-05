using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Abstractions;
using RealmEngine.Core.Features.ItemGeneration.Commands;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Services;

namespace RealmEngine.Core.Tests.Features.ItemGeneration.Commands;

/// <summary>
/// Execution-path tests for <see cref="GenerateItemsByCategoryHandler"/> and
/// <see cref="GenerateRandomItemsHandler"/>. The wave-1 suites cover input validation; these tests drive
/// the catalog and budget generation branches end-to-end against a seeded in-memory content database.
/// </summary>
[Trait("Category", "Feature")]
public class ItemGenerationHandlerExecutionTests
{
    // ── Fixture ───────────────────────────────────────────────────────────────

    private sealed class StubDbContextFactory(string databaseName) : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() => CreateDb(databaseName);
    }

    private sealed class ThrowingDbContextFactory : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() =>
            throw new InvalidOperationException("content database unavailable");
    }

    private sealed class EmptyNamePatternRepository : INamePatternRepository
    {
        public Task<IEnumerable<NamePatternSet>> GetAllAsync() =>
            Task.FromResult<IEnumerable<NamePatternSet>>([]);

        public Task<NamePatternSet?> GetByEntityPathAsync(string entityPath) =>
            Task.FromResult<NamePatternSet?>(null);
    }

    private static ContentDbContext CreateDb(string databaseName) =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options);

    private static string Seed(Action<ContentDbContext>? seed = null)
    {
        var databaseName = Guid.NewGuid().ToString();
        using var db = CreateDb(databaseName);
        seed?.Invoke(db);
        db.SaveChanges();
        return databaseName;
    }

    private static void SeedWeapon(ContentDbContext db, string slug = "iron-sword", string typeKey = "weapons") =>
        db.Items.Add(new RealmEngine.Data.Entities.Item
        {
            Slug = slug,
            TypeKey = typeKey,
            ItemType = "weapon",
            DisplayName = "Iron Sword",
            IsActive = true,
            RarityWeight = 100,
            Stats = new ItemStats { Value = 10, Weight = 2f }
        });

    private static async Task<ItemGenerator> CreateItemGeneratorAsync(IDbContextFactory<ContentDbContext> factory)
    {
        var configFactory = new BudgetConfigFactory(new NullGameConfigService(), NullLogger<BudgetConfigFactory>.Instance);
        var calculator = new BudgetCalculator(configFactory.GetBudgetConfig(), NullLogger<BudgetCalculator>.Instance);
        var namePatterns = new NamePatternService(
            new EmptyNamePatternRepository(),
            NullLogger<NamePatternService>.Instance);
        await namePatterns.InitializeAsync();

        var budgetGenerator = new BudgetItemGenerationService(
            calculator,
            new MaterialPoolService(
                factory,
                calculator,
                configFactory.GetMaterialPools(),
                configFactory.GetEnemyTypes(),
                NullLogger<MaterialPoolService>.Instance),
            configFactory,
            namePatterns,
            new ItemDataService(factory, NullLogger<ItemDataService>.Instance),
            NullLogger<BudgetItemGenerationService>.Instance);

        // Enchantment/socket generators are unused by the catalog paths exercised here.
        return new ItemGenerator(
            factory.CreateDbContext(), null!, null!, budgetGenerator, NullLogger<ItemGenerator>.Instance);
    }

    private static async Task<GenerateItemsByCategoryHandler> CreateByCategoryHandlerAsync(string databaseName) =>
        new(await CreateItemGeneratorAsync(new StubDbContextFactory(databaseName)),
            NullLogger<GenerateItemsByCategoryHandler>.Instance);

    private static async Task<GenerateRandomItemsHandler> CreateRandomHandlerAsync(
        string databaseName,
        IDbContextFactory<ContentDbContext>? discoveryFactory = null) =>
        new(await CreateItemGeneratorAsync(new StubDbContextFactory(databaseName)),
            new CategoryDiscoveryService(
                discoveryFactory ?? new StubDbContextFactory(databaseName),
                NullLogger<CategoryDiscoveryService>.Instance),
            NullLogger<GenerateRandomItemsHandler>.Instance);

    // ── GenerateItemsByCategoryHandler ────────────────────────────────────────

    [Fact]
    public async Task ByCategory_Generates_Items_From_The_Category_Catalog()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons", Quantity = 3 }, default);

        result.Success.Should().BeTrue();
        result.Category.Should().Be("weapons");
        result.RequestedQuantity.Should().Be(3);
        result.ActualQuantity.Should().Be(3);
        result.Items.Should().OnlyContain(i => i.Name == "Iron Sword");
        result.Items.Should().OnlyContain(i => i.Slug == "iron-sword");
        result.Items.Should().OnlyContain(i => i.Price == 10);
    }

    [Fact]
    public async Task ByCategory_Generates_A_Named_Item_Repeatedly_When_ItemName_Is_Provided()
    {
        var databaseName = Seed(db => db.Items.AddRange(
            new RealmEngine.Data.Entities.Item
            {
                Slug = "oak-staff",
                TypeKey = "weapons",
                ItemType = "weapon",
                DisplayName = "Oak Staff",
                IsActive = true,
                Stats = new ItemStats { Value = 8 }
            },
            new RealmEngine.Data.Entities.Item
            {
                Slug = "iron-sword",
                TypeKey = "weapons",
                ItemType = "weapon",
                DisplayName = "Iron Sword",
                IsActive = true,
                Stats = new ItemStats { Value = 10 }
            }));
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons", ItemName = "oak-staff", Quantity = 2 },
            default);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => i.Slug == "oak-staff");
        result.Items.Should().OnlyContain(i => i.Name == "Oak Staff");
    }

    [Fact]
    public async Task ByCategory_Returns_Failure_When_A_Named_Item_Cannot_Be_Resolved()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons", ItemName = "missing-relic", Quantity = 2 },
            default);

        result.Success.Should().BeFalse();
        result.Items.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Failed to generate any items");
    }

    [Fact]
    public async Task ByCategory_Returns_Failure_When_The_Category_Catalog_Is_Empty()
    {
        var databaseName = Seed();
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons", Quantity = 2 }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Failed to generate any items");
    }

    [Fact]
    public async Task ByCategory_Uses_Budget_Generation_When_Requested()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand
            {
                Category = "weapons",
                Quantity = 2,
                UseBudgetGeneration = true,
                MinBudget = 50,
                MaxBudget = 100
            },
            default);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Name));
    }

    [Fact]
    public async Task ByCategory_Uses_Budget_Generation_For_A_Specific_Item_Name()
    {
        // Arrange — ItemName takes priority over UseBudgetGeneration, and inside the named branch the
        // budget flag selects the budget pipeline rather than a slug lookup.
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand
            {
                Category = "weapons",
                ItemName = "iron-sword",
                Quantity = 1,
                UseBudgetGeneration = true,
                MinBudget = 25,
                MaxBudget = 30
            },
            default);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(1);
        result.Items[0].Name.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task ByCategory_Skips_Items_That_Throw_And_Reports_No_Results()
    {
        // Arrange — an inverted budget range makes every random draw throw, exercising the per-item guard.
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand
            {
                Category = "weapons",
                Quantity = 2,
                UseBudgetGeneration = true,
                MinBudget = 100,
                MaxBudget = 10
            },
            default);

        result.Success.Should().BeFalse();
        result.Items.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Failed to generate any items");
    }

    [Fact]
    public async Task ByCategory_Skips_A_Named_Budget_Item_That_Throws_And_Reports_No_Results()
    {
        // Arrange — ItemName + budget generation: the inverted range makes each named draw throw.
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateByCategoryHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand
            {
                Category = "weapons",
                ItemName = "iron-sword",
                Quantity = 2,
                UseBudgetGeneration = true,
                MinBudget = 100,
                MaxBudget = 10
            },
            default);

        result.Success.Should().BeFalse();
        result.Items.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Failed to generate any items");
    }

    [Fact]
    public async Task ByCategory_Reports_An_Unexpected_Failure_Instead_Of_Propagating_It()
    {
        // Arrange — the success log throws, which exercises the handler's error boundary.
        var databaseName = Seed(db => SeedWeapon(db));
        var generator = await CreateItemGeneratorAsync(new StubDbContextFactory(databaseName));
        var handler = new GenerateItemsByCategoryHandler(
            generator, ThrowingInformationLogger<GenerateItemsByCategoryHandler>.Instance);

        // Act
        var result = await handler.Handle(
            new GenerateItemsByCategoryCommand { Category = "weapons", Quantity = 1 }, default);

        // Assert
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("Generation failed: ").And.Contain("log sink unavailable");
    }

    // ── GenerateRandomItemsHandler ────────────────────────────────────────────

    [Fact]
    public async Task Random_Rejects_A_Quantity_Above_The_Hard_Cap()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "weapons", Quantity = 1001 }, default);

        result.Success.Should().BeFalse();
        result.Items.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Quantity must not exceed 1000");
    }

    [Fact]
    public async Task Random_Treats_A_Wildcard_In_The_Legacy_Category_Field_As_A_Pattern()
    {
        var databaseName = Seed(db =>
        {
            SeedWeapon(db);
            SeedWeapon(db, "steel-axe", "weapons/axes");
        });
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "weapons/*", Quantity = 2, UseBudgetGeneration = false },
            default);

        result.Success.Should().BeTrue();
        result.CategoriesUsed.Should().Equal("weapons/axes");
        result.Items.Should().OnlyContain(i => i.Slug == "steel-axe");
    }

    [Fact]
    public async Task Random_Generates_From_The_Catalog_When_Budget_Generation_Is_Off()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "weapons", Quantity = 2, UseBudgetGeneration = false },
            default);

        result.Success.Should().BeTrue();
        result.CategoriesUsed.Should().Equal("weapons");
        result.RequestedQuantity.Should().Be(2);
        result.ActualQuantity.Should().Be(2);
        result.Items.Should().OnlyContain(i => i.Slug == "iron-sword");
    }

    [Fact]
    public async Task Random_Uses_Every_Discovered_Category_When_The_Category_Is_Random()
    {
        // Arrange — both discovered categories (an item TypeKey and a material TypeKey) resolve to items,
        // so every draw in the batch produces one regardless of which category is picked.
        var databaseName = Seed(db =>
        {
            SeedWeapon(db);
            db.Items.Add(new RealmEngine.Data.Entities.Item
            {
                Slug = "iron-ingot",
                TypeKey = "metals",
                ItemType = "material",
                DisplayName = "Iron Ingot",
                IsActive = true,
                RarityWeight = 100,
                Stats = new ItemStats { Value = 3 }
            });
            db.Materials.Add(new Material
            {
                Slug = "iron",
                DisplayName = "Iron",
                TypeKey = "metals",
                MaterialFamily = "metal",
                CostScale = 1.0f,
                RarityWeight = 100,
                IsActive = true
            });
        });
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "random", Quantity = 10, UseBudgetGeneration = false },
            default);

        result.CategoriesUsed.Should().BeEquivalentTo(["metals", "weapons"]);
        result.Success.Should().BeTrue();
        result.ActualQuantity.Should().Be(10);
        result.Items.Should().OnlyContain(i => i.Slug == "iron-sword" || i.Slug == "iron-ingot");
    }

    [Fact]
    public async Task Random_Uses_The_Category_Pattern_With_Wildcards()
    {
        var databaseName = Seed(db =>
        {
            SeedWeapon(db);
            SeedWeapon(db, "steel-axe", "weapons/axes");
            db.Materials.Add(new Material
            {
                Slug = "iron",
                DisplayName = "Iron",
                TypeKey = "metals",
                MaterialFamily = "metal",
                CostScale = 1.0f,
                RarityWeight = 100,
                IsActive = true
            });
        });
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand
            {
                CategoryPattern = "weapons*",
                Quantity = 3,
                UseBudgetGeneration = false
            },
            default);

        result.Success.Should().BeTrue();
        result.CategoriesUsed.Should().BeEquivalentTo(["weapons", "weapons/axes"]);
        result.Items.Should().OnlyContain(i => i.Slug == "iron-sword" || i.Slug == "steel-axe");
    }

    [Fact]
    public async Task Random_Returns_Failure_When_The_Pattern_Matches_Nothing()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { CategoryPattern = "artifacts/*", Quantity = 1 }, default);

        result.Success.Should().BeFalse();
        result.CategoriesUsed.Should().BeEmpty();
        result.ErrorMessage.Should().Be("No valid categories found for 'artifacts/*'");
    }

    [Fact]
    public async Task Random_Expands_A_Parent_Category_Into_Its_Leaf_Categories()
    {
        var databaseName = Seed(db => SeedWeapon(db, "steel-axe", "weapons/axes"));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "weapons", Quantity = 2, UseBudgetGeneration = false },
            default);

        result.Success.Should().BeTrue();
        result.CategoriesUsed.Should().Equal("weapons/axes");
        result.Items.Should().OnlyContain(i => i.Slug == "steel-axe");
    }

    [Fact]
    public async Task Random_Returns_Failure_When_The_Category_Is_Unknown()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "ships", Quantity = 1 }, default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No valid categories found for 'ships'");
    }

    [Fact]
    public async Task Random_Returns_Failure_When_No_Item_Can_Be_Produced()
    {
        // Arrange — the only discovered category comes from materials, which has no item rows.
        var databaseName = Seed(db => db.Materials.Add(new Material
        {
            Slug = "iron",
            DisplayName = "Iron",
            TypeKey = "metals",
            MaterialFamily = "metal",
            CostScale = 1.0f,
            RarityWeight = 100,
            IsActive = true
        }));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "metals", Quantity = 2, UseBudgetGeneration = false },
            default);

        result.Success.Should().BeFalse();
        result.Items.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Failed to generate any items");
    }

    [Fact]
    public async Task Random_Uses_Budget_Generation_When_Requested()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand
            {
                Category = "weapons",
                Quantity = 2,
                UseBudgetGeneration = true,
                MinBudget = 50,
                MaxBudget = 90
            },
            default);

        result.Success.Should().BeTrue();
        result.Items.Should().HaveCount(2);
        result.Items.Should().OnlyContain(i => !string.IsNullOrWhiteSpace(i.Name));
    }

    [Fact]
    public async Task Random_Skips_Budget_Items_That_Throw_And_Reports_No_Results()
    {
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateRandomHandlerAsync(databaseName);

        var result = await handler.Handle(
            new GenerateRandomItemsCommand
            {
                Category = "weapons",
                Quantity = 2,
                UseBudgetGeneration = true,
                MinBudget = 100,
                MaxBudget = 10
            },
            default);

        result.Success.Should().BeFalse();
        result.Items.Should().BeEmpty();
        result.ErrorMessage.Should().Be("Failed to generate any items");
    }

    [Fact]
    public async Task Random_Returns_Failure_When_Category_Discovery_Throws()
    {
        // Arrange — category discovery cannot open a content context, so the handler reports the failure.
        var databaseName = Seed(db => SeedWeapon(db));
        var handler = await CreateRandomHandlerAsync(databaseName, new ThrowingDbContextFactory());

        var result = await handler.Handle(
            new GenerateRandomItemsCommand { Category = "random", Quantity = 1, UseBudgetGeneration = false },
            default);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().StartWith("Generation failed: ");
        result.ErrorMessage.Should().Contain("content database unavailable");
    }
}
