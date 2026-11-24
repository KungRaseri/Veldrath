using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Harvesting;
using Veldrath.Server.Features.Characters.Queries;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;
using HarvestConfig = RealmEngine.Core.Services.Harvesting.HarvestingConfig;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// The regression for the crafting store mismatch: materials harvested into the character's bag must be
/// spendable by the craft, and the crafted item must come back through the inventory read the client calls.
/// </summary>
/// <remarks>
/// Crafting used to read and write <c>IInventoryService</c> — <c>GameDbContext.InventoryRecords</c> — while
/// every bag write (harvest, chest, combat loot, shop) lands in the character's own <c>InventoryBlob</c>.
/// <c>InventoryRecords</c> is never populated, so a craft demanded materials the player was carrying and put
/// its output in a table no client reads. This test starts from a real harvest so the bag is populated by the
/// production write path, not by a fixture that hides the mismatch.
/// </remarks>
public class CraftFromHarvestedBagTests : IDisposable
{
    private const string LocationSlug = "wood-road";
    private const string NodeId = "wood-road-iron-vein";
    private const string NodeDisplayName = "Iron Vein";
    private const string LootTableRef = "@loot-tables/harvesting/woods:iron-vein";
    private const string IngotRef = "@items/materials/ingots:iron-ingot";
    private const string SwordRef = "@items/weapons/swords:iron-longsword";
    private const string RecipeSlug = "iron-longsword";

    private readonly TestDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task A_harvested_material_is_spendable_and_the_crafted_item_shows_in_the_inventory_read()
    {
        await using var db = _factory.CreateContext();
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(
            db,
            account.Id,
            level: 5,
            attributes: """{"Strength":12,"Skill_blacksmithing_Rank":1,"Skill_blacksmithing_XP":100}""");
        character.CurrentZoneLocationSlug = LocationSlug;
        await db.SaveChangesAsync();

        // 1. The production harvest path writes two ingots into the character's InventoryBlob as the bare slug.
        var harvest = await HarvestHandler(db).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        harvest.Success.Should().BeTrue(harvest.ErrorMessage);
        harvest.Materials.Should().ContainSingle().Which.Should().Be(new HarvestedMaterial(IngotRef, "Iron Ingot", 2));

        var harvestedBag = CharacterBag.Read((await StoredAsync(db, character.Id)).InventoryBlob);
        CharacterBag.Count(harvestedBag, "iron-ingot").Should().Be(2, "harvest writes the slug the client resolves");

        // 2. The craft spends those ingots and puts the sword in the same blob.
        var craft = await CraftHandler(db).Handle(
            new CraftItemHubCommand(character.Id, RecipeSlug), CancellationToken.None);

        craft.Success.Should().BeTrue($"the bag holds the harvested ingots; the craft says: {craft.ErrorMessage}");
        craft.ItemRef.Should().Be(SwordRef);
        craft.ConsumedMaterials.Should().Equal(new ConsumedMaterialDto(IngotRef, 2));

        var craftedBag = CharacterBag.Read((await StoredAsync(db, character.Id)).InventoryBlob);
        CharacterBag.Count(craftedBag, "iron-ingot").Should().Be(0, "the materials were consumed");
        CharacterBag.Count(craftedBag, "iron-longsword").Should().Be(1, "the output is in the client's bag");

        // 3. The same command the client calls returns the sword.
        var items = new Mock<IItemRepository>();
        items.Setup(repository => repository.GetBySlugAsync(It.IsAny<string>())).ReturnsAsync((Item?)null);

        var inventory = await new GetInventoryHubCommandHandler(
                new CharacterRepository(db),
                items.Object,
                NullLogger<GetInventoryHubCommandHandler>.Instance)
            .Handle(new GetInventoryHubCommand(character.Id), CancellationToken.None);

        inventory.Success.Should().BeTrue(inventory.ErrorMessage);
        inventory.Items.Should().ContainSingle()
            .Which.Should().Match<InventoryItemView>(view =>
                view.ItemRef == "iron-longsword" && view.Quantity == 1);

        // 4. The craft panel's "what can I make?" read uses the same bag: the spent ingots are gone from it.
        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.GetAllAsync()).ReturnsAsync([Longsword()]);

        var craftable = await new GetCraftableRecipesQueryHandler(
                new CharacterRepository(db),
                recipes.Object,
                NullLogger<GetCraftableRecipesQueryHandler>.Instance)
            .Handle(new GetCraftableRecipesQuery(character.Id), CancellationToken.None);

        var recipe = craftable.Should().ContainSingle().Subject;
        recipe.Materials.Should().ContainSingle().Which.Owned.Should().Be(0);
        recipe.Available.Should().BeFalse("the recipe's two ingots were just spent");
        recipe.UnavailableReason.Should().Be($"Short of 2 more {IngotRef}.");
    }

    private static async Task<Character> StoredAsync(ApplicationDbContext db, Guid characterId) =>
        await db.Characters.AsNoTracking().FirstAsync(row => row.Id == characterId);

    private static CraftItemHubCommandHandler CraftHandler(ApplicationDbContext db)
    {
        var characters = new CharacterRepository(db);
        var award = new AwardSkillXpHubCommandHandler(
            characters, NullLogger<AwardSkillXpHubCommandHandler>.Instance);

        var mediator = new Mock<IMediator>();
        mediator
            .Setup(m => m.Send(It.IsAny<IRequest<AwardSkillXpHubResult>>(), It.IsAny<CancellationToken>()))
            .Returns((IRequest<AwardSkillXpHubResult> request, CancellationToken cancellationToken) =>
                award.Handle((AwardSkillXpHubCommand)request, cancellationToken));

        var recipes = new Mock<IRecipeRepository>();
        recipes.Setup(repository => repository.GetBySlugAsync(RecipeSlug)).ReturnsAsync(Longsword());

        return new CraftItemHubCommandHandler(
            characters,
            recipes.Object,
            mediator.Object,
            NullLogger<CraftItemHubCommandHandler>.Instance);
    }

    private static HarvestNodeHubCommandHandler HarvestHandler(ApplicationDbContext db)
    {
        var config = new HarvestConfig();
        var lootTables = new LootTableService(
            NullLogger<LootTableService>.Instance,
            new IronVeinLootTables(),
            new Mock<IDbContextFactory<ContentDbContext>>().Object,
            seed: 17);

        return new HarvestNodeHubCommandHandler(
            db,
            new FakeNodeRepository(Node()),
            lootTables,
            new HarvestCalculatorService(NullLogger<HarvestCalculatorService>.Instance, config),
            new CriticalHarvestService(
                NullLogger<CriticalHarvestService>.Instance,
                config,
                FakeRandomSource.ReturningDoubles(0.99)),
            NullLogger<HarvestNodeHubCommandHandler>.Instance);
    }

    private static Recipe Longsword() => new()
    {
        Id = RecipeSlug,
        Name = "Iron longsword",
        Slug = RecipeSlug,
        Description = "A plain blade.",
        Category = "weapons",
        RequiredLevel = 1,
        RequiredSkill = "blacksmithing",
        RequiredSkillLevel = 1,
        OutputItemReference = SwordRef,
        OutputQuantity = 1,
        ExperienceGained = 15,
        Materials = [new RecipeMaterial { ItemReference = IngotRef, Quantity = 2 }],
    };

    private static HarvestableNode Node() => new()
    {
        NodeId = NodeId,
        NodeType = "iron-vein",
        DisplayName = NodeDisplayName,
        MaterialTier = "common",
        CurrentHealth = 100,
        MaxHealth = 100,
        LocationId = LocationSlug,
        BiomeType = "forest",
        LootTableRef = LootTableRef,
        BaseYield = 1,
    };

    private static LootTableEntryData Guaranteed(int quantity) => new()
    {
        ItemDomain = "items/materials/ingots",
        ItemSlug = "iron-ingot",
        DropWeight = 100,
        QuantityMin = quantity,
        QuantityMax = quantity,
        IsGuaranteed = true,
    };

    /// <summary>A fixed harvesting loot table, so the roll is deterministic.</summary>
    private sealed class IronVeinLootTables : ILootTableRepository
    {
        private readonly LootTableData _table = new()
        {
            Slug = "iron-vein",
            Name = "Iron Vein Loot",
            Context = "harvesting",
            RarityWeight = 50,
            Entries = [Guaranteed(2)],
        };

        public Task<List<LootTableData>> GetAllAsync() => Task.FromResult(new List<LootTableData> { _table });

        public Task<LootTableData?> GetBySlugAsync(string slug) =>
            Task.FromResult(slug == _table.Slug ? _table : null);

        public Task<List<LootTableData>> GetByContextAsync(string context) =>
            Task.FromResult(context == _table.Context ? new List<LootTableData> { _table } : []);
    }

    /// <summary>In-memory node store; the harvest mutates the instance it is handed.</summary>
    private sealed class FakeNodeRepository(params HarvestableNode[] nodes) : INodeRepository
    {
        private readonly Dictionary<string, HarvestableNode> _nodes =
            nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        public Task<HarvestableNode?> GetNodeByIdAsync(string nodeId) =>
            Task.FromResult(_nodes.GetValueOrDefault(nodeId));

        public Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId) =>
            Task.FromResult(_nodes.Values.Where(node => node.LocationId == locationId).ToList());

        public Task<List<HarvestableNode>> GetNearbyNodesAsync(string locationId, int radius) =>
            GetNodesByLocationAsync(locationId);

        public Task<bool> SaveNodeAsync(HarvestableNode node)
        {
            _nodes[node.NodeId] = node;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateNodeHealthAsync(string nodeId, int newHealth) => Task.FromResult(true);

        public Task<HarvestableNode> SpawnNodeAsync(HarvestableNode node) => Task.FromResult(node);

        public Task<bool> RemoveNodeAsync(string nodeId) => Task.FromResult(_nodes.Remove(nodeId));

        public Task<List<HarvestableNode>> GetNodesReadyForRegenerationAsync() =>
            Task.FromResult(new List<HarvestableNode>());
    }
}
