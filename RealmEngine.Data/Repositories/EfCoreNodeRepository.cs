using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for harvestable resource nodes, persisting to <see cref="GameDbContext"/>.
/// </summary>
public class EfCoreNodeRepository(GameDbContext db, ILogger<EfCoreNodeRepository> logger)
    : INodeRepository
{
    /// <inheritdoc />
    public async Task<HarvestableNode?> GetNodeByIdAsync(string nodeId)
    {
        var entity = await db.HarvestableNodes
            .AsNoTracking()
            .Where(n => n.NodeId == nodeId)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<HarvestableNode>> GetNodesByLocationAsync(string locationId)
    {
        var entities = await db.HarvestableNodes
            .AsNoTracking()
            .Where(n => n.LocationId == locationId)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<List<HarvestableNode>> GetNearbyNodesAsync(string locationId, int radius)
    {
        // Simplified: returns all nodes in the same location zone.
        // A full spatial implementation would require PostGIS or a coordinate-distance query.
        return await GetNodesByLocationAsync(locationId);
    }

    /// <inheritdoc />
    public async Task<bool> SaveNodeAsync(HarvestableNode node)
    {
        var existing = await db.HarvestableNodes
            .Where(n => n.NodeId == node.NodeId)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            db.HarvestableNodes.Add(MapToRecord(node));
        }
        else
        {
            existing.CurrentHealth  = node.CurrentHealth;
            existing.MaxHealth      = node.MaxHealth;
            existing.TimesHarvested = node.TimesHarvested;
            existing.LastHarvestedAt = node.LastHarvestedAt;
            existing.LocationId     = node.LocationId;
            existing.MaterialTier   = node.MaterialTier;
            existing.IsRichNode     = node.IsRichNode;
        }

        var rows = await db.SaveChangesAsync();
        logger.LogDebug("SaveNodeAsync({NodeId}) affected {Rows} row(s)", node.NodeId, rows);
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<bool> UpdateNodeHealthAsync(string nodeId, int newHealth)
    {
        var entity = await db.HarvestableNodes
            .Where(n => n.NodeId == nodeId)
            .FirstOrDefaultAsync();

        if (entity is null)
            return false;

        entity.CurrentHealth = newHealth;
        var rows = await db.SaveChangesAsync();
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<HarvestableNode> SpawnNodeAsync(HarvestableNode node)
    {
        var record = MapToRecord(node);
        db.HarvestableNodes.Add(record);
        await db.SaveChangesAsync();
        logger.LogDebug("Spawned node {NodeId} at {LocationId}", record.NodeId, record.LocationId);
        return MapToModel(record);
    }

    /// <inheritdoc />
    public async Task<bool> RemoveNodeAsync(string nodeId)
    {
        var entity = await db.HarvestableNodes
            .Where(n => n.NodeId == nodeId)
            .FirstOrDefaultAsync();

        if (entity is null)
            return false;

        db.HarvestableNodes.Remove(entity);
        var rows = await db.SaveChangesAsync();
        return rows > 0;
    }

    /// <inheritdoc />
    public async Task<List<HarvestableNode>> GetNodesReadyForRegenerationAsync()
    {
        var entities = await db.HarvestableNodes
            .AsNoTracking()
            .Where(n => n.CurrentHealth < n.MaxHealth)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static HarvestableNode MapToModel(HarvestableNodeRecord r) => new()
    {
        NodeId          = r.NodeId,
        NodeType        = r.NodeType,
        DisplayName     = r.DisplayName,
        MaterialTier    = r.MaterialTier,
        CurrentHealth   = r.CurrentHealth,
        MaxHealth       = r.MaxHealth,
        LastHarvestedAt = r.LastHarvestedAt,
        TimesHarvested  = r.TimesHarvested,
        LocationId      = r.LocationId,
        BiomeType       = r.BiomeType,
        LootTableRef    = r.LootTableRef,
        MinToolTier     = r.MinToolTier,
        BaseYield       = r.BaseYield,
        IsRichNode      = r.IsRichNode,
    };

    private static HarvestableNodeRecord MapToRecord(HarvestableNode n) => new()
    {
        NodeId          = n.NodeId,
        NodeType        = n.NodeType,
        DisplayName     = n.DisplayName,
        MaterialTier    = n.MaterialTier,
        CurrentHealth   = n.CurrentHealth,
        MaxHealth       = n.MaxHealth,
        LastHarvestedAt = n.LastHarvestedAt,
        TimesHarvested  = n.TimesHarvested,
        LocationId      = n.LocationId,
        BiomeType       = n.BiomeType,
        LootTableRef    = n.LootTableRef,
        MinToolTier     = n.MinToolTier,
        BaseYield       = n.BaseYield,
        IsRichNode      = n.IsRichNode,
    };
}
