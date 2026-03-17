using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using ArmorEntity = RealmEngine.Data.Entities.Armor;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for armor catalog data.</summary>
public class EfCoreArmorRepository(ContentDbContext db) : IArmorRepository
{
    /// <inheritdoc />
    public async Task<List<Item>> GetAllAsync()
    {
        var entities = await db.Armors.AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync();
        return entities.Select(MapToItem).ToList();
    }

    /// <inheritdoc />
    public async Task<Item?> GetBySlugAsync(string slug)
    {
        var entity = await db.Armors.AsNoTracking()
            .FirstOrDefaultAsync(a => a.IsActive && a.Slug == slug);
        return entity is null ? null : MapToItem(entity);
    }

    private static Item MapToItem(ArmorEntity a) => new()
    {
        Id = $"armor:{a.TypeKey}:{a.Slug}",
        Slug = a.Slug,
        Name = a.DisplayName ?? a.TypeKey,
        BaseName = a.DisplayName ?? a.TypeKey,
        Type = a.ArmorType.Equals("shield", StringComparison.OrdinalIgnoreCase) ? ItemType.Shield : ItemType.Chest,
        TypeKey = a.TypeKey,
        ArmorType = a.ArmorType,
        ArmorClass = a.ArmorType,
        Price = a.Stats.Value ?? 0,
        Weight = a.Stats.Weight ?? 0,
        TotalRarityWeight = a.RarityWeight,
    };
}
