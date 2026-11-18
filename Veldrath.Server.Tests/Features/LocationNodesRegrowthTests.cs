using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;
using Veldrath.Server.Data;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Features.Characters.Harvesting;
using Veldrath.Server.Settings;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// Resource nodes come back after a while, so a location cannot be farmed out permanently.
/// </summary>
/// <remarks>
/// Nothing called the engine's <c>RespawnNode</c>, so an emptied node was dead for good: a player could harvest
/// the Ashlen Wood dry once and never gather there again. Regrowth is lazy — listing a location's nodes is what
/// revives the ones that have waited long enough — which keeps the world free of a background sweep that walks
/// every node.
/// </remarks>
[Trait("Category", "Feature")]
public class LocationNodesRegrowthTests
{
    [Fact]
    public async Task An_Empty_Node_That_Waited_Long_Enough_Comes_Back()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var character = await SeedCharacterAsync(db, "wood-road");

        var nodes = new FakeNodeRepository();
        nodes.Nodes.Add(Node("old", health: 0, harvestedAt: DateTime.UtcNow.AddHours(-3)));

        var listed = await MakeHandler(db, nodes, regrowMinutes: 30)
            .Handle(new GetLocationNodesHubCommand(character.Id), CancellationToken.None);

        listed.Should().ContainSingle().Which.CanHarvest.Should().BeTrue();
        nodes.Saved.Should().ContainSingle("the regrown node has to be persisted, not just drawn");
        nodes.Nodes[0].CurrentHealth.Should().Be(100);
        nodes.Nodes[0].TimesHarvested.Should().Be(0);
    }

    [Fact]
    public async Task A_Node_Emptied_A_Moment_Ago_Stays_Empty()
    {
        using var factory = new TestDbContextFactory();
        await using var db = factory.CreateContext();
        var character = await SeedCharacterAsync(db, "wood-road");

        var nodes = new FakeNodeRepository();
        nodes.Nodes.Add(Node("fresh", health: 0, harvestedAt: DateTime.UtcNow.AddMinutes(-5)));

        var listed = await MakeHandler(db, nodes, regrowMinutes: 30)
            .Handle(new GetLocationNodesHubCommand(character.Id), CancellationToken.None);

        listed.Should().ContainSingle().Which.CanHarvest.Should().BeFalse();
        nodes.Saved.Should().BeEmpty();
    }

    private static GetLocationNodesHubCommandHandler MakeHandler(
        ApplicationDbContext db,
        INodeRepository nodes,
        int regrowMinutes) =>
        new(
            db,
            nodes,
            Options.Create(new HarvestingOptions { RegrowMinutes = regrowMinutes }),
            NullLogger<GetLocationNodesHubCommandHandler>.Instance);

    private static HarvestableNode Node(string id, int health, DateTime harvestedAt) => new()
    {
        NodeId = id,
        NodeType = "glowcap-cluster",
        DisplayName = "Glowcap Mushrooms",
        LocationId = "wood-road",
        MaxHealth = 100,
        CurrentHealth = health,
        LastHarvestedAt = harvestedAt,
    };

    private static async Task<Character> SeedCharacterAsync(ApplicationDbContext db, string locationSlug)
    {
        var account = new PlayerAccount { UserName = $"u_{Guid.NewGuid():N}" };
        account.NormalizedUserName = account.UserName.ToUpperInvariant();
        db.Users.Add(account);

        var character = new Character
        {
            AccountId = account.Id,
            Name = $"Char_{Guid.NewGuid():N}",
            ClassName = "Warrior",
            SlotIndex = 1,
            Attributes = "{}",
            CurrentZoneLocationSlug = locationSlug,
        };

        db.Characters.Add(character);
        await db.SaveChangesAsync();
        return character;
    }

    private sealed class FakeNodeRepository : INodeRepository
    {
        public List<HarvestableNode> Nodes { get; } = [];
        public List<HarvestableNode> Saved { get; } = [];

        public Task<HarvestableNode?> GetNodeByIdAsync(string nodeId) =>
            Task.FromResult(Nodes.FirstOrDefault(node => node.NodeId == nodeId));

        public Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId) =>
            Task.FromResult(Nodes.Where(node => node.LocationId == locationId).ToList());

        public Task<List<HarvestableNode>> GetNearbyNodesAsync(string locationId, int radius) =>
            Task.FromResult(Nodes.Where(node => node.LocationId == locationId).ToList());

        public Task<bool> SaveNodeAsync(HarvestableNode node)
        {
            Saved.Add(node);
            return Task.FromResult(true);
        }

        public Task<HarvestableNode> SpawnNodeAsync(HarvestableNode node)
        {
            Nodes.Add(node);
            return Task.FromResult(node);
        }

        public Task<bool> UpdateNodeHealthAsync(string nodeId, int currentHealth)
        {
            var node = Nodes.FirstOrDefault(candidate => candidate.NodeId == nodeId);
            if (node is null) return Task.FromResult(false);

            node.CurrentHealth = currentHealth;
            Saved.Add(node);
            return Task.FromResult(true);
        }

        public Task<bool> RemoveNodeAsync(string nodeId)
        {
            var removed = Nodes.RemoveAll(node => node.NodeId == nodeId);
            return Task.FromResult(removed > 0);
        }

        public Task<List<HarvestableNode>> GetNodesReadyForRegenerationAsync() =>
            Task.FromResult(Nodes.Where(node => !node.CanHarvest()).ToList());
    }
}
