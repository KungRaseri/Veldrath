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
        Type        = MapItemType(entity),
        TypeKey     = entity.TypeKey,
        Weight      = entity.Stats.Weight ?? 0.0,
        Price       = entity.Stats.Value ?? 0,
        StackSize   = entity.Stats.StackSize ?? 1,
        IsStackable = entity.Traits.Stackable ?? false,
        WeaponType  = entity.WeaponType,
        ArmorType   = entity.ArmorType,
        ArmorClass  = entity.ArmorType,
    };

    /// <summary>
    /// Maps the persisted <c>ItemType</c> discriminator and armor category to the model
    /// <see cref="ItemType"/>, so clients can render equipment buttons and icons correctly.
    /// </summary>
    private static ItemType MapItemType(Entities.Item entity)
    {
        var discriminator = entity.ItemType?.ToLowerInvariant();
        if (discriminator == "weapon")
            return ItemType.Weapon;

        if (discriminator == "armor")
        {
            var category = entity.TypeKey?.ToLowerInvariant() ?? entity.ArmorType?.ToLowerInvariant();
            if (category?.Contains("shield", StringComparison.OrdinalIgnoreCase) == true)
                return ItemType.Shield;

            return category switch
            {
                "head" or "helmet" or "helm" => ItemType.Helmet,
                "shoulders" or "shoulder"     => ItemType.Shoulders,
                "chest" or "light" or "medium" or "heavy" => ItemType.Chest,
                "bracers" or "wrist"          => ItemType.Bracers,
                "gloves" or "hands"           => ItemType.Gloves,
                "belt"                        => ItemType.Belt,
                "legs"                        => ItemType.Legs,
                "feet" or "boots"             => ItemType.Boots,
                _                             => ItemType.Chest,
            };
        }

        return ItemType.Consumable;
    }
}
