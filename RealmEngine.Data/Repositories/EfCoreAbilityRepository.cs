using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for ability catalog data.</summary>
public class EfCoreAbilityRepository(ContentDbContext db, ILogger<EfCoreAbilityRepository> logger)
    : IAbilityRepository
{
    /// <inheritdoc />
    public async Task<List<Ability>> GetAllAsync()
    {
        var entities = await db.Abilities.AsNoTracking()
            .Where(a => a.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} abilities from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Ability?> GetBySlugAsync(string slug)
    {
        var entity = await db.Abilities.AsNoTracking()
            .Where(a => a.IsActive && a.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Ability>> GetByTypeAsync(string abilityType)
    {
        var entities = await db.Abilities.AsNoTracking()
            .Where(a => a.IsActive && a.AbilityType == abilityType)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Ability MapToModel(Entities.Ability e) => new()
    {
        Id          = e.Slug,
        Slug        = e.Slug,
        Name        = e.Slug,
        DisplayName = e.DisplayName ?? e.Slug,
        RarityWeight = e.RarityWeight,
        Cooldown    = (int)(e.Stats.Cooldown ?? 0),
        ManaCost    = e.Stats.ManaCost ?? 0,
        Range       = e.Stats.Range.HasValue ? (int)e.Stats.Range.Value : null,
        Duration    = e.Stats.Duration.HasValue ? (int)e.Stats.Duration.Value : null,
        IsPassive   = e.Traits.IsPassive ?? false,
        Type        = ParseAbilityType(e.AbilityType, e.Traits.IsPassive ?? false),
    };

    // Entity AbilityType ("active"/"passive"/"reactive"/"ultimate") maps to activation style,
    // not the model's combat-role enum. We preserve "passive" exactly; all active variants
    // default to Offensive until per-row TypeKey data can drive a richer mapping.
    private static AbilityTypeEnum ParseAbilityType(string abilityType, bool isPassive)
    {
        if (isPassive) return AbilityTypeEnum.Passive;
        return abilityType.ToLowerInvariant() switch
        {
            "passive"  => AbilityTypeEnum.Passive,
            "reactive" => AbilityTypeEnum.Defensive,
            _          => AbilityTypeEnum.Offensive
        };
    }
}
