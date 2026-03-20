using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Data.Repositories;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Tests.Repositories;

[Trait("Category", "Repository")]
public class EfCoreItemRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Item MakeItem(string slug, string itemType = "consumable", bool active = true) =>
        new() { Slug = slug, ItemType = itemType, TypeKey = "consumables", IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveItems()
    {
        await using var db = CreateDbContext();
        db.Items.AddRange(MakeItem("health-potion"), MakeItem("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreItemRepository(db, NullLogger<EfCoreItemRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedModel()
    {
        await using var db = CreateDbContext();
        db.Items.Add(MakeItem("mana-potion", "consumable"));
        await db.SaveChangesAsync();
        var repo = new EfCoreItemRepository(db, NullLogger<EfCoreItemRepository>.Instance);

        var result = await repo.GetBySlugAsync("mana-potion");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("mana-potion");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenInactive()
    {
        await using var db = CreateDbContext();
        db.Items.Add(MakeItem("ghost-item", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreItemRepository(db, NullLogger<EfCoreItemRepository>.Instance);

        (await repo.GetBySlugAsync("ghost-item")).Should().BeNull();
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreItemRepository(db, NullLogger<EfCoreItemRepository>.Instance);

        (await repo.GetBySlugAsync("nonexistent")).Should().BeNull();
    }

    [Fact]
    public async Task GetByTypeAsync_FiltersOnItemType()
    {
        await using var db = CreateDbContext();
        db.Items.AddRange(
            MakeItem("health-potion", "consumable"),
            MakeItem("ruby",          "gem"),
            MakeItem("mana-potion",   "consumable"));
        await db.SaveChangesAsync();
        var repo = new EfCoreItemRepository(db, NullLogger<EfCoreItemRepository>.Instance);

        var result = await repo.GetByTypeAsync("consumable");

        result.Should().HaveCount(2).And.OnlyContain(i => i.Slug != "ruby");
    }
}

[Trait("Category", "Repository")]
public class EfCoreEnchantmentRepositoryTests
{
    private static ContentDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<ContentDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Enchantment MakeEnchantment(string slug, string? targetSlot = "weapon", bool active = true) =>
        new() { Slug = slug, TypeKey = "enchantments", TargetSlot = targetSlot, IsActive = active, DisplayName = slug };

    [Fact]
    public async Task GetAllAsync_ReturnsOnlyActiveEnchantments()
    {
        await using var db = CreateDbContext();
        db.Enchantments.AddRange(MakeEnchantment("sharpness"), MakeEnchantment("hidden", active: false));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnchantmentRepository(db, NullLogger<EfCoreEnchantmentRepository>.Instance);

        (await repo.GetAllAsync()).Should().HaveCount(1);
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsMappedModel()
    {
        await using var db = CreateDbContext();
        db.Enchantments.Add(MakeEnchantment("flame", "weapon"));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnchantmentRepository(db, NullLogger<EfCoreEnchantmentRepository>.Instance);

        var result = await repo.GetBySlugAsync("flame");

        result.Should().NotBeNull();
        result!.Slug.Should().Be("flame");
    }

    [Fact]
    public async Task GetBySlugAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateDbContext();
        var repo = new EfCoreEnchantmentRepository(db, NullLogger<EfCoreEnchantmentRepository>.Instance);

        (await repo.GetBySlugAsync("nonexistent")).Should().BeNull();
    }

    [Fact]
    public async Task GetByTargetSlotAsync_FiltersOnTargetSlot()
    {
        await using var db = CreateDbContext();
        db.Enchantments.AddRange(
            MakeEnchantment("sharpness",  "weapon"),
            MakeEnchantment("protection", "armor"),
            MakeEnchantment("flame",      "weapon"));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnchantmentRepository(db, NullLogger<EfCoreEnchantmentRepository>.Instance);

        var result = await repo.GetByTargetSlotAsync("weapon");

        result.Should().HaveCount(2).And.OnlyContain(e => e.Slug != "protection");
    }

    [Fact]
    public async Task GetByTargetSlotAsync_IsCaseInsensitive()
    {
        await using var db = CreateDbContext();
        db.Enchantments.Add(MakeEnchantment("steel", "armor"));
        await db.SaveChangesAsync();
        var repo = new EfCoreEnchantmentRepository(db, NullLogger<EfCoreEnchantmentRepository>.Instance);

        (await repo.GetByTargetSlotAsync("ARMOR")).Should().HaveCount(1);
    }
}

[Trait("Category", "Repository")]
public class EfCoreNodeRepositoryTests
{
    private static GameDbContext CreateGameDbContext() =>
        new(new DbContextOptionsBuilder<GameDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static HarvestableNode MakeNode(string nodeId, string locationId = "zone-1", int maxHealth = 100) =>
        new()
        {
            NodeId      = nodeId,
            NodeType    = "copper_vein",
            DisplayName = "Copper Vein",
            LocationId  = locationId,
            MaxHealth   = maxHealth,
            CurrentHealth = maxHealth,
            BiomeType   = "mountains",
            LootTableRef = "loot-copper",
        };

    [Fact]
    public async Task GetNodeByIdAsync_ReturnsNode_WhenExists()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        await repo.SpawnNodeAsync(MakeNode("node-1"));

        var result = await repo.GetNodeByIdAsync("node-1");

        result.Should().NotBeNull();
        result!.NodeId.Should().Be("node-1");
    }

    [Fact]
    public async Task GetNodeByIdAsync_ReturnsNull_WhenNotFound()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);

        (await repo.GetNodeByIdAsync("nonexistent")).Should().BeNull();
    }

    [Fact]
    public async Task GetNodesByLocationAsync_FiltersOnLocationId()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        await repo.SpawnNodeAsync(MakeNode("node-1", "zone-1"));
        await repo.SpawnNodeAsync(MakeNode("node-2", "zone-2"));
        await repo.SpawnNodeAsync(MakeNode("node-3", "zone-1"));

        var result = await repo.GetNodesByLocationAsync("zone-1");

        result.Should().HaveCount(2).And.OnlyContain(n => n.LocationId == "zone-1");
    }

    [Fact]
    public async Task SpawnNodeAsync_PersistsAndReturnsNode()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        var node = MakeNode("node-42", "zone-3");

        var result = await repo.SpawnNodeAsync(node);

        result.NodeId.Should().Be("node-42");
        db.HarvestableNodes.Should().HaveCount(1);
    }

    [Fact]
    public async Task UpdateNodeHealthAsync_UpdatesHealth()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        await repo.SpawnNodeAsync(MakeNode("node-5", maxHealth: 100));

        var updated = await repo.UpdateNodeHealthAsync("node-5", 40);

        updated.Should().BeTrue();
        var node = await repo.GetNodeByIdAsync("node-5");
        node!.CurrentHealth.Should().Be(40);
    }

    [Fact]
    public async Task UpdateNodeHealthAsync_ReturnsFalse_WhenNotFound()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);

        (await repo.UpdateNodeHealthAsync("ghost", 50)).Should().BeFalse();
    }

    [Fact]
    public async Task RemoveNodeAsync_DeletesNode()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        await repo.SpawnNodeAsync(MakeNode("node-10"));

        var removed = await repo.RemoveNodeAsync("node-10");

        removed.Should().BeTrue();
        (await repo.GetNodeByIdAsync("node-10")).Should().BeNull();
    }

    [Fact]
    public async Task RemoveNodeAsync_ReturnsFalse_WhenNotFound()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);

        (await repo.RemoveNodeAsync("nonexistent")).Should().BeFalse();
    }

    [Fact]
    public async Task GetNodesReadyForRegenerationAsync_ReturnsDepletedNodes()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        var fullNode     = MakeNode("node-full",     maxHealth: 100);
        var depletedNode = MakeNode("node-depleted", maxHealth: 100);
        await repo.SpawnNodeAsync(fullNode);
        await repo.SpawnNodeAsync(depletedNode);
        await repo.UpdateNodeHealthAsync("node-depleted", 60);

        var result = await repo.GetNodesReadyForRegenerationAsync();

        result.Should().HaveCount(1).And.Contain(n => n.NodeId == "node-depleted");
    }

    [Fact]
    public async Task SaveNodeAsync_UpsertsBothNewAndExisting()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        var node = MakeNode("node-upsert", maxHealth: 100);

        // Insert
        (await repo.SaveNodeAsync(node)).Should().BeTrue();
        // Update
        node.CurrentHealth = 50;
        (await repo.SaveNodeAsync(node)).Should().BeTrue();

        var fetched = await repo.GetNodeByIdAsync("node-upsert");
        fetched!.CurrentHealth.Should().Be(50);
        db.HarvestableNodes.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetNearbyNodesAsync_ReturnsSameLocationNodes()
    {
        await using var db = CreateGameDbContext();
        var repo = new EfCoreNodeRepository(db, NullLogger<EfCoreNodeRepository>.Instance);
        await repo.SpawnNodeAsync(MakeNode("node-a", "zone-A"));
        await repo.SpawnNodeAsync(MakeNode("node-b", "zone-B"));

        var result = await repo.GetNearbyNodesAsync("zone-A", radius: 10);

        result.Should().HaveCount(1).And.Contain(n => n.NodeId == "node-a");
    }
}
