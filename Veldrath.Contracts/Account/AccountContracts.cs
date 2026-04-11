namespace Veldrath.Contracts.Account;

// ── Self-Service Profile DTOs ─────────────────────────────────────────────────

/// <summary>A linked OAuth provider entry on a player account.</summary>
/// <param name="ProviderName">Provider display name (e.g. <c>"Discord"</c>, <c>"Google"</c>).</param>
/// <param name="ProviderKey">Opaque provider-assigned user identifier.</param>
/// <param name="LinkedAt">UTC timestamp when this provider was linked to the account.</param>
public record LinkedProviderDto(string ProviderName, string ProviderKey, DateTimeOffset LinkedAt);

/// <summary>Full self-service view of the authenticated user's own account.</summary>
/// <param name="AccountId">Unique account identifier.</param>
/// <param name="Username">Login username.</param>
/// <param name="DisplayName">Optional public display name distinct from the login username.</param>
/// <param name="Bio">Optional short biography displayed on the public player profile.</param>
/// <param name="Email">Account email address.</param>
/// <param name="HasPassword">
/// Whether a password is set. <c>false</c> for OAuth-only accounts that have never set a password.
/// </param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="LastSeenAt">Most recent activity timestamp, or <c>null</c> if the account was never seen.</param>
/// <param name="Roles">All role names currently assigned to this account.</param>
/// <param name="Permissions">Effective permission set (union of role and per-user grants).</param>
/// <param name="LinkedProviders">OAuth providers linked to this account.</param>
/// <param name="ActiveSessionCount">Number of active refresh token sessions (including the current one).</param>
public record AccountProfileDto(
    Guid AccountId,
    string Username,
    string? DisplayName,
    string? Bio,
    string? Email,
    bool HasPassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions,
    IReadOnlyList<LinkedProviderDto> LinkedProviders,
    int ActiveSessionCount);

// ── Self-Service Request Records ──────────────────────────────────────────────

/// <summary>Updates the optional public profile fields on the authenticated account.</summary>
/// <param name="DisplayName">New display name, or <c>null</c> to clear it.</param>
/// <param name="Bio">New biography text, or <c>null</c> to clear it.</param>
public record UpdateProfileRequest(string? DisplayName, string? Bio);

/// <summary>Changes the password of the authenticated account.</summary>
/// <param name="CurrentPassword">The account's current password, required to authorise the change.</param>
/// <param name="NewPassword">The new password to set.</param>
public record ChangePasswordRequest(string CurrentPassword, string NewPassword);

/// <summary>Changes the username of the authenticated account.</summary>
/// <param name="NewUsername">The desired new username. Must not already be taken by another account.</param>
public record ChangeUsernameRequest(string NewUsername);

// ── Session DTOs ──────────────────────────────────────────────────────────────

/// <summary>A single active refresh-token session belonging to the authenticated account.</summary>
/// <param name="Id">Session unique identifier (corresponds to the refresh token ID).</param>
/// <param name="CreatedByIp">IP address from which this session was originally created.</param>
/// <param name="CreatedAt">UTC timestamp when the session was created.</param>
/// <param name="ExpiresAt">UTC timestamp when this session will expire.</param>
/// <param name="IsCurrent">
/// <c>true</c> when this session corresponds to the token used in the current request.
/// </param>
public record AccountSessionDto(
    Guid Id,
    string CreatedByIp,
    DateTimeOffset CreatedAt,
    DateTimeOffset ExpiresAt,
    bool IsCurrent);

/// <summary>Revokes all active sessions for the authenticated account except the specified one.</summary>
/// <param name="CurrentSessionId">The refresh-token session ID to preserve.</param>
public record RevokeOtherSessionsRequest(Guid CurrentSessionId);

/// <summary>Identifies an OAuth provider login entry to remove from the authenticated account.</summary>
/// <param name="ProviderKey">The provider-assigned user identifier for the login to unlink.</param>
public record UnlinkProviderRequest(string ProviderKey);

// ── Admin Utilities ───────────────────────────────────────────────────────────

/// <summary>Status of a single health check reported by the server.</summary>
/// <param name="Name">Health-check name as registered in the DI container.</param>
/// <param name="Status">
/// Textual status: <c>"Healthy"</c>, <c>"Degraded"</c>, or <c>"Unhealthy"</c>.
/// </param>
/// <param name="Description">Optional description or error message for this check.</param>
public record HealthCheckEntryDto(string Name, string Status, string? Description);
