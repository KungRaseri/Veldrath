using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for enchantment catalog data, reading from <see cref="ContentDbContext"/>.
/// </summary>
public class EfCoreEnchantmentRepository(ContentDbContext db, ILogger<EfCoreEnchantmentRepository> logger)
    : IEnchantmentRepository
{
    /// <inheritdoc />
    public async Task<List<Enchantment>> GetAllAsync()
    {
        var entities = await db.Enchantments
            .AsNoTracking()
            .Where(e => e.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} enchantments from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Enchantment?> GetBySlugAsync(string slug)
    {
        var entity = await db.Enchantments
            .AsNoTracking()
            .Where(e => e.IsActive && e.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Enchantment>> GetByTargetSlotAsync(string targetSlot)
    {
        var entities = await db.Enchantments
            .AsNoTracking()
            .Where(e => e.IsActive && e.TargetSlot == targetSlot.ToLowerInvariant())
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Enchantment MapToModel(Entities.Enchantment entity) => new()
    {
        Slug        = entity.Slug,
        Name        = entity.DisplayName ?? entity.Slug,
        DisplayName = entity.DisplayName ?? entity.Slug,
        Description = string.Empty,
        Value       = entity.Stats.Value ?? 0,
        RarityWeight = entity.RarityWeight,
    };
}
