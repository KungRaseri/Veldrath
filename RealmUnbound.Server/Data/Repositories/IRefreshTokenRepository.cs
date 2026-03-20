using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Persistence contract for <see cref="RefreshToken"/>.</summary>
public interface IRefreshTokenRepository
{
    /// <summary>Returns the token matching the given hash, or <see langword="null"/> if not found or already revoked.</summary>
    Task<RefreshToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Persists a new refresh token and returns the saved entity.</summary>
    Task<RefreshToken> CreateAsync(RefreshToken token, CancellationToken ct = default);

    /// <summary>Revoke a single token. Optionally record the replacement token ID (rotation).</summary>
    Task RevokeAsync(Guid id, string revokedByIp, Guid? replacedByTokenId = null, CancellationToken ct = default);

    /// <summary>
    /// Revoke all active tokens for an account.
    /// Called on suspicious activity (presented-revoked-token detection) or admin action.
    /// </summary>
    Task RevokeAllForAccountAsync(Guid accountId, string revokedByIp, CancellationToken ct = default);
}
