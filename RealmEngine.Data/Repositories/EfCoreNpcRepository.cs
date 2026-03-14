using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for NPC catalog data.</summary>
public class EfCoreNpcRepository(ContentDbContext db, ILogger<EfCoreNpcRepository> logger)
    : INpcRepository
{
    /// <inheritdoc />
    public async Task<List<NPC>> GetAllAsync()
    {
        var entities = await db.Npcs.AsNoTracking()
            .Where(n => n.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} NPCs from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<NPC?> GetBySlugAsync(string slug)
    {
        var entity = await db.Npcs.AsNoTracking()
            .Where(n => n.IsActive && n.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<NPC>> GetByCategoryAsync(string category)
    {
        var entities = await db.Npcs.AsNoTracking()
            .Where(n => n.IsActive && n.TypeKey == category)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static NPC MapToModel(Entities.Npc e) => new()
    {
        Id          = e.Slug,
        Slug        = e.Slug,
        Name        = e.DisplayName ?? e.Slug,
        DisplayName = e.DisplayName ?? e.Slug,
        Occupation  = e.TypeKey,
    };
}
