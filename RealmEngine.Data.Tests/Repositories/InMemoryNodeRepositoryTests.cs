using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class InMemoryNodeRepositoryTests
{
    private readonly InMemoryNodeRepository _repository =
        new(NullLogger<InMemoryNodeRepository>.Instance);

    private static HarvestableNode MakeNode(string id, string locationId = "loc-1", int maxHealth = 100, int currentHealth = 100) =>
        new()
        {
            NodeId = id,
            NodeType = "copper_vein",
            DisplayName = "Copper Vein",
            LocationId = locationId,
            MaxHealth = maxHealth,
            CurrentHealth = currentHealth,
            LastHarvestedAt = DateTime.MinValue
        };

    // ── SpawnNodeAsync ────────────────────────────────────────────────────

    [Fact]
    public async Task SpawnNodeAsync_WithExplicitId_StoresNodeUnderThatId()
    {
        var node = MakeNode("node-1");
        await _repository.SpawnNodeAsync(node);
        (await _repository.GetNodeByIdAsync("node-1")).Should().NotBeNull();
    }

    [Fact]
    public async Task SpawnNodeAsync_WithEmptyId_AssignsAutoId()
    {
        var node = MakeNode(string.Empty);
        var spawned = await _repository.SpawnNodeAsync(node);
        spawned.NodeId.Should().NotBeNullOrEmpty();
        _repository.Count.Should().Be(1);
    }

    [Fact]
    public async Task SpawnNodeAsync_AutoIds_AreSequential()
    {
        var a = await _repository.SpawnNodeAsync(MakeNode(string.Empty));
        var b = await _repository.SpawnNodeAsync(MakeNode(string.Empty));
        a.NodeId.Should().Be("node_1");
        b.NodeId.Should().Be("node_2");
    }

    [Fact]
    public async Task SpawnNodeAsync_IndexesNodeByLocation()
    {
        await _repository.SpawnNodeAsync(MakeNode("n1", "loc-A"));
        var nodes = await _repository.GetNodesByLocationAsync("loc-A");
        nodes.Should().ContainSingle(n => n.NodeId == "n1");
    }

    // ── GetNodeByIdAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task GetNodeByIdAsync_ReturnsNull_WhenMissing()
    {
        (await _repository.GetNodeByIdAsync("no-such-id")).Should().BeNull();
    }

    [Fact]
    public async Task GetNodeByIdAsync_ReturnsNode_AfterSpawn()
    {
        await _repository.SpawnNodeAsync(MakeNode("n-get"));
        (await _repository.GetNodeByIdAsync("n-get")).Should().NotBeNull();
    }

    // ── GetNodesByLocationAsync ───────────────────────────────────────────

    [Fact]
    public async Task GetNodesByLocationAsync_ReturnsOnlyMatchingLocation()
    {
        await _repository.SpawnNodeAsync(MakeNode("n1", "zone-A"));
        await _repository.SpawnNodeAsync(MakeNode("n2", "zone-B"));
        await _repository.SpawnNodeAsync(MakeNode("n3", "zone-A"));

        var result = await _repository.GetNodesByLocationAsync("zone-A");
        result.Should().HaveCount(2).And.OnlyContain(n => n.LocationId == "zone-A");
    }

    [Fact]
    public async Task GetNodesByLocationAsync_ReturnsEmpty_WhenLocationUnknown()
    {
        var result = await _repository.GetNodesByLocationAsync("nowhere");
        result.Should().BeEmpty();
    }

    // ── GetNearbyNodesAsync ───────────────────────────────────────────────

    [Fact]
    public async Task GetNearbyNodesAsync_DelegatesToLocation()
    {
        await _repository.SpawnNodeAsync(MakeNode("n1", "loc-near"));
        var result = await _repository.GetNearbyNodesAsync("loc-near", radius: 10);
        result.Should().ContainSingle(n => n.NodeId == "n1");
    }

    // ── SaveNodeAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task SaveNodeAsync_UpsertsExistingNode()
    {
        await _repository.SpawnNodeAsync(MakeNode("n-save", "loc-1", maxHealth: 100, currentHealth: 100));
        var updated = MakeNode("n-save", "loc-1", maxHealth: 100, currentHealth: 50);
        var result = await _repository.SaveNodeAsync(updated);

        result.Should().BeTrue();
        (await _repository.GetNodeByIdAsync("n-save"))!.CurrentHealth.Should().Be(50);
    }

    [Fact]
    public async Task SaveNodeAsync_AddsNewNode_WhenNotPresent()
    {
        var node = MakeNode("n-new", "loc-save");
        var result = await _repository.SaveNodeAsync(node);

        result.Should().BeTrue();
        _repository.Count.Should().Be(1);
    }

    [Fact]
    public async Task SaveNodeAsync_DoesNotDuplicateLocationIndex()
    {
        var node = MakeNode("n-dup", "loc-dup");
        await _repository.SaveNodeAsync(node);
        await _repository.SaveNodeAsync(node); // same node again

        var nodes = await _repository.GetNodesByLocationAsync("loc-dup");
        nodes.Should().ContainSingle(); // no duplicate entries
    }

    // ── UpdateNodeHealthAsync ─────────────────────────────────────────────

    [Fact]
    public async Task UpdateNodeHealthAsync_UpdatesHealth_ReturnsTrue()
    {
        await _repository.SpawnNodeAsync(MakeNode("n-health", currentHealth: 100));
        var result = await _repository.UpdateNodeHealthAsync("n-health", 40);

        result.Should().BeTrue();
        (await _repository.GetNodeByIdAsync("n-health"))!.CurrentHealth.Should().Be(40);
    }

    [Fact]
    public async Task UpdateNodeHealthAsync_ReturnsFalse_WhenNodeMissing()
    {
        (await _repository.UpdateNodeHealthAsync("no-node", 50)).Should().BeFalse();
    }

    // ── RemoveNodeAsync ───────────────────────────────────────────────────

    [Fact]
    public async Task RemoveNodeAsync_RemovesNode_ReturnsTrue()
    {
        await _repository.SpawnNodeAsync(MakeNode("n-rm", "loc-rm"));
        var result = await _repository.RemoveNodeAsync("n-rm");

        result.Should().BeTrue();
        _repository.Count.Should().Be(0);
    }

    [Fact]
    public async Task RemoveNodeAsync_RemovesFromLocationIndex()
    {
        await _repository.SpawnNodeAsync(MakeNode("n-rm2", "loc-idx"));
        await _repository.RemoveNodeAsync("n-rm2");

        (await _repository.GetNodesByLocationAsync("loc-idx")).Should().BeEmpty();
    }

    [Fact]
    public async Task RemoveNodeAsync_ReturnsFalse_WhenNodeMissing()
    {
        (await _repository.RemoveNodeAsync("ghost")).Should().BeFalse();
    }

    // ── GetNodesReadyForRegenerationAsync ─────────────────────────────────

    [Fact]
    public async Task GetNodesReadyForRegen_ReturnsNodes_WhenDepleted_And60sElapsed()
    {
        var depleted = MakeNode("n-regen", currentHealth: 50, maxHealth: 100);
        depleted.LastHarvestedAt = DateTime.UtcNow.AddSeconds(-61);
        await _repository.SpawnNodeAsync(depleted);

        var ready = await _repository.GetNodesReadyForRegenerationAsync();
        ready.Should().ContainSingle(n => n.NodeId == "n-regen");
    }

    [Fact]
    public async Task GetNodesReadyForRegen_ExcludesNodes_WithFullHealth()
    {
        var full = MakeNode("n-full", currentHealth: 100, maxHealth: 100);
        full.LastHarvestedAt = DateTime.UtcNow.AddSeconds(-120);
        await _repository.SpawnNodeAsync(full);

        var ready = await _repository.GetNodesReadyForRegenerationAsync();
        ready.Should().BeEmpty();
    }

    [Fact]
    public async Task GetNodesReadyForRegen_ExcludesNodes_HarvestedTooRecently()
    {
        var recent = MakeNode("n-recent", currentHealth: 30, maxHealth: 100);
        recent.LastHarvestedAt = DateTime.UtcNow.AddSeconds(-5);
        await _repository.SpawnNodeAsync(recent);

        var ready = await _repository.GetNodesReadyForRegenerationAsync();
        ready.Should().BeEmpty();
    }

    // ── Clear ────────────────────────────────────────────────────────────

    [Fact]
    public async Task Clear_RemovesAllNodesAndResetsCounter()
    {
        await _repository.SpawnNodeAsync(MakeNode("n1", "loc-1"));
        await _repository.SpawnNodeAsync(MakeNode("n2", "loc-2"));
        _repository.Clear();

        _repository.Count.Should().Be(0);
        var spawned = await _repository.SpawnNodeAsync(MakeNode(string.Empty));
        spawned.NodeId.Should().Be("node_1"); // counter reset
    }
}
