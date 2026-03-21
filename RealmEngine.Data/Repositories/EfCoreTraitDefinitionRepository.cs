using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for trait definition data.</summary>
public class EfCoreTraitDefinitionRepository(ContentDbContext db, ILogger<EfCoreTraitDefinitionRepository> logger)
    : ITraitDefinitionRepository
{
    /// <inheritdoc />
    public async Task<List<TraitDefinitionEntry>> GetAllAsync()
    {
        var entities = await db.TraitDefinitions.AsNoTracking()
            .ToListAsync();

        logger.LogDebug("Loaded {Count} trait definitions from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<TraitDefinitionEntry?> GetByKeyAsync(string key)
    {
        var entity = await db.TraitDefinitions.AsNoTracking()
            .Where(t => t.Key == key)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<TraitDefinitionEntry>> GetByAppliesToAsync(string entityType)
    {
        var entities = await db.TraitDefinitions.AsNoTracking()
            .Where(t => t.AppliesTo == "*" || (t.AppliesTo != null && t.AppliesTo.Contains(entityType)))
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static TraitDefinitionEntry MapToModel(Entities.TraitDefinition t) =>
        new(t.Key, t.ValueType, t.Description, t.AppliesTo);
}
