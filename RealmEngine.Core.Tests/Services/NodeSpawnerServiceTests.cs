using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Core.Tests.Services;

[Trait("Category", "Service")]
public class NodeSpawnerServiceTests
{
    private static MaterialEntry MakeMaterial(string slug, string family, float rarityWeight = 50f) =>
        new(slug, $"{slug} display", family, rarityWeight, true, null, null, null, null);

    private static NodeSpawnerService CreateService(IMaterialRepository repo) =>
        new(NullLogger<NodeSpawnerService>.Instance, repo);

    // ── SpawnNodesAsync validation ─────────────────────────────────────────────

    [Fact]
    public async Task SpawnNodesAsync_ThrowsArgumentException_WhenLocationIdIsEmpty()
    {
        var repo = new Mock<IMaterialRepository>();
        var svc = CreateService(repo.Object);

        await svc.Invoking(s => s.SpawnNodesAsync(string.Empty, "forest"))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("locationId");
    }

    [Fact]
    public async Task SpawnNodesAsync_ThrowsArgumentException_WhenBiomeIsEmpty()
    {
        var repo = new Mock<IMaterialRepository>();
        var svc = CreateService(repo.Object);

        await svc.Invoking(s => s.SpawnNodesAsync("loc-1", string.Empty))
            .Should().ThrowAsync<ArgumentException>()
            .WithParameterName("biome");
    }

    [Fact]
    public async Task SpawnNodesAsync_ReturnsEmpty_WhenRepositoryReturnsNoMaterials()
    {
        var repo = new Mock<IMaterialRepository>();
        repo.Setup(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([]);
        var svc = CreateService(repo.Object);

        var result = await svc.SpawnNodesAsync("loc-1", "forest");

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task SpawnNodesAsync_ReturnsNodes_WithCorrectLocationAndBiome()
    {
        var repo = new Mock<IMaterialRepository>();
        repo.Setup(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([MakeMaterial("oak-wood", "wood", 60f)]);
        var svc = CreateService(repo.Object);

        var result = await svc.SpawnNodesAsync("forest-zone-1", "forest", "medium");

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(n =>
        {
            n.LocationId.Should().Be("forest-zone-1");
            n.BiomeType.Should().Be("forest");
        });
    }

    [Fact]
    public async Task SpawnNodesAsync_ReturnsNodes_WithUniqueNodeIds()
    {
        var repo = new Mock<IMaterialRepository>();
        repo.Setup(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync([MakeMaterial("iron-ore", "metal", 50f)]);
        var svc = CreateService(repo.Object);

        // "abundant" guarantees at least 5 nodes, enough to verify unique IDs
        var result = await svc.SpawnNodesAsync("cave-zone", "dungeon", "abundant");

        var ids = result.Select(n => n.NodeId).ToList();
        ids.Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task SpawnNodesAsync_ReturnsEmpty_WhenRepositoryThrows()
    {
        var repo = new Mock<IMaterialRepository>();
        repo.Setup(r => r.GetByFamiliesAsync(It.IsAny<IEnumerable<string>>()))
            .ThrowsAsync(new InvalidOperationException("DB down"));
        var svc = CreateService(repo.Object);

        // Should catch internally and return empty, not propagate exception
        var result = await svc.SpawnNodesAsync("loc-1", "forest");

        result.Should().BeEmpty();
    }

    // ── RespawnNode ────────────────────────────────────────────────────────────

    [Fact]
    public void RespawnNode_ThrowsArgumentNullException_WhenNodeIsNull()
    {
        var repo = new Mock<IMaterialRepository>();
        var svc = CreateService(repo.Object);

        svc.Invoking(s => s.RespawnNode(null!))
            .Should().Throw<ArgumentNullException>()
            .WithParameterName("node");
    }

    [Fact]
    public void RespawnNode_ReturnsFalse_WhenNodeIsStillHarvestable()
    {
        // CanHarvest() = true when CurrentHealth / MaxHealth >= 0.1
        var node = new HarvestableNode
        {
            NodeId = "node-1",
            MaxHealth = 100,
            CurrentHealth = 100,
            LastHarvestedAt = DateTime.UtcNow.AddHours(-2)
        };
        var svc = CreateService(new Mock<IMaterialRepository>().Object);

        svc.RespawnNode(node).Should().BeFalse();
    }

    [Fact]
    public void RespawnNode_ReturnsFalse_WhenLastHarvestedAtIsMinValue()
    {
        // Empty node (CanHarvest = false) that was never actually harvested
        var node = new HarvestableNode
        {
            NodeId = "node-1",
            MaxHealth = 100,
            CurrentHealth = 0,
            LastHarvestedAt = DateTime.MinValue
        };
        var svc = CreateService(new Mock<IMaterialRepository>().Object);

        svc.RespawnNode(node).Should().BeFalse();
    }

    [Fact]
    public void RespawnNode_ReturnsFalse_WhenCooldownHasNotElapsed()
    {
        var node = new HarvestableNode
        {
            NodeId = "node-1",
            MaxHealth = 100,
            CurrentHealth = 0,
            LastHarvestedAt = DateTime.UtcNow.AddMinutes(-30)
        };
        var svc = CreateService(new Mock<IMaterialRepository>().Object);

        svc.RespawnNode(node, cooldownMinutes: 60).Should().BeFalse();
    }

    [Fact]
    public void RespawnNode_ReturnsTrue_AndRestoresHealth_WhenCooldownHasPassed()
    {
        var node = new HarvestableNode
        {
            NodeId = "node-1",
            MaxHealth = 100,
            CurrentHealth = 0,
            LastHarvestedAt = DateTime.UtcNow.AddHours(-2)
        };
        var svc = CreateService(new Mock<IMaterialRepository>().Object);

        var result = svc.RespawnNode(node, cooldownMinutes: 60);

        result.Should().BeTrue();
        node.CurrentHealth.Should().Be(node.MaxHealth);
        node.LastHarvestedAt.Should().Be(DateTime.MinValue);
        node.TimesHarvested.Should().Be(0);
    }
}
