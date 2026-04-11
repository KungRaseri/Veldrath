using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>Persistence contract for <see cref="PendingLinkToken"/>.</summary>
public interface IPendingLinkRepository
{
    /// <summary>Persists a new pending link token and returns the saved entity.</summary>
    Task<PendingLinkToken> CreateAsync(PendingLinkToken token, CancellationToken ct = default);

    /// <summary>
    /// Returns the token matching the given SHA-256 hash, or <see langword="null"/> if not found.
    /// The caller is responsible for checking <see cref="PendingLinkToken.IsConfirmed"/> and
    /// <see cref="PendingLinkToken.ExpiresAt"/> before acting on the result.
    /// </summary>
    Task<PendingLinkToken?> GetByTokenHashAsync(string tokenHash, CancellationToken ct = default);

    /// <summary>Marks the specified token as confirmed so it cannot be reused.</summary>
    Task ConfirmAsync(Guid id, CancellationToken ct = default);

    /// <summary>Deletes all tokens whose <see cref="PendingLinkToken.ExpiresAt"/> is in the past.</summary>
    Task PurgeExpiredAsync(CancellationToken ct = default);
}
