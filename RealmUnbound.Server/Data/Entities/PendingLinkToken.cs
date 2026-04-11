namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Tracks a pending OAuth provider-link confirmation request.
/// When a new external login arrives whose email matches an existing account
/// that does not yet have that provider linked, a <see cref="PendingLinkToken"/>
/// is created and a confirmation email is sent. The provider is not attached
/// to the account until the user clicks the confirmation link, which presents
/// the raw token to <c>GET /api/auth/link/confirm</c>.
/// </summary>
/// <remarks>
/// Tokens are stored as a SHA-256 hex hash — the raw random value is only carried
/// in the confirmation URL and is never persisted.
/// Expired or already-confirmed tokens are rejected and the row is left in place
/// until a purge pass removes it.
/// </remarks>
public class PendingLinkToken
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>FK to the existing <see cref="PlayerAccount"/> that the new provider will be linked to on confirmation.</summary>
    public Guid AccountId { get; set; }

    /// <summary>OAuth scheme name, e.g. <c>Discord</c>, <c>Google</c>, <c>Microsoft</c>.</summary>
    public string LoginProvider { get; set; } = string.Empty;

    /// <summary>Provider-issued unique identifier for the account on that provider.</summary>
    public string ProviderKey { get; set; } = string.Empty;

    /// <summary>Human-readable display name from the provider (may be null if the provider did not supply one).</summary>
    public string? ProviderDisplayName { get; set; }

    /// <summary>SHA-256 hex digest of the raw confirmation token. Never store the raw token.</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Email address the confirmation was sent to (and the address that matched the existing account).</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>
    /// Optional return URL to redirect to after a successful confirmation, e.g. the Foundry profile page.
    /// Must satisfy <c>IsAllowedReturnUrl</c> at token creation time; null means fall back to <c>/login</c>.
    /// </summary>
    public string? ReturnUrl { get; set; }

    /// <summary>UTC timestamp after which this token is rejected.</summary>
    public DateTimeOffset ExpiresAt { get; set; }

    /// <summary>UTC timestamp when the token was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary><see langword="true"/> after the user successfully clicks the confirmation link.</summary>
    public bool IsConfirmed { get; set; }

    // Navigation
    /// <summary>The <see cref="PlayerAccount"/> that owns this pending link.</summary>
    public PlayerAccount Account { get; set; } = null!;
}
