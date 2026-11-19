using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;
using RealmEngine.Shared.Services;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Chests;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Covers <see cref="ChestService"/>: the reopen window, the roaming draw (zone choice, site choice, loot table
/// choice) and resource-node regrowth, which moved onto the same sweep so a node comes back on time whether or
/// not anybody is looking at the location.
/// </summary>
public class ChestSweeperTests : IDisposable
{
    private readonly TestDbContextFactory _factory = new();

    /// <inheritdoc />
    public void Dispose() => _factory.Dispose();

    private static ChestService Service(
        ApplicationDbContext db,
        IEnumerable<ZoneLocationEntry>? locations = null,
        IEnumerable<LootTableData>? tables = null,
        INodeRepository? nodes = null,
        ChestOptions? chests = null,
        HarvestingOptions? harvesting = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(db);
        services.AddSingleton<IZoneLocationRepository>(new FakeZoneLocationRepository(locations ?? []));
        services.AddSingleton<ILootTableRepository>(new FakeLootTableRepository(tables ?? []));
        services.AddSingleton(nodes ?? new FakeNodeRepository([]));
        services.AddSingleton<IRandomSource>(new SystemRandomSource());

        var provider = services.BuildServiceProvider();

        return new ChestService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(chests ?? new ChestOptions()),
            Options.Create(harvesting ?? new HarvestingOptions()),
            NullLogger<ChestService>.Instance);
    }

    private static LootChest OpenedChest(DateTimeOffset reopenAt) => new()
    {
        ChestId = "test-chest",
        Kind = LootChestKind.Authored,
        ZoneId = "ashlen-wood",
        LocationSlug = "old-clearing",
        DisplayName = "Mossy Strongbox",
        LootTableRefs = "[\"@loot-tables/chests/forest:forest-chest-loot\"]",
        IsOpened = true,
        OpenedAt = DateTimeOffset.UtcNow.AddMinutes(-240),
        ReopenAt = reopenAt,
    };

    private static LootTableData ForestChestLoot() => new()
    {
        Slug = "forest-chest-loot",
        Name = "Forest Chest Loot",
        Context = "chests",
        RarityWeight = 50,
        IsChest = true,
    };

    private static ZoneLocationEntry Site(string slug, string zoneId, int minLevel = 3) =>
        new(slug, slug, "locations", zoneId, RarityWeight: 100, MinLevel: minLevel, MaxLevel: null);

    // ── Reopen window ────────────────────────────────────────────────────────

    [Fact]
    public async Task A_Chest_Whose_Window_Has_Passed_Reopens()
    {
        await using var db = _factory.CreateContext();
        db.LootChests.Add(OpenedChest(DateTimeOffset.UtcNow.AddMinutes(-1)));
        await db.SaveChangesAsync();

        await Service(db).SweepAsync(CancellationToken.None);

        // Read past the change tracker: the sweep wrote with ExecuteUpdate, which the tracker never sees.
        var stored = await db.LootChests.AsNoTracking().SingleAsync();
        stored.IsOpened.Should().BeFalse();
        stored.ReopenAt.Should().BeNull();
        stored.OpenedAt.Should().BeNull("the clock is cleared with the state it belongs to");
    }

    [Fact]
    public async Task A_Chest_Inside_Its_Window_Stays_Closed()
    {
        await using var db = _factory.CreateContext();
        db.LootChests.Add(OpenedChest(DateTimeOffset.UtcNow.AddHours(2)));
        await db.SaveChangesAsync();

        await Service(db).SweepAsync(CancellationToken.None);

        (await db.LootChests.AsNoTracking().SingleAsync()).IsOpened.Should().BeTrue();
    }

    // ── Roaming placement ────────────────────────────────────────────────────

    [Fact]
    public async Task A_Roaming_Chest_Is_Placed_In_A_Wilderness_Zone_When_Due()
    {
        await using var db = _factory.CreateContext();

        await Service(
                db,
                locations: [Site("the-droveway-road", "the-droveway"), Site("the-droveway-hollow", "the-droveway")],
                tables: [ForestChestLoot()])
            .SweepAsync(CancellationToken.None);

        var chest = db.LootChests.Single(row => row.Kind == LootChestKind.Roaming);
        chest.ZoneId.Should().Be("the-droveway");
        chest.LocationSlug.Should().BeOneOf("the-droveway-road", "the-droveway-hollow");
        chest.IsOpened.Should().BeFalse();
        chest.ZoneInstanceId.Should().BeNull("a roaming chest in the shared world belongs to no run");
        chest.LootTableRefs.Should().Contain("forest-chest-loot");
    }

    [Fact]
    public async Task No_Roaming_Chest_Is_Placed_In_A_Town()
    {
        await using var db = _factory.CreateContext();

        await Service(
                db,
                locations: [Site("crestfall-square", "crestfall"), Site("crestfall-market", "crestfall")],
                tables: [ForestChestLoot()])
            .SweepAsync(CancellationToken.None);

        db.LootChests.Should().NotContain(row => row.Kind == LootChestKind.Roaming);
    }

    [Fact]
    public async Task A_Safe_Respawn_Or_Instanced_Site_Is_Never_Chosen()
    {
        await using var db = _factory.CreateContext();

        await Service(
                db,
                locations:
                [
                    Site("the-droveway-road", "the-droveway") with { IsSafeRespawn = true },
                    Site("the-droveway-crypt", "the-droveway") with { IsInstanced = true },
                ],
                tables: [ForestChestLoot()])
            .SweepAsync(CancellationToken.None);

        db.LootChests.Should().BeEmpty("no candidate survives the site filter");
    }

    [Fact]
    public async Task A_Zone_That_Already_Holds_A_Chest_Gets_No_Second_One()
    {
        await using var db = _factory.CreateContext();
        db.LootChests.Add(new LootChest
        {
            ChestId = "roaming:existing",
            Kind = LootChestKind.Roaming,
            ZoneId = "the-droveway",
            LocationSlug = "the-droveway-road",
            DisplayName = "Hidden Cache",
            LootTableRefs = "[\"@loot-tables/chests/forest:forest-chest-loot\"]",
            SpawnedAt = DateTimeOffset.UtcNow.AddDays(-2),
        });
        await db.SaveChangesAsync();

        await Service(
                db,
                locations: [Site("the-droveway-road", "the-droveway"), Site("the-droveway-hollow", "the-droveway")],
                tables: [ForestChestLoot()])
            .SweepAsync(CancellationToken.None);

        db.LootChests.Count(row => row.Kind == LootChestKind.Roaming).Should().Be(1);
    }

    [Fact]
    public async Task A_Zone_Inside_Its_Cadence_Gets_Nothing_New()
    {
        await using var db = _factory.CreateContext();
        db.LootChests.Add(new LootChest
        {
            ChestId = "roaming:just-opened",
            Kind = LootChestKind.Roaming,
            ZoneId = "the-droveway",
            LocationSlug = "the-droveway-road",
            DisplayName = "Hidden Cache",
            LootTableRefs = "[\"@loot-tables/chests/forest:forest-chest-loot\"]",
            IsOpened = true,
            OpenedAt = DateTimeOffset.UtcNow,
            ReopenAt = DateTimeOffset.UtcNow.AddHours(3),
            SpawnedAt = DateTimeOffset.UtcNow,
        });
        await db.SaveChangesAsync();

        await Service(
                db,
                locations: [Site("the-droveway-hollow", "the-droveway")],
                tables: [ForestChestLoot()])
            .SweepAsync(CancellationToken.None);

        db.LootChests.Count(row => row.Kind == LootChestKind.Roaming).Should().Be(1, "the cadence has not elapsed");
    }

    // ── Node regrowth ────────────────────────────────────────────────────────

    [Fact]
    public async Task An_Empty_Node_That_Waited_Long_Enough_Comes_Back()
    {
        await using var db = _factory.CreateContext();
        var nodes = new FakeNodeRepository(
        [
            new HarvestableNode
            {
                NodeId = "old-clearing-glowcap",
                DisplayName = "Glowcap Mushrooms",
                MaterialTier = "common",
                CurrentHealth = 0,
                MaxHealth = 100,
                LastHarvestedAt = DateTime.UtcNow.AddHours(-2),
                LocationId = "old-clearing",
                LootTableRef = "forest-forage",
            },
        ]);

        await Service(db, nodes: nodes).SweepAsync(CancellationToken.None);

        var stored = await nodes.GetNodeByIdAsync("old-clearing-glowcap");
        stored!.CurrentHealth.Should().Be(100);
        stored.TimesHarvested.Should().Be(0);
    }

    [Fact]
    public async Task A_Node_Emptied_A_Moment_Ago_Stays_Empty()
    {
        await using var db = _factory.CreateContext();
        var nodes = new FakeNodeRepository(
        [
            new HarvestableNode
            {
                NodeId = "wood-road-glowcap",
                DisplayName = "Glowcap Mushrooms",
                MaterialTier = "common",
                CurrentHealth = 0,
                MaxHealth = 100,
                LastHarvestedAt = DateTime.UtcNow,
                LocationId = "wood-road",
                LootTableRef = "forest-forage",
            },
        ]);

        await Service(db, nodes: nodes).SweepAsync(CancellationToken.None);

        var stored = await nodes.GetNodeByIdAsync("wood-road-glowcap");
        stored!.CurrentHealth.Should().Be(0);
    }

    private sealed class FakeZoneLocationRepository(IEnumerable<ZoneLocationEntry> locations)
        : IZoneLocationRepository
    {
        private readonly List<ZoneLocationEntry> _locations = [.. locations];

        public Task<List<ZoneLocationEntry>> GetAllAsync() => Task.FromResult(_locations.ToList());

        public Task<ZoneLocationEntry?> GetBySlugAsync(string slug) =>
            Task.FromResult(_locations.FirstOrDefault(location => location.Slug == slug));

        public Task<List<ZoneLocationEntry>> GetByTypeKeyAsync(string typeKey) =>
            Task.FromResult(_locations.Where(location => location.TypeKey == typeKey).ToList());

        public Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId) =>
            Task.FromResult(_locations.Where(location => location.ZoneId == zoneId).ToList());

        public Task<List<ZoneLocationEntry>> GetByZoneIdAsync(string zoneId, IEnumerable<string> unlockedSlugs) =>
            GetByZoneIdAsync(zoneId);

        public Task<List<ZoneLocationEntry>> GetHiddenByZoneIdAsync(string zoneId) =>
            Task.FromResult(_locations.Where(location => location.ZoneId == zoneId && location.IsHidden).ToList());

        public Task<List<ZoneLocationEntry>> GetAllByZoneIdAsync(string zoneId) => GetByZoneIdAsync(zoneId);
    }

    private sealed class FakeLootTableRepository(IEnumerable<LootTableData> tables) : ILootTableRepository
    {
        private readonly List<LootTableData> _tables = [.. tables];

        public Task<List<LootTableData>> GetAllAsync() => Task.FromResult(_tables.ToList());

        public Task<LootTableData?> GetBySlugAsync(string slug) =>
            Task.FromResult(_tables.FirstOrDefault(table => table.Slug == slug));

        public Task<List<LootTableData>> GetByContextAsync(string context) =>
            Task.FromResult(_tables.Where(table => table.Context == context).ToList());
    }

    private sealed class FakeNodeRepository(IEnumerable<HarvestableNode> nodes) : INodeRepository
    {
        private readonly Dictionary<string, HarvestableNode> _nodes =
            nodes.ToDictionary(node => node.NodeId, StringComparer.Ordinal);

        public Task<HarvestableNode?> GetNodeByIdAsync(string nodeId) =>
            Task.FromResult(_nodes.TryGetValue(nodeId, out var node) ? node : null);

        public Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId) =>
            Task.FromResult(_nodes.Values.Where(node => node.LocationId == locationId).ToList());

        public Task<List<HarvestableNode>> GetNearbyNodesAsync(string locationId, int radius) =>
            GetNodesByLocationAsync(locationId);

        public Task<bool> SaveNodeAsync(HarvestableNode node)
        {
            _nodes[node.NodeId] = node;
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
            Task.FromResult(_nodes.Values.Where(node => node.CurrentHealth < node.MaxHealth).ToList());
    }
}
