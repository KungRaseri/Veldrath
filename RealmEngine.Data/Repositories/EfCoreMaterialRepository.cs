using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Entities;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for material catalog data.</summary>
public class EfCoreMaterialRepository(ContentDbContext db) : IMaterialRepository
{
    /// <inheritdoc />
    public async Task<List<MaterialEntry>> GetAllAsync()
    {
        var entities = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive)
            .ToListAsync();
        return entities.Select(MapToEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<List<MaterialEntry>> GetByFamiliesAsync(IEnumerable<string> families)
    {
        var familyList = families.ToList();
        var entities = await db.Materials.AsNoTracking()
            .Where(m => m.IsActive && familyList.Contains(m.MaterialFamily))
            .ToListAsync();
        return entities.Select(MapToEntry).ToList();
    }

    /// <inheritdoc />
    public async Task<MaterialEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.Materials.AsNoTracking()
            .FirstOrDefaultAsync(m => m.IsActive && m.Slug == slug);
        return entity is null ? null : MapToEntry(entity);
    }

    private static MaterialEntry MapToEntry(Material m) => new(
        m.Slug,
        m.DisplayName ?? m.Slug,
        m.MaterialFamily,
        m.RarityWeight,
        m.IsActive,
        m.Stats.Hardness,
        m.Stats.Conductivity,
        m.Traits.Magical,
        m.Traits.Enchantable);
}
