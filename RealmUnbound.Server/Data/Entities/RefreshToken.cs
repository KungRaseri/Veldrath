namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Tracks a JWT refresh token for a <see cref="PlayerAccount"/>.
/// Tokens are stored as a SHA-256 hash — the raw token is never persisted.
/// Rotation strategy: each exchange revokes the old token and records the replacement ID.
/// If a revoked token is presented again, all tokens for the account are revoked immediately
/// (token theft detection).
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK to the owning <see cref="PlayerAccount"/>.</summary>
    public Guid AccountId { get; set; }

    /// <summary>SHA-256 hex digest of the raw token value. Never store the raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public string CreatedByIp { get; set; } = string.Empty;

    /// <summary>Populated when this token is rotated out or explicitly revoked.</summary>
    public DateTimeOffset? RevokedAt { get; set; }

    public string? RevokedByIp { get; set; }

    /// <summary>ID of the token issued to replace this one (rotation chain).</summary>
    public Guid? ReplacedByTokenId { get; set; }

    public bool IsActive => RevokedAt is null && DateTimeOffset.UtcNow < ExpiresAt;

    // Navigation
    public PlayerAccount Account { get; set; } = null!;
}
