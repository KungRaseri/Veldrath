using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Core.Generators.Modern;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Tests.Infrastructure;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Tests.Features.Crafting;

/// <summary>
/// Tests for the crafting execution path (<see cref="CraftingService.CraftItemAsync"/>),
/// item construction and material bookkeeping. The success, critical and failure rolls all come from
/// the injected <see cref="IRandomSource"/>, so each outcome arm is scripted instead of sampled.
/// </summary>
[Trait("Category", "Service")]
public class CraftingServiceCraftingTests
{
    private const string RecipeId = "iron-sword";
    private const string MaterialRef = "iron-ingot";

    private static CraftingService CreateService(
        BudgetHelperService? budgetHelper = null,
        ItemGenerator? itemGenerator = null,
        IRandomSource? randomSource = null) =>
        new(
            new RecipeDataService(new InMemoryRecipeRepository(), NullLogger<RecipeDataService>.Instance),
            NullLogger<CraftingService>.Instance,
            budgetHelper,
            itemGenerator,
            randomSource);

    private static BudgetHelperService CreateBudgetHelper() =>
        new(
            new BudgetCalculator(new BudgetConfig(), NullLogger<BudgetCalculator>.Instance),
            NullLogger<BudgetHelperService>.Instance);

    private static Character CreateCharacter(int skillLevel, int materialCount = 2)
    {
        var character = new Character
        {
            Name = "Smith",
            Skills = new Dictionary<string, CharacterSkill>
            {
                ["Blacksmithing"] = new() { SkillId = "blacksmithing", Name = "Blacksmithing", CurrentRank = skillLevel }
            },
            LearnedRecipes = []
        };
        character.Inventory = Enumerable.Range(0, materialCount)
            .Select(_ => new Item { Id = MaterialRef, Name = "Iron Ingot" })
            .ToList();
        return character;
    }

    private static Recipe CreateRecipe(
        string station = "anvil",
        int requiredLevel = 10,
        int materialQuantity = 2,
        string materialReference = MaterialRef,
        string category = "weapons",
        string output = "test-output",
        RecipeUnlockMethod unlockMethod = RecipeUnlockMethod.Default,
        int minQuality = 0,
        int maxQuality = 2) => new()
        {
            Id = RecipeId,
            Name = "Iron Sword",
            Category = category,
            RequiredStation = station,
            RequiredSkillLevel = requiredLevel,
            UnlockMethod = unlockMethod,
            Materials = [new RecipeMaterial { ItemReference = materialReference, Quantity = materialQuantity }],
            OutputItemReference = output,
            MinQuality = (ItemRarity)minQuality,
            MaxQuality = (ItemRarity)maxQuality
        };

    // ── Guard branches ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CraftItemAsync_With_Insufficient_Skill_Returns_Failure_Without_Consuming_Materials()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 1);
        var recipe = CreateRecipe(requiredLevel: 10);

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Success.Should().BeFalse();
        result.Item.Should().BeNull();
        result.FailureReason.Should().Contain("Requires Blacksmithing skill level 10 (current: 1)");
        character.Inventory.Should().HaveCount(2);
    }

    [Fact]
    public async Task CraftItemAsync_With_Locked_Recipe_Returns_Failure()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 20);
        var recipe = CreateRecipe(unlockMethod: RecipeUnlockMethod.Trainer);

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("is not unlocked");
        character.Inventory.Should().HaveCount(2);
    }

    [Fact]
    public async Task CraftItemAsync_With_Missing_Materials_Returns_Failure()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 20, materialCount: 1);
        var recipe = CreateRecipe(requiredLevel: 10, materialQuantity: 5);

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Success.Should().BeFalse();
        result.FailureReason.Should().Contain("need 5, have 1");
        character.Inventory.Should().HaveCount(1);
    }

    [Fact]
    public async Task CraftItemAsync_When_Materials_Are_Not_Consumed_Leaves_The_Inventory_Intact()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 20);
        var recipe = CreateRecipe();

        // Act
        var result = await service.CraftItemAsync(character, recipe, consumeMaterials: false);

        // Assert
        result.Success.Should().BeTrue();
        character.Inventory.Should().HaveCount(2);
        result.RefundedMaterials.Should().BeEmpty();
    }

    // ── Successful craft shape ─────────────────────────────────────────────────

    [Fact]
    public async Task CraftItemAsync_Should_Build_The_Item_From_The_Recipe_When_No_Generator_Is_Available()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 150); // success chance clamps to 99%
        var recipe = CreateRecipe();

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Success.Should().BeTrue();
        result.Item.Should().NotBeNull();
        result.Item!.Id.Should().Be("test-output");
        result.Item.Name.Should().Be("Iron Sword");
        result.Item.Description.Should().Be("Crafted Iron Sword");
        result.Item.Type.Should().Be(ItemType.Weapon);
        result.Item.Price.Should().Be(20); // 2 materials * 10
        result.Item.StackSize.Should().Be(1);
        result.Item.IsStackable.Should().BeFalse();
        ((int)result.ActualQuality).Should().BeGreaterThanOrEqualTo((int)recipe.MinQuality);
        ((int)result.ActualQuality).Should().BeLessThanOrEqualTo((int)recipe.MaxQuality);
    }

    [Theory]
    [InlineData("weapons", ItemType.Weapon)]
    [InlineData("armor", ItemType.Chest)]
    [InlineData("shields", ItemType.Shield)]
    [InlineData("consumables", ItemType.Consumable)]
    [InlineData("trinkets", ItemType.Consumable)]
    public async Task CraftItemAsync_Should_Map_The_Recipe_Category_To_An_Item_Type(string category, ItemType expected)
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 150);
        var recipe = CreateRecipe(category: category);

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Item!.Type.Should().Be(expected);
    }

    [Fact]
    public async Task CraftItemAsync_Should_Clamp_Quality_To_The_Recipe_Range()
    {
        // Arrange — skill 150 vs requirement 10 caps both rolls; a critical success would earn
        // +14 tiers, so only the recipe's own ceiling keeps the result at Epic.
        var service = CreateService(randomSource: FakeRandomSource.Returning(1, 1));
        var recipe = CreateRecipe(minQuality: 2, maxQuality: 3);

        // Act
        var result = await service.CraftItemAsync(CreateCharacter(skillLevel: 150), recipe);

        // Assert
        result.WasCritical.Should().BeTrue();
        result.QualityBonus.Should().Be(3, "the bonus is capped at three tiers before the range clamp");
        result.ActualQuality.Should().Be(ItemRarity.Epic, "min quality 2 + 3 tiers clamps to max quality 3");
        ((int)result.Item!.Rarity).Should().Be((int)result.ActualQuality);
    }

    // ── Outcome arms (scripted through the injected random source) ──────────────

    [Fact]
    public async Task CraftItemAsync_Critical_Success_Adds_The_Extra_Tier_And_Consumes_Materials()
    {
        // Arrange — equal skill and requirement: 50% success and 5% critical. Both rolls land.
        var random = FakeRandomSource.Returning(1, 5);
        var service = CreateService(randomSource: random);
        var character = CreateCharacter(skillLevel: 10);

        // Act
        var result = await service.CraftItemAsync(character, CreateRecipe(requiredLevel: 10));

        // Assert
        result.Success.Should().BeTrue();
        result.WasCritical.Should().BeTrue();
        result.FailureSeverity.Should().Be(0);
        result.QualityBonus.Should().Be(1, "no skill margin, plus one tier for the critical");
        result.ActualQuality.Should().Be(ItemRarity.Uncommon);
        result.RefundedMaterials.Should().BeEmpty();
        character.Inventory.Should().BeEmpty("a successful craft consumes both ingots");
        random.RemainingInts.Should().Be(0, "success and critical are the only rolls this path makes");
    }

    [Theory]
    [InlineData(60, 1, 1, "marginal: 10 points past a 50% chance")]
    [InlineData(75, 1, 2, "moderate: 25 points past a 50% chance")]
    public async Task CraftItemAsync_Failed_Roll_Degrades_Quality_Without_Refunding(
        int successRoll,
        int criticalRoll,
        int expectedSeverity,
        string reason)
    {
        // Arrange — equal skill and requirement: 50% success, so 60 and 75 both fail.
        var service = CreateService(randomSource: FakeRandomSource.Returning(successRoll, criticalRoll));
        var character = CreateCharacter(skillLevel: 10);

        // Act
        var result = await service.CraftItemAsync(character, CreateRecipe(requiredLevel: 10));

        // Assert — the craft still produces an item, but at the bottom of the recipe's range.
        result.Success.Should().BeTrue("a failed roll never destroys the craft");
        result.WasCritical.Should().BeFalse();
        result.FailureSeverity.Should().Be(expectedSeverity, reason);
        result.QualityBonus.Should().Be(0, "the zero skill margin is already the floor");
        result.ActualQuality.Should().Be(ItemRarity.Common);
        result.RefundedMaterials.Should().BeEmpty();
        character.Inventory.Should().BeEmpty("severity 1 and 2 consume every material");
    }

    [Fact]
    public async Task CraftItemAsync_Critical_Failure_Refunds_Half_The_Materials()
    {
        // Arrange — 41 points past the 50% chance is severity 3, which refunds 50% of each material.
        var service = CreateService(randomSource: FakeRandomSource.Returning(91, 1));
        var character = CreateCharacter(skillLevel: 10);

        // Act
        var result = await service.CraftItemAsync(
            character, CreateRecipe(requiredLevel: 10, materialQuantity: 2));

        // Assert — two ingots consumed as one, one refunded.
        result.Success.Should().BeTrue();
        result.FailureSeverity.Should().Be(3);
        result.RefundedMaterials.Should().ContainSingle().Which.Should().Be((MaterialRef, 1));
        character.Inventory.Should().ContainSingle()
            .Which.Id.Should().Be(MaterialRef, "only the consumed half of the stack is removed");
    }

    [Fact]
    public async Task CraftItemAsync_With_A_Budget_Helper_Should_Use_Its_Chance_And_Failure_Tables()
    {
        // Arrange — 15 skill over the requirement: 80% success, 6% critical, 1 tier of quality.
        // Rolls: 50/5 is a critical success, 90/1 is a marginal failure (10 points past 80%).
        var service = CreateService(
            CreateBudgetHelper(),
            randomSource: FakeRandomSource.Returning(50, 5, 90, 1));
        var recipe = CreateRecipe(requiredLevel: 10);

        // Act
        var critical = await service.CraftItemAsync(CreateCharacter(skillLevel: 25), recipe);
        var failed = await service.CraftItemAsync(CreateCharacter(skillLevel: 25), recipe);

        // Assert
        critical.Success.Should().BeTrue();
        critical.WasCritical.Should().BeTrue();
        critical.QualityBonus.Should().Be(2, "one tier for the skill margin plus one for the critical");
        critical.ActualQuality.Should().Be(ItemRarity.Rare);

        failed.FailureSeverity.Should().Be(1);
        failed.WasCritical.Should().BeFalse();
        failed.QualityBonus.Should().Be(0, "the marginal failure strips the single skill tier");
        failed.ActualQuality.Should().Be(ItemRarity.Common);
    }

    // ── Item generator integration ─────────────────────────────────────────────

    private static ContentDbContext CreateDb(string name) =>
        new(new DbContextOptionsBuilder<ContentDbContext>().UseInMemoryDatabase(name).Options);

    private static ItemGenerator CreateItemGenerator(ContentDbContext db, bool seedItem = true)
    {
        if (seedItem)
        {
            db.Items.Add(new RealmEngine.Data.Entities.Item
            {
                Slug = "iron-longsword",
                TypeKey = "swords",
                ItemType = "weapon",
                WeaponType = "sword",
                IsActive = true,
                RarityWeight = 80
            });
            db.SaveChanges();
        }

        return new ItemGenerator(
            db,
            new EnchantmentGenerator(db, NullLogger<EnchantmentGenerator>.Instance),
            new SocketGenerator(new SocketConfig(), NullLogger<SocketGenerator>.Instance),
            CreateBudgetGenerationService(db),
            NullLogger<ItemGenerator>.Instance);
    }

    private static BudgetItemGenerationService CreateBudgetGenerationService(ContentDbContext db)
    {
        var stub = new StubConfigService();
        var budgetCalculator = new BudgetCalculator(new BudgetConfig(), NullLogger<BudgetCalculator>.Instance);
        var factory = new BudgetConfigFactory(stub, NullLogger<BudgetConfigFactory>.Instance);
        var dbFactory = new StubDbContextFactory(db);

        return new BudgetItemGenerationService(
            budgetCalculator,
            new MaterialPoolService(
                dbFactory,
                budgetCalculator,
                factory.GetMaterialPools(),
                factory.GetEnemyTypes(),
                NullLogger<MaterialPoolService>.Instance),
            factory,
            new NamePatternService(new InMemoryNamePatternRepository(), NullLogger<NamePatternService>.Instance),
            new ItemDataService(dbFactory, NullLogger<ItemDataService>.Instance),
            NullLogger<BudgetItemGenerationService>.Instance);
    }

    private sealed class StubConfigService : RealmEngine.Data.Services.GameConfigService
    {
        public override string? GetData(string key) => null;
    }

    private sealed class StubDbContextFactory(ContentDbContext db) : IDbContextFactory<ContentDbContext>
    {
        public ContentDbContext CreateDbContext() => db;
    }

    private sealed class InMemoryNamePatternRepository : RealmEngine.Core.Abstractions.INamePatternRepository
    {
        public Task<IEnumerable<RealmEngine.Data.Entities.NamePatternSet>> GetAllAsync() =>
            Task.FromResult<IEnumerable<RealmEngine.Data.Entities.NamePatternSet>>(
                new List<RealmEngine.Data.Entities.NamePatternSet>());

        public Task<RealmEngine.Data.Entities.NamePatternSet?> GetByEntityPathAsync(string entityPath) =>
            Task.FromResult<RealmEngine.Data.Entities.NamePatternSet?>(null);
    }

    [Fact]
    public async Task CraftItemAsync_Should_Use_The_Generator_For_A_Json_Reference_Output()
    {
        // Arrange
        using var db = CreateDb(Guid.NewGuid().ToString());
        var generator = CreateItemGenerator(db);
        var service = CreateService(itemGenerator: generator);
        var character = CreateCharacter(skillLevel: 150);
        var recipe = CreateRecipe(output: "@items/weapons/swords:iron-longsword");

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Success.Should().BeTrue();
        result.Item.Should().NotBeNull();
        result.Item!.Id.Should().NotBe("test-output");
        result.Item.Id.Should().NotBe("@items/weapons/swords:iron-longsword");
        ((int)result.Item.Rarity).Should().Be((int)result.ActualQuality);
    }

    [Fact]
    public async Task CraftItemAsync_Should_Fall_Back_To_A_Basic_Item_When_The_Generator_Finds_Nothing()
    {
        // Arrange — the catalog is empty, so GenerateItemByNameAsync returns null.
        using var db = CreateDb(Guid.NewGuid().ToString());
        var generator = CreateItemGenerator(db, seedItem: false);
        var service = CreateService(itemGenerator: generator);
        var character = CreateCharacter(skillLevel: 150);
        var recipe = CreateRecipe(output: "@items/weapons/swords:iron-longsword");

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Item!.Id.Should().Be("@items/weapons/swords:iron-longsword");
        result.Item.Name.Should().Be("Iron Sword");
    }

    [Theory]
    [InlineData("@broken-reference")]
    [InlineData("plain-reference")]
    public async Task CraftItemAsync_Should_Fall_Back_To_A_Basic_Item_For_Unparsable_References(string output)
    {
        // Arrange
        using var db = CreateDb(Guid.NewGuid().ToString());
        var generator = CreateItemGenerator(db);
        var service = CreateService(itemGenerator: generator);
        var character = CreateCharacter(skillLevel: 150);
        var recipe = CreateRecipe(output: output);

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Item!.Id.Should().Be(output);
        result.Item.Name.Should().Be("Iron Sword");
    }

    // ── Material reference parsing ─────────────────────────────────────────────

    [Fact]
    public async Task CraftItemAsync_Should_Match_Materials_By_The_Slug_After_The_Colon()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 150, materialCount: 0);
        character.Inventory =
        [
            new Item { Id = "iron-ore", Name = "Iron Ore" },
            new Item { Id = "iron-ore", Name = "Iron Ore" }
        ];
        var recipe = CreateRecipe(materialReference: "@items/materials/ores:iron-ore");

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Success.Should().BeTrue();
        character.Inventory.Should().BeEmpty();
    }

    [Fact]
    public async Task CraftItemAsync_Should_Strip_A_Query_Suffix_From_The_Material_Reference()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 150, materialCount: 0);
        character.Inventory = [new Item { Id = "iron-ingot", Name = "Iron Ingot" }];
        var recipe = CreateRecipe(materialReference: "@items/materials/ingots:iron-ingot?tier=2", materialQuantity: 1);

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert
        result.Success.Should().BeTrue();
        character.Inventory.Should().BeEmpty();
    }

    [Fact]
    public async Task CraftItemAsync_Should_Match_Wildcard_Materials_Against_Any_Inventory_Item()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 150, materialCount: 0);
        character.Inventory =
        [
            new Item { Id = "oak-log", Name = "Oak Log" },
            new Item { Id = "ash-log", Name = "Ash Log" }
        ];
        var recipe = CreateRecipe(materialReference: "@items/materials/organics:*", materialQuantity: 2);

        // Act
        var result = await service.CraftItemAsync(character, recipe);

        // Assert — wildcard references count any inventory item during validation, but the
        // removal path only matches exact ids/names, so the concrete stacks are left in place.
        result.Success.Should().BeTrue();
        character.Inventory.Should().HaveCount(2);
    }

    [Fact]
    public async Task CraftItemAsync_Should_Not_Remove_More_Items_Than_The_Recipe_Requires()
    {
        // Arrange
        var service = CreateService();
        var character = CreateCharacter(skillLevel: 150, materialCount: 4);
        var recipe = CreateRecipe(materialQuantity: 2);

        // Act
        await service.CraftItemAsync(character, recipe);

        // Assert
        character.Inventory.Should().HaveCount(2);
    }

    // ── Station → skill mapping ───────────────────────────────────────────────

    [Theory]
    [InlineData("anvil", "Blacksmithing")]
    [InlineData("blacksmith_forge", "Blacksmithing")]
    [InlineData("alchemy_table", "Alchemy")]
    [InlineData("enchanting_altar", "Enchanting")]
    [InlineData("cooking_fire", "Cooking")]
    [InlineData("workbench", "Carpentry")]
    [InlineData("loom", "Tailoring")]
    [InlineData("tanning_rack", "Leatherworking")]
    [InlineData("jewelry_bench", "Jewelcrafting")]
    [InlineData("mysterious_obelisk", "Crafting")]
    public void CanCraftRecipe_Should_Resolve_The_Skill_For_Each_Station(string station, string expectedSkill)
    {
        // Arrange
        var service = CreateService();
        var character = new Character
        {
            Name = "Smith",
            Skills = new Dictionary<string, CharacterSkill>
            {
                [expectedSkill] = new() { SkillId = expectedSkill.ToLowerInvariant(), CurrentRank = 5 }
            },
            Inventory = [new Item { Id = MaterialRef, Name = "Iron Ingot" }]
        };
        var recipe = CreateRecipe(station: station, requiredLevel: 5, materialQuantity: 1);

        // Act
        var canCraft = service.CanCraftRecipe(character, recipe, out var reason);

        // Assert
        canCraft.Should().BeTrue();
        reason.Should().BeEmpty();
    }

    // ── CalculateQuality ──────────────────────────────────────────────────────

    [Fact]
    public void CalculateQuality_With_Null_Recipe_Returns_Common()
    {
        // Arrange
        var service = CreateService();

        // Act
        var quality = service.CalculateQuality(CreateCharacter(skillLevel: 10), null!);

        // Assert
        quality.Should().Be((int)ItemRarity.Common);
    }

    [Fact]
    public void CalculateQuality_With_A_Single_Rarity_Returns_That_Rarity()
    {
        // Arrange — range of zero means no variance is possible.
        var service = CreateService();
        var recipe = CreateRecipe(minQuality: 2, maxQuality: 2);

        // Act
        var quality = service.CalculateQuality(CreateCharacter(skillLevel: 50), recipe);

        // Assert
        quality.Should().Be((int)ItemRarity.Rare);
    }
}
