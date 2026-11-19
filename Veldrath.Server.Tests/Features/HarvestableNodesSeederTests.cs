using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using Veldrath.Server.Data.Seeders;
using Veldrath.Server.Tests.Infrastructure;

namespace Veldrath.Server.Tests.Features;

/// <summary>
/// The authored resource nodes, and the loot they promise.
/// </summary>
/// <remarks>
/// <c>gather-mushrooms</c> has existed since the content baseline while nothing in the world could produce
/// a <c>glowcap-mushroom</c>: no harvesting loot table was seeded and no node was ever created. These tests
/// pin both halves of that loop — the nodes are authored, idempotent, and they name a loot table the seeded
/// catalog actually contains — so a future content pass cannot quietly detach one from the other again.
/// </remarks>
[Trait("Category", "Validation")]
public class HarvestableNodesSeederTests
{
    [Fact]
    public async Task SeedAsync_AddsEveryAuthoredNode_Once()
    {
        using var factory = new TestGameDbContextFactory();
        await using var db = factory.CreateContext();

        await HarvestableNodesSeeder.SeedAsync(db);

        var seeded = await db.HarvestableNodes.AsNoTracking().ToListAsync();
        seeded.Should().HaveCount(HarvestableNodesSeeder.Authors().Length);
        seeded.Should().OnlyContain(n => n.CurrentHealth > 0 && n.CurrentHealth == n.MaxHealth);
    }

    [Fact]
    public async Task SeedAsync_IsIdempotent_AndKeepsTheLiveStateItFinds()
    {
        using var factory = new TestGameDbContextFactory();
        await using var db = factory.CreateContext();

        await HarvestableNodesSeeder.SeedAsync(db);

        // Deplete one node the way a harvest would, then start the server again.
        var node = await db.HarvestableNodes.SingleAsync(n => n.NodeId == "wood-road-glowcap-1");
        node.CurrentHealth = 10;
        node.TimesHarvested = 3;
        await db.SaveChangesAsync();

        await HarvestableNodesSeeder.SeedAsync(db);

        var after = await db.HarvestableNodes.AsNoTracking().ToListAsync();
        after.Should().HaveCount(HarvestableNodesSeeder.Authors().Length);
        var depleted = after.Single(n => n.NodeId == "wood-road-glowcap-1");
        depleted.CurrentHealth.Should().Be(10, "a restart must not restore a depleted node");
        depleted.TimesHarvested.Should().Be(3);
    }

    [Fact]
    public void Authors_PlaceForageInTheWoodsTheQuestNames()
    {
        var nodes = HarvestableNodesSeeder.Authors();

        nodes.Should().NotBeEmpty();
        nodes.Select(n => n.NodeId).Should().OnlyHaveUniqueItems();
        nodes.Should().OnlyContain(n => n.LootTableRef == HarvestableNodesSeeder.ForestForageLootTableRef);
        nodes.Should().OnlyContain(n => n.BiomeType == "forest");
        nodes.Should().OnlyContain(n => n.MaxHealth > 0 && n.BaseYield >= 1);

        // `gather-mushrooms` reads "from the forest floor", and the Ashlen Wood is where it points.
        nodes.Select(n => n.LocationId).Distinct().Should().BeEquivalentTo("wood-road", "old-clearing");
    }

    [Fact]
    public async Task EveryAuthoredNode_NamesALootTableTheSeededCatalogHas_AndItYieldsTheQuestItem()
    {
        await using var probe = new SeededContentProbe();
        var content = await probe.CreateContextAsync();

        // The reference form is "@loot-tables/{context}/{kind}:{slug}" — the engine rolls by the slug
        // after the last colon (LootTableService), so that is what has to exist in the catalog.
        foreach (var reference in HarvestableNodesSeeder.Authors().Select(n => n.LootTableRef).Distinct())
        {
            var slug = reference[(reference.LastIndexOf(':') + 1)..];
            var table = await content.LootTables
                .Include(t => t.Traits)
                .Include(t => t.Entries)
                .AsNoTracking()
                .SingleOrDefaultAsync(t => t.Slug == slug);

            table.Should().NotBeNull($"node loot reference '{reference}' must resolve to a seeded table");
            table!.Traits.IsHarvesting.Should().BeTrue($"{slug} is a harvesting table");
            table.Entries.Should().Contain(
                e => e.ItemSlug == "glowcap-mushroom",
                "the gathering quest asks for glowcap mushrooms and a node is the only source");
        }
    }
}
