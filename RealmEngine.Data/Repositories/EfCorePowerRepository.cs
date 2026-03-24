using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for the unified power catalog.</summary>
public class EfCorePowerRepository(ContentDbContext db, ILogger<EfCorePowerRepository> logger)
    : IPowerRepository
{
    /// <inheritdoc />
    public async Task<List<Power>> GetAllAsync()
    {
        var entities = await db.Powers.AsNoTracking()
            .Where(p => p.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} powers from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Power?> GetBySlugAsync(string slug)
    {
        var entity = await db.Powers.AsNoTracking()
            .Where(p => p.IsActive && p.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Power>> GetByTypeAsync(string powerType)
    {
        var entities = await db.Powers.AsNoTracking()
            .Where(p => p.IsActive && p.PowerType == powerType)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<List<Power>> GetBySchoolAsync(string school)
    {
        var entities = await db.Powers.AsNoTracking()
            .Where(p => p.IsActive && p.School == school)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Power MapToModel(Entities.Power e) => new()
    {
        Id           = e.Slug,
        Slug         = e.Slug,
        Name         = e.Slug,
        DisplayName  = e.DisplayName ?? e.Slug,
        RarityWeight = e.RarityWeight,
        Rank         = e.RarityWeight, // RarityWeight repurposed as Rank for spell-rank queries
        Cooldown     = (int)(e.Stats.Cooldown ?? 0),
        ManaCost     = e.Stats.ManaCost ?? 0,
        Range        = e.Stats.Range.HasValue ? (int)e.Stats.Range.Value : null,
        Duration     = e.Stats.Duration.HasValue ? (int)e.Stats.Duration.Value : null,
        Radius       = e.Stats.Radius,
        MaxTargets   = e.Stats.MaxTargets,
        School       = e.School,
        Tradition    = e.School is null ? null : ParseTradition(e.School),
        Type         = ParsePowerType(e.PowerType),
        IsPassive    = e.PowerType == "passive" || (e.Traits.IsPassive ?? false),
        EffectType   = PowerEffectType.None,
    };

    private static PowerType ParsePowerType(string powerType) =>
        powerType.ToLowerInvariant() switch
        {
            "innate"   => PowerType.Innate,
            "talent"   => PowerType.Talent,
            "spell"    => PowerType.Spell,
            "cantrip"  => PowerType.Cantrip,
            "ultimate" => PowerType.Ultimate,
            "passive"  => PowerType.Passive,
            "reaction" => PowerType.Reaction,
            _          => PowerType.Talent
        };

    // Entity stores granular schools ("fire", "frost", etc.); map to the four broad traditions.
    private static MagicalTradition ParseTradition(string school) =>
        school.ToLowerInvariant() switch
        {
            "arcane" or "force" or "transmutation" => MagicalTradition.Arcane,
            "divine" or "holy" or "sacred" or "light" or "healing" => MagicalTradition.Divine,
            "shadow" or "occult" or "psychic" or "dark" or "void" => MagicalTradition.Occult,
            _ => MagicalTradition.Primal  // fire, frost, nature, earth, water, wind, storm, …
        };
}
