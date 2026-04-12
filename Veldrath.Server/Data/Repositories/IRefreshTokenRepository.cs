using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

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

    /// <summary>
    /// Follows the <c>ReplacedByTokenId</c> rotation chain starting at <paramref name="startId"/> and
    /// returns the first <see cref="RefreshToken"/> in the chain whose <see cref="RefreshToken.IsActive"/> is
    /// <see langword="true"/>, or <see langword="null"/> if the chain dead-ends without an active token.
    /// Walks at most 10 hops to guard against cycles.
    /// </summary>
    Task<RefreshToken?> GetCurrentActiveInChainAsync(Guid startId, CancellationToken ct = default);
}
