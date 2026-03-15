using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for enemy catalog data.</summary>
public class EfCoreEnemyRepository(ContentDbContext db, ILogger<EfCoreEnemyRepository> logger)
    : IEnemyRepository
{
    /// <inheritdoc />
    public async Task<List<Enemy>> GetAllAsync()
    {
        var entities = await db.ActorArchetypes.AsNoTracking()
            .Where(e => e.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} actor archetypes from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Enemy?> GetBySlugAsync(string slug)
    {
        var entity = await db.ActorArchetypes.AsNoTracking()
            .Where(e => e.IsActive && e.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Enemy>> GetByFamilyAsync(string family)
    {
        var entities = await db.ActorArchetypes.AsNoTracking()
            .Where(e => e.IsActive && e.TypeKey == family)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Enemy MapToModel(Entities.ActorArchetype e) => new()
    {
        Id          = e.Slug,
        Slug        = e.Slug,
        Name        = e.DisplayName ?? e.Slug,
        BaseName    = e.DisplayName ?? e.Slug,
        Health      = e.Stats.Health ?? 50,
        Level       = e.MinLevel,
        RarityWeight = e.RarityWeight,
        Attributes  =
        {
            ["strength"]     = e.Stats.Strength     ?? 10,
            ["dexterity"]    = e.Stats.Agility       ?? 10,
            ["intelligence"] = e.Stats.Intelligence  ?? 10,
            ["constitution"] = e.Stats.Constitution  ?? 10,
        },
    };
}
