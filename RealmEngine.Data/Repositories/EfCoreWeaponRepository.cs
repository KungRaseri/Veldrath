using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using WeaponEntity = RealmEngine.Data.Entities.Weapon;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for weapon catalog data.</summary>
public class EfCoreWeaponRepository(ContentDbContext db) : IWeaponRepository
{
    /// <inheritdoc />
    public async Task<List<Item>> GetAllAsync()
    {
        var entities = await db.Weapons.AsNoTracking()
            .Where(w => w.IsActive)
            .ToListAsync();
        return entities.Select(MapToItem).ToList();
    }

    /// <inheritdoc />
    public async Task<Item?> GetBySlugAsync(string slug)
    {
        var entity = await db.Weapons.AsNoTracking()
            .FirstOrDefaultAsync(w => w.IsActive && w.Slug == slug);
        return entity is null ? null : MapToItem(entity);
    }

    private static Item MapToItem(WeaponEntity w) => new()
    {
        Id = $"weapon:{w.TypeKey}:{w.Slug}",
        Slug = w.Slug,
        Name = w.DisplayName ?? w.TypeKey,
        BaseName = w.DisplayName ?? w.TypeKey,
        Type = ItemType.Weapon,
        TypeKey = w.TypeKey,
        WeaponType = w.WeaponType,
        Price = w.Stats.Value ?? 0,
        Weight = w.Stats.Weight ?? 0,
        TotalRarityWeight = w.RarityWeight,
    };
}
