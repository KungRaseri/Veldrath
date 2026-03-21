using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>EF Core-backed repository for organization catalog data.</summary>
public class EfCoreOrganizationRepository(ContentDbContext db, ILogger<EfCoreOrganizationRepository> logger)
    : IOrganizationRepository
{
    /// <inheritdoc />
    public async Task<List<OrganizationEntry>> GetAllAsync()
    {
        var entities = await db.Organizations.AsNoTracking()
            .Where(o => o.IsActive)
            .ToListAsync();

        logger.LogDebug("Loaded {Count} organizations from database", entities.Count);
        return entities.Select(MapToModel).ToList();
    }

    /// <inheritdoc />
    public async Task<OrganizationEntry?> GetBySlugAsync(string slug)
    {
        var entity = await db.Organizations.AsNoTracking()
            .Where(o => o.IsActive && o.Slug == slug)
            .FirstOrDefaultAsync();

        return entity is null ? null : MapToModel(entity);
    }

    /// <inheritdoc />
    public async Task<List<OrganizationEntry>> GetByTypeAsync(string orgType)
    {
        var lower = orgType.ToLowerInvariant();
        var entities = await db.Organizations.AsNoTracking()
            .Where(o => o.IsActive && o.OrgType.ToLower() == lower)
            .ToListAsync();

        return entities.Select(MapToModel).ToList();
    }

    private static OrganizationEntry MapToModel(Entities.Organization o) =>
        new(o.Slug, o.DisplayName ?? o.Slug, o.TypeKey, o.OrgType, o.RarityWeight);
}
