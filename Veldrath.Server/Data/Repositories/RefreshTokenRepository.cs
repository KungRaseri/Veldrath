using Microsoft.EntityFrameworkCore;
using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>EF Core implementation of <see cref="IRefreshTokenRepository"/>.</summary>
public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly ApplicationDbContext _db;

    public RefreshTokenRepository(ApplicationDbContext db) => _db = db;

    public Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default) =>
        _db.RefreshTokens
            .Include(rt => rt.Account)
            .FirstOrDefaultAsync(rt => rt.TokenHash == tokenHash, ct);

    public async Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken ct = default)
    {
        _db.RefreshTokens.Add(token);
        await _db.SaveChangesAsync(ct);
        return token;
    }

    public async Task RevokeAsync(Guid id, string revokedByIp, Guid? replacedByTokenId = null, CancellationToken ct = default)
    {
        var token = await _db.RefreshTokens.FindAsync([id], ct);
        if (token is null || !token.IsActive) return;

        token.RevokedAt          = DateTimeOffset.UtcNow;
        token.RevokedByIp        = revokedByIp;
        token.ReplacedByTokenId  = replacedByTokenId;
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForAccountAsync(Guid accountId, string revokedByIp, CancellationToken ct = default)
    {
        var active = await _db.RefreshTokens
            .Where(rt => rt.AccountId == accountId && rt.RevokedAt == null)
            .ToListAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var token in active)
        {
            token.RevokedAt   = now;
            token.RevokedByIp = revokedByIp;
        }

        await _db.SaveChangesAsync(ct);
    }
}
