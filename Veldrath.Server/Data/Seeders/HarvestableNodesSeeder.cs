using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;

namespace Veldrath.Server.Data.Seeders;

/// <summary>
/// Seeds the resource nodes a player can actually harvest.
/// </summary>
/// <remarks>
/// The engine has had a complete harvesting path for a while — <c>HarvestNodeCommand</c>, the yield and
/// depletion calculators, tool validation, the harvesting loot tables — but nothing ever created a node,
/// so <c>gather-mushrooms</c> could not be completed by any route. The engine's own node spawner is
/// procedural (biome + density), which cannot promise that the item a specific quest asks for exists.
/// These rows are authored instead: they are the forest-floor mushrooms the gathering quest names, placed
/// in the Ashlen Wood where the quest's text points.
///
/// Idempotent by <see cref="HarvestableNodeRecord.NodeId"/>: a database that already holds a node keeps its
/// live state (health, harvest count), so restarting the server never restores a depleted node.
/// </remarks>
public static class HarvestableNodesSeeder
{
    /// <summary>The loot table every authored node points at, in the engine's reference form.</summary>
    public const string ForestForageLootTableRef = "@loot-tables/harvesting/forest:forest-forage";

    /// <summary>Seeds the authored nodes into the game database.</summary>
    /// <param name="db">The game database that holds live node state.</param>
    public static async Task SeedAsync(GameDbContext db)
    {
        var existing = await db.HarvestableNodes.Select(n => n.NodeId).ToListAsync();
        var known = existing.ToHashSet(StringComparer.Ordinal);
        var missing = Authors().Where(node => !known.Contains(node.NodeId)).ToArray();

        if (missing.Length == 0)
            return;

        db.HarvestableNodes.AddRange(missing);
        await db.SaveChangesAsync();
    }

    /// <summary>The authored nodes. Pure so a test can assert what the seed promises.</summary>
    /// <returns>Every node the seeder creates on a fresh database.</returns>
    public static HarvestableNodeRecord[] Authors()
    {
        static HarvestableNodeRecord Glowcap(string nodeId, string locationId, int maxHealth, bool rich = false) => new()
        {
            NodeId = nodeId,
            NodeType = "glowcap-cluster",
            DisplayName = "Glowcap Mushrooms",
            MaterialTier = "common",
            CurrentHealth = maxHealth,
            MaxHealth = maxHealth,
            LocationId = locationId,
            BiomeType = "forest",
            LootTableRef = ForestForageLootTableRef,
            MinToolTier = 0,
            BaseYield = 1,
            IsRichNode = rich,
        };

        return
        [
            // The Wood Road and the Old Clearing are the two forest-floor locations in the Ashlen Wood,
            // which is where `gather-mushrooms` says to look.
            Glowcap("wood-road-glowcap-1", "wood-road", 100),
            Glowcap("wood-road-glowcap-2", "wood-road", 100),
            Glowcap("wood-road-glowcap-3", "wood-road", 100),
            Glowcap("old-clearing-glowcap-1", "old-clearing", 150, rich: true),
            Glowcap("old-clearing-glowcap-2", "old-clearing", 100),
        ];
    }
}
