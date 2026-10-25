using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IGroundItemRepository"/> backed by
/// <see cref="ApplicationDbContext"/>.
/// </summary>
public class EfCoreGroundItemRepository(ApplicationDbContext db) : IGroundItemRepository
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<GroundItem>> GetAtLocationAsync(
        string zoneId,
        string locationSlug,
        CancellationToken cancellationToken = default) =>
        await db.GroundItems
            .Where(item => item.ZoneId == zoneId && item.LocationSlug == locationSlug)
            .OrderBy(item => item.DroppedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<GroundItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.GroundItems.FirstOrDefaultAsync(item => item.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task AddAsync(GroundItem item, CancellationToken cancellationToken = default)
    {
        db.GroundItems.Add(item);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task RemoveAsync(GroundItem item, CancellationToken cancellationToken = default)
    {
        db.GroundItems.Remove(item);
        await db.SaveChangesAsync(cancellationToken);
    }
}
