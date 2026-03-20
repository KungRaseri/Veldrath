using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for general item catalog data, reading from <see cref="ContentDbContext"/>.
/// Covers consumables, crystals, gems, runes, essences, and orbs.
/// </summary>
public class EfCoreItemRepository(ContentDbContext db, ILogger<EfCoreItemRepository> logger)
    : IItemRepository
{
    /// <inheritdoc />
    public async Task<List<Item>> GetAllAsync()
    {
        var entities = await db.Items
            .AsNoTracking()
            .Where(i => i.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} items from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Item?> GetBySlugAsync(string slug)
    {
        var entity = await db.Items
            .AsNoTracking()
            .Where(i => i.IsActive && i.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Item>> GetByTypeAsync(string itemType)
    {
        var entities = await db.Items
            .AsNoTracking()
            .Where(i => i.IsActive && i.ItemType == itemType.ToLowerInvariant())
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Item MapToModel(Entities.Item entity) => new()
    {
        Slug        = entity.Slug,
        Name        = entity.DisplayName ?? entity.Slug,
        Description = string.Empty,
        TypeKey     = entity.TypeKey,
        Weight      = entity.Stats.Weight ?? 0.0,
        Price       = entity.Stats.Value ?? 0,
        StackSize   = entity.Stats.StackSize ?? 1,
        IsStackable = entity.Traits.Stackable ?? false,
    };
}
