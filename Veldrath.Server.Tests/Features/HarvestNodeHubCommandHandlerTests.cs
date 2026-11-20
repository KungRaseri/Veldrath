using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Harvesting;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;
using Veldrath.Server.Data;
using Veldrath.Server.Features.Characters;
using Veldrath.Server.Features.Characters.Harvesting;
using Veldrath.Server.Tests.Infrastructure;
using Character = Veldrath.Server.Data.Entities.Character;
using HarvestConfig = RealmEngine.Core.Services.Harvesting.HarvestingConfig;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers the server's multiplayer harvesting path: <see cref="HarvestNodeHubCommandHandler"/> and
/// <see cref="GetLocationNodesHubCommandHandler"/>.
/// </summary>
/// <remarks>
/// The harvesting and chest slices shipped without server tests, which is what dropped
/// <c>Veldrath.Server</c> under its coverage floor. Every refusal in the handler is a distinct player-facing
/// sentence, so each one is pinned here — a test that only walks the happy path would let a message drift.
/// </remarks>
public class HarvestNodeHubCommandHandlerTests : IDisposable
{
    private const string LocationSlug = "wood-road";
    private const string OtherLocationSlug = "old-clearing";
    private const string NodeId = "wood-road-glowcap-1";
    private const string DisplayName = "Glowcap Patch";
    private const string LootTableRef = "@loot-tables/harvesting/woods:glowcap";
    private const string ItemRef = "@items/general:glowcap-mushroom";

    private readonly TestDbContextFactory _factory = new();

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    // ── Refusals ─────────────────────────────────────────────────────────────

    [Fact]
    public async Task Harvest_Without_A_Character_Refuses()
    {
        await using var db = _factory.CreateContext();
        var nodes = new FakeNodeRepository(Node());

        var result = await Handler(db, nodes).Handle(
            new HarvestNodeHubCommand(Guid.NewGuid(), NodeId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("No character to harvest with.");
        result.Materials.Should().BeEmpty();
        nodes.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Harvest_An_Unknown_Node_Refuses()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var nodes = new FakeNodeRepository(Node());

        var result = await Handler(db, nodes).Handle(
            new HarvestNodeHubCommand(character.Id, "no-such-node"), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("This resource no longer exists.");
        result.Materials.Should().BeEmpty();
    }

    [Fact]
    public async Task Harvest_A_Node_In_Another_Location_Refuses_And_Leaves_It_Alone()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node(locationId: OtherLocationSlug);
        var nodes = new FakeNodeRepository(node);

        var result = await Handler(db, nodes).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be($"You are not standing where the {DisplayName} grows.");
        node.CurrentHealth.Should().Be(100, "a node anywhere else is not the character's to wear down");
        node.TimesHarvested.Should().Be(0);
        nodes.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Harvest_An_Empty_Node_Reports_Its_Health_Without_Wearing_It_Further()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node(currentHealth: 0);
        var nodes = new FakeNodeRepository(node);

        var result = await Handler(db, nodes).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be($"This {DisplayName} is depleted. It needs time to regenerate.");
        result.NodeId.Should().Be(NodeId);
        result.DisplayName.Should().Be(DisplayName);
        result.NodeHealthRemaining.Should().Be(0);
        result.NodeHealthPercent.Should().Be(0);
        result.NodeState.Should().Be("Empty");
        result.Materials.Should().BeEmpty();
        result.Message.Should().BeEmpty("the depletion sentence is the whole message");
        nodes.SaveCount.Should().Be(0);
    }

    [Fact]
    public async Task Harvest_A_Node_That_Needs_A_Tool_Refuses()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node(minToolTier: 2);
        var nodes = new FakeNodeRepository(node);

        var result = await Handler(db, nodes).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be($"The {DisplayName} needs a proper tool.");
        node.CurrentHealth.Should().Be(100);
        nodes.SaveCount.Should().Be(0);
    }

    // ── The happy path ───────────────────────────────────────────────────────

    [Fact]
    public async Task Harvest_Puts_The_Material_In_The_Bag_And_Wears_The_Node_Down()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node();
        var nodes = new FakeNodeRepository(node);

        var result = await Handler(db, nodes).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.ErrorMessage.Should().BeNull();
        result.NodeId.Should().Be(NodeId);
        result.DisplayName.Should().Be(DisplayName);
        result.Materials.Should().ContainSingle().Which.Should().Be(
            new HarvestedMaterial(ItemRef, "Glowcap Mushroom", 1));
        result.WasCritical.Should().BeFalse();
        result.Message.Should().Be($"Gathered 1 material from the {DisplayName}.", "one unit reads singular");

        // 20 base depletion doubled for carrying no tool.
        result.NodeHealthRemaining.Should().Be(60);
        result.NodeHealthPercent.Should().Be(60);
        result.NodeState.Should().Be("Depleted");
        node.TimesHarvested.Should().Be(1);
        node.LastHarvestedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        nodes.SaveCount.Should().Be(1);

        // The bag is the character's InventoryBlob, which is the store the game client reads.
        var stored = await db.Characters.AsNoTracking().FirstAsync(row => row.Id == character.Id);
        var bag = CharacterBag.Read(stored.InventoryBlob);
        bag.Should().ContainSingle();
        bag[0]["ItemRef"].GetString().Should().Be("glowcap-mushroom");
        bag[0]["Quantity"].GetInt32().Should().Be(1);
    }

    [Fact]
    public async Task Harvest_Counts_A_Larger_Yield_As_Plural()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node();

        var result = await Handler(db, new FakeNodeRepository(node), new HarvestLootTables(Guaranteed(quantity: 2))).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.Materials.Should().ContainSingle().Which.Quantity.Should().Be(2);
        result.Message.Should().Be($"Gathered 2 materials from the {DisplayName}.");

        var bag = CharacterBag.Read(await StoredBlobAsync(db, character.Id));
        bag[0]["Quantity"].GetInt32().Should().Be(2);
    }

    [Fact]
    public async Task Harvest_A_Critical_Roll_Doubles_The_Haul_And_Says_So()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node(isRich: true);

        var result = await Handler(
                db,
                new FakeNodeRepository(node),
                tables: new HarvestLootTables(Guaranteed()),
                random: FakeRandomSource.ReturningDoubles(0.001))
            .Handle(new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.WasCritical.Should().BeTrue();
        result.Materials.Should().ContainSingle().Which.Quantity.Should().Be(2, "a critical doubles the yield");
        result.Message.Should().Be($"Critical harvest! 2 materials from the {DisplayName}.");
    }

    [Fact]
    public async Task Harvest_Stacks_Onto_What_The_Bag_Already_Carries()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db, inventoryBlob: """[{"ItemRef":"glowcap-mushroom","Quantity":3}]""");
        var node = Node();

        var result = await Handler(db, new FakeNodeRepository(node)).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        var bag = CharacterBag.Read(await StoredBlobAsync(db, character.Id));
        bag.Should().ContainSingle();
        bag[0]["Quantity"].GetInt32().Should().Be(4);
    }

    [Fact]
    public async Task Harvest_Floors_A_Nearly_Empty_Node_At_Zero()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node(currentHealth: 10);
        var nodes = new FakeNodeRepository(node);

        var result = await Handler(db, nodes).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.NodeHealthRemaining.Should().Be(0, "depletion past empty is clamped, never negative");
        result.NodeHealthPercent.Should().Be(0);
        result.NodeState.Should().Be("Empty");
        node.CurrentHealth.Should().Be(0);
        nodes.SaveCount.Should().Be(1, "the node still wears out even for a thin harvest");
    }

    // ── The empty roll ───────────────────────────────────────────────────────

    [Fact]
    public async Task Harvest_A_Node_Whose_Table_Yields_Nothing_Still_Wears_It_And_Says_So()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var node = Node();
        var nodes = new FakeNodeRepository(node);

        // The node names a table the catalog does not hold, which is the authoring mistake the handler warns about.
        var result = await Handler(db, nodes, tables: new HarvestLootTables()).Handle(
            new HarvestNodeHubCommand(character.Id, NodeId), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("Nothing came away this time.");
        result.Materials.Should().BeEmpty();
        result.Message.Should().Be($"Gathered 0 materials from the {DisplayName}.");
        node.TimesHarvested.Should().Be(1, "the attempt still happened");
        node.CurrentHealth.Should().Be(60);
        nodes.SaveCount.Should().Be(1);
    }

    // ── GetLocationNodes ─────────────────────────────────────────────────────

    [Fact]
    public async Task Location_Nodes_Without_A_Character_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var nodes = new FakeNodeRepository(Node());

        var listed = await LocationHandler(db, nodes).Handle(
            new GetLocationNodesHubCommand(Guid.NewGuid()), CancellationToken.None);

        listed.Should().BeEmpty();
    }

    [Fact]
    public async Task Location_Nodes_Without_A_Location_Is_Empty()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db, locationSlug: null);
        var nodes = new FakeNodeRepository(Node());

        var listed = await LocationHandler(db, nodes).Handle(
            new GetLocationNodesHubCommand(character.Id), CancellationToken.None);

        listed.Should().BeEmpty();
    }

    [Fact]
    public async Task Location_Nodes_Project_Every_Node_Standing_There()
    {
        await using var db = _factory.CreateContext();
        var character = await SeedStandingAsync(db);
        var nodes = new FakeNodeRepository(
            Node(),
            Node(nodeId: "wood-road-glowcap-2", currentHealth: 50),
            Node(nodeId: "old-clearing-glowcap", locationId: OtherLocationSlug),
            Node(nodeId: "wood-road-dead", maxHealth: 0, currentHealth: 0));

        var listed = await LocationHandler(db, nodes).Handle(
            new GetLocationNodesHubCommand(character.Id), CancellationToken.None);

        listed.Should().BeEquivalentTo(new[]
        {
            new LocationNodeDto(NodeId, DisplayName, "common", 100, "Healthy", true),
            new LocationNodeDto("wood-road-glowcap-2", DisplayName, "common", 50, "Depleted", true),
            new LocationNodeDto("wood-road-dead", DisplayName, "common", 0, "Empty", false),
        }, "only the nodes at the character's location are listed");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static HarvestNodeHubCommandHandler Handler(
        ApplicationDbContext db,
        INodeRepository? nodes = null,
        ILootTableRepository? tables = null,
        IRandomSource? random = null)
    {
        var config = new HarvestConfig();
        var lootTables = new LootTableService(
            NullLogger<LootTableService>.Instance,
            tables ?? new HarvestLootTables(Guaranteed()),
            new Mock<IDbContextFactory<ContentDbContext>>().Object,
            seed: 17);

        return new HarvestNodeHubCommandHandler(
            db,
            nodes ?? new FakeNodeRepository(),
            lootTables,
            new HarvestCalculatorService(NullLogger<HarvestCalculatorService>.Instance, config),
            new CriticalHarvestService(
                NullLogger<CriticalHarvestService>.Instance,
                config,
                random ?? FakeRandomSource.ReturningDoubles(0.99)),
            NullLogger<HarvestNodeHubCommandHandler>.Instance);
    }

    private static GetLocationNodesHubCommandHandler LocationHandler(ApplicationDbContext db, INodeRepository nodes) =>
        new(db, nodes, NullLogger<GetLocationNodesHubCommandHandler>.Instance);

    private static async Task<Character> SeedStandingAsync(
        ApplicationDbContext db,
        string? locationSlug = LocationSlug,
        string inventoryBlob = "[]")
    {
        var account = await HubHarness.SeedAccountAsync(db);
        var character = await HubHarness.SeedCharacterAsync(db, account.Id);
        character.CurrentZoneLocationSlug = locationSlug;
        character.InventoryBlob = inventoryBlob;
        await db.SaveChangesAsync();
        return character;
    }

    private static async Task<string> StoredBlobAsync(ApplicationDbContext db, Guid characterId) =>
        (await db.Characters.AsNoTracking().FirstAsync(row => row.Id == characterId)).InventoryBlob;

    private static HarvestableNode Node(
        string nodeId = NodeId,
        string locationId = LocationSlug,
        int currentHealth = 100,
        int maxHealth = 100,
        int minToolTier = 0,
        bool isRich = false) => new()
        {
            NodeId = nodeId,
            NodeType = "glowcap",
            DisplayName = DisplayName,
            MaterialTier = "common",
            CurrentHealth = currentHealth,
            MaxHealth = maxHealth,
            LocationId = locationId,
            BiomeType = "forest",
            LootTableRef = LootTableRef,
            MinToolTier = minToolTier,
            BaseYield = 1,
            IsRichNode = isRich,
        };

    private static LootTableEntryData Guaranteed(int quantity = 1) => new()
    {
        ItemDomain = "items/general",
        ItemSlug = "glowcap-mushroom",
        DropWeight = 100,
        QuantityMin = quantity,
        QuantityMax = quantity,
        IsGuaranteed = true,
    };

    /// <summary>An empty or fixed harvesting loot table, so a roll is deterministic.</summary>
    private sealed class HarvestLootTables(params LootTableEntryData[] entries) : ILootTableRepository
    {
        private readonly LootTableData _table = new()
        {
            Slug = "glowcap",
            Name = "Glowcap Loot",
            Context = "harvesting",
            RarityWeight = 50,
            Entries = [.. entries],
        };

        public Task<List<LootTableData>> GetAllAsync() => Task.FromResult(new List<LootTableData> { _table });

        public Task<LootTableData?> GetBySlugAsync(string slug) =>
            Task.FromResult(slug == _table.Slug ? _table : null);

        public Task<List<LootTableData>> GetByContextAsync(string context) =>
            Task.FromResult(context == _table.Context ? new List<LootTableData> { _table } : []);
    }

    /// <summary>In-memory node store that keeps the instances the handler mutates and counts the saves.</summary>
    private sealed class FakeNodeRepository(params HarvestableNode[] nodes) : INodeRepository
    {
        private readonly Dictionary<string, HarvestableNode> _nodes =
            nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        public int SaveCount { get; private set; }

        public Task<HarvestableNode?> GetNodeByIdAsync(string nodeId) =>
            Task.FromResult(_nodes.GetValueOrDefault(nodeId));

        public Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId) =>
            Task.FromResult(_nodes.Values.Where(node => node.LocationId == locationId).ToList());

        public Task<List<HarvestableNode>> GetNearbyNodesAsync(string locationId, int radius) =>
            GetNodesByLocationAsync(locationId);

        public Task<bool> SaveNodeAsync(HarvestableNode node)
        {
            _nodes[node.NodeId] = node;
            SaveCount++;
            return Task.FromResult(true);
        }

        public Task<bool> UpdateNodeHealthAsync(string nodeId, int newHealth)
        {
            if (!_nodes.TryGetValue(nodeId, out var node)) return Task.FromResult(false);
            node.CurrentHealth = newHealth;
            return Task.FromResult(true);
        }

        public Task<HarvestableNode> SpawnNodeAsync(HarvestableNode node)
        {
            _nodes[node.NodeId] = node;
            return Task.FromResult(node);
        }

        public Task<bool> RemoveNodeAsync(string nodeId) => Task.FromResult(_nodes.Remove(nodeId));

        public Task<List<HarvestableNode>> GetNodesReadyForRegenerationAsync() =>
            Task.FromResult(new List<HarvestableNode>());
    }
}
