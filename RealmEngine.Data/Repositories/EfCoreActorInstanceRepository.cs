using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Services;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for actor instance catalog data.
/// </summary>
/// <remarks>
/// The catalogue projections describe instances; the placement projections resolve one — projecting its archetype and
/// composing the instance's overrides onto the template — because that composed template is what a spawn is copied
/// from. A row with no location is catalogue-only and is never returned by either placement query.
/// </remarks>
public class EfCoreActorInstanceRepository(
    ContentDbContext db,
    ILogger<EfCoreActorInstanceRepository> logger,
    IRandomSource? randomSource = null)
    : IActorInstanceRepository
{
    private readonly IRandomSource _random = randomSource ?? SystemRandomSource.Shared;

    /// <inheritdoc />
    public async Task<List<ActorInstanceEntry>> GetAllAsync()
    {
        var entities = await db.ActorInstances.AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} actor instances from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<ActorInstanceEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.ActorInstances.AsNoTracking()
            .Where(a => a.IsActive && a.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<ActorInstanceEntry>> GetByTypeKeyAsync(string typeKey)
    {
        var entities = await db.ActorInstances.AsNoTracking()
            .Where(a => a.IsActive && a.TypeKey == typeKey)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<List<PlacedActorInstance>> GetPlacedAsync(
        string locationSlug,
        CancellationToken cancellationToken = default)
    {
        var entities = await db.ActorInstances.AsNoTracking()
            .Include(a => a.Archetype).ThenInclude(archetype => archetype!.LootTable)
            .Include(a => a.LootTable)
            .Where(a => a.IsActive && a.LocationSlug == locationSlug)
            .OrderBy(a => a.Slug)
            .ToListAsync(cancellationToken);

        var placed = new List<PlacedActorInstance>(entities.Count);
        foreach (var entity in entities)
        {
            if (ToPlacedOrNull(entity, locationSlug) is { } instance)
            {
                placed.Add(instance);
            }
        }

        logger.LogDebug(
            "Resolved {Count} actor instance(s) placed at {LocationSlug}", placed.Count, locationSlug);

        return placed;
    }

    /// <inheritdoc />
    public async Task<PlacedActorInstance?> GetTemplateBySlugAsync(
        string instanceSlug,
        CancellationToken cancellationToken = default)
    {
        var entity = await db.ActorInstances.AsNoTracking()
            .Include(a => a.Archetype).ThenInclude(archetype => archetype!.LootTable)
            .Include(a => a.LootTable)
            .Where(a => a.IsActive && a.Slug == instanceSlug)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : ToPlacedOrNull(entity, entity.LocationSlug);
    }

    /// <summary>
    /// Composes one instance onto its archetype's projected template, reporting a dangling archetype instead of
    /// spawning something with the model defaults.
    /// </summary>
    /// <param name="entity">Instance row, with its archetype and loot tables loaded.</param>
    /// <param name="locationSlug">Location the caller asked about, used only for the log line.</param>
    /// <returns>The placed instance, or <see langword="null"/> when its archetype is missing.</returns>
    private PlacedActorInstance? ToPlacedOrNull(Entities.ActorInstance entity, string? locationSlug)
    {
        if (entity.Archetype is null)
        {
            // Failsafe, not fallback: an instance whose archetype was deleted has no stats to compose onto, and
            // spawning it with model defaults would silently put a 50 HP nobody in the roster.
            logger.LogError(
                "Actor instance '{Slug}' at '{LocationSlug}' references a missing archetype; it was not placed",
                entity.Slug,
                locationSlug);
            return null;
        }

        var template = ActorTemplateProjection.Project(
            entity.Archetype,
            _random,
            entity.StatOverrides,
            entity.LevelOverride,
            entity.DisplayName,
            entity.LootTable?.Slug);

        return new PlacedActorInstance(
            entity.Slug,
            entity.Archetype.Slug,
            template.Name,
            entity.TypeKey,
            entity.LocationSlug ?? string.Empty,
            template);
    }

    private static ActorInstanceEntry MapToModel(Entities.ActorInstance a) =>
        new(a.Slug, a.DisplayName ?? a.Slug, a.TypeKey, a.ArchetypeId,
            a.LevelOverride, a.FactionOverride, a.RarityWeight, a.LocationSlug);
}
