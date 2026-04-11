using Microsoft.EntityFrameworkCore;
using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>EF Core implementation of <see cref="IPendingLinkRepository"/>.</summary>
public class EfCorePendingLinkRepository(ApplicationDbContext db) : IPendingLinkRepository
{
    /// <inheritdoc />
    public async Task<PendingLinkToken> CreateAsync(PendingLinkToken token, CancellationToken ct = default)
    {
        db.PendingLinkTokens.Add(token);
        await db.SaveChangesAsync(ct);
        return token;
    }

    /// <inheritdoc />
    public Task<PendingLinkToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        db.PendingLinkTokens
          .Include(t => t.Account)
          .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, ct);

    /// <inheritdoc />
    public async Task ConfirmAsync(Guid id, CancellationToken ct = default)
    {
        var token = await db.PendingLinkTokens.FindAsync([id], ct);
        if (token is null) return;
        token.IsConfirmed = true;
        await db.SaveChangesAsync(ct);
    }

    /// <inheritdoc />
    public async Task PurgeExpiredAsync(CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow;
        await db.PendingLinkTokens
                .Where(t => t.ExpiresAt <= cutoff)
                .ExecuteDeleteAsync(ct);
    }
}
