using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Services;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for enemy catalog data. The archetype row is projected onto the <see cref="Enemy"/>
/// template a spawn is copied from, so every authored stat has to survive the projection: combat reads the
/// template, never the content row.
/// </summary>
/// <param name="db">Content database context.</param>
/// <param name="logger">Logger instance.</param>
/// <param name="randomSource">
/// Source for the per-spawn gold roll; defaults to <see cref="SystemRandomSource.Shared"/>. Register or pass a
/// seeded source to make a spawn reproducible.
/// </param>
public class EfCoreEnemyRepository(
    ContentDbContext db,
    ILogger<EfCoreEnemyRepository> logger,
    IRandomSource? randomSource = null)
    : IEnemyRepository
{
    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Shared;

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
            .Include(e => e.LootTable)
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

    /// <summary>Projects one archetype row onto the template a spawn is copied from.</summary>
    /// <remarks>
    /// The projection lives in <see cref="ActorTemplateProjection"/> so the named-instance path composes its overrides
    /// onto exactly the same projection instead of keeping a second copy of these rules; this call passes no
    /// overrides, which is what keeps a bare archetype spawn identical to what it was before instances were placed.
    /// </remarks>
    /// <param name="e">The archetype row.</param>
    /// <returns>The spawned-out enemy template.</returns>
    private Enemy MapToModel(Entities.ActorArchetype e) => ActorTemplateProjection.Project(e, _random);
}
