using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for loot table catalog data.</summary>
public class EfCoreLootTableRepository(ContentDbContext db, ILogger<EfCoreLootTableRepository> logger)
    : ILootTableRepository
{
    /// <inheritdoc />
    public async Task<List<LootTableData>> GetAllAsync()
    {
        var entities = await db.LootTables.AsNoTracking()
            .Where(t => t.IsActive)
            .Include(t => t.Entries)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} loot tables from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<LootTableData?> GetBySlugAsync(string slug)
    {
        var entity = await db.LootTables.AsNoTracking()
            .Where(t => t.IsActive && t.Slug == slug)
            .Include(t => t.Entries)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<LootTableData>> GetByContextAsync(string context)
    {
        var entities = await db.LootTables.AsNoTracking()
            .Where(t => t.IsActive && t.TypeKey == context)
            .Include(t => t.Entries)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static LootTableData MapToModel(Entities.LootTable e) => new()
    {
        Slug         = e.Slug,
        Name         = e.DisplayName ?? e.Slug,
        Context      = e.TypeKey,
        RarityWeight = e.RarityWeight,
        IsBoss       = e.Traits.Boss == true,
        IsChest      = e.Traits.IsChest == true,
        IsHarvesting = e.Traits.IsHarvesting == true,
        Entries      = e.Entries.Select(entry => new LootTableEntryData
        {
            ItemDomain   = entry.ItemDomain,
            ItemSlug     = entry.ItemSlug,
            DropWeight   = entry.DropWeight,
            QuantityMin  = entry.QuantityMin,
            QuantityMax  = entry.QuantityMax,
            IsGuaranteed = entry.IsGuaranteed,
        }).ToList(),
    };
}
