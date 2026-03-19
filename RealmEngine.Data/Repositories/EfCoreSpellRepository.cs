using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for spell catalog data.</summary>
public class EfCoreSpellRepository(ContentDbContext db, ILogger<EfCoreSpellRepository> logger)
    : ISpellRepository
{
    /// <inheritdoc />
    public async Task<List<Spell>> GetAllAsync()
    {
        var entities = await db.Spells.AsNoTracking()
            .Where(s => s.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} spells from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<Spell?> GetBySlugAsync(string slug)
    {
        var entity = await db.Spells.AsNoTracking()
            .Where(s => s.IsActive && s.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<Spell>> GetBySchoolAsync(string school)
    {
        var entities = await db.Spells.AsNoTracking()
            .Where(s => s.IsActive && s.School == school)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static Spell MapToModel(Entities.Spell e) => new()
    {
        SpellId     = e.Slug,
        Name        = e.Slug,
        DisplayName = e.DisplayName ?? e.Slug,
        ManaCost    = e.Stats.ManaCost ?? 0,
        Rank        = e.RarityWeight,
        Cooldown    = (int)(e.Stats.Cooldown ?? 0),
        Range       = e.Stats.Range.HasValue ? (int)e.Stats.Range.Value : 0,
        Duration    = (int)(e.Stats.Duration ?? 0),
        Tradition   = ParseTradition(e.School),
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
