using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Read-only repository for <see cref="Region"/> catalog entries.</summary>
public interface IRegionRepository
{
    /// <summary>Returns all regions in the world, ordered by minimum level.</summary>
    Task<List<Region>> GetAllAsync();

    /// <summary>Returns the region with the given <paramref name="regionId"/>, or <see langword="null"/> if not found.</summary>
    Task<Region?> GetByIdAsync(string regionId);

    /// <summary>Returns all regions connected (adjacent) to the region with the given <paramref name="regionId"/>.</summary>
    Task<List<Region>> GetConnectedAsync(string regionId);

    /// <summary>Returns the region flagged as the starter region, or <see langword="null"/> if none is configured.</summary>
    Task<Region?> GetStarterAsync();
}

/// <summary>EF Core implementation of <see cref="IRegionRepository"/>.</summary>
public class RegionRepository(ApplicationDbContext db) : IRegionRepository
{
    /// <inheritdoc/>
    public Task<List<Region>> GetAllAsync() =>
        db.Regions.OrderBy(r => r.MinLevel).ThenBy(r => r.Name).ToListAsync();

    /// <inheritdoc/>
    public Task<Region?> GetByIdAsync(string regionId) =>
        db.Regions.FirstOrDefaultAsync(r => r.Id == regionId);

    /// <inheritdoc/>
    public Task<List<Region>> GetConnectedAsync(string regionId) =>
        db.RegionConnections
          .Where(rc => rc.FromRegionId == regionId)
          .Select(rc => rc.ToRegion)
          .OrderBy(r => r.MinLevel)
          .ToListAsync();

    /// <inheritdoc/>
    public Task<Region?> GetStarterAsync() =>
        db.Regions.FirstOrDefaultAsync(r => r.IsStarter);
}
