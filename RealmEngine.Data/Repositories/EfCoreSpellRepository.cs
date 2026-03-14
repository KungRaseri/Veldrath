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
    };
}
