using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// EF Core-backed repository for character backgrounds, reading from <see cref="ContentDbContext"/>.
/// </summary>
public class EfCoreBackgroundRepository(ContentDbContext db, ILogger<EfCoreBackgroundRepository> logger)
    : IBackgroundRepository
{
    /// <inheritdoc />
    public async Task<List<Background>> GetAllBackgroundsAsync()
    {
        var entities = await db.Backgrounds
            .AsNoTracking()
            .Where(b => b.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} backgrounds from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Background?> GetBackgroundByIdAsync(string backgroundId)
    {
        var entity = await db.Backgrounds
            .AsNoTracking()
            .Where(b => b.IsActive && b.Slug == backgroundId)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Background>> GetBackgroundsByAttributeAsync(string attribute)
    {
        var entities = await db.Backgrounds
            .Where(b => b.IsActive && b.TypeKey == attribute.ToLowerInvariant())
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Background MapToModel(Entities.Background entity) => new()
    {
        Slug             = entity.Slug,
        Name             = entity.DisplayName ?? entity.Slug,
        RarityWeight     = entity.RarityWeight,
        Description      = string.Empty,
        PrimaryAttribute = entity.TypeKey,
        PrimaryBonus     = DerivePrimaryBonus(entity),
        SecondaryAttribute = string.Empty,
        SecondaryBonus     = 0,
        RecommendedLocationTypes = [],
    };

    private static int DerivePrimaryBonus(Entities.Background entity) =>
        entity.TypeKey.ToLowerInvariant() switch
        {
            "strength"     => entity.Stats.BonusStrength     ?? 0,
            "dexterity"    => entity.Stats.BonusDexterity    ?? 0,
            "intelligence" => entity.Stats.BonusIntelligence ?? 0,
            "constitution" => entity.Stats.BonusConstitution ?? 0,
            _              => 0,
        };
}
