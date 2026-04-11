using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>Repository for <see cref="Announcement"/> entries.</summary>
public interface IAnnouncementRepository
{
    /// <summary>
    /// Returns all active, non-expired announcements ordered by pinned entries first,
    /// then newest first.
    /// </summary>
    Task<List<Announcement>> GetActiveAsync(CancellationToken ct = default);
}

/// <inheritdoc/>
public class AnnouncementRepository(ApplicationDbContext db) : IAnnouncementRepository
{
    /// <inheritdoc/>
    public Task<List<Announcement>> GetActiveAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        return db.Announcements
                  .Where(a => a.IsActive && (a.ExpiresAt == null || a.ExpiresAt > now))
                  .OrderByDescending(a => a.IsPinned)
                  .ThenByDescending(a => a.PublishedAt)
                  .ToListAsync(ct);
    }
}
