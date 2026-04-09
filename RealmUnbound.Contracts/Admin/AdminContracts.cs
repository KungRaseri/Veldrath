namespace RealmUnbound.Contracts.Admin;

// ── User / Account DTOs ──────────────────────────────────────────────────────

/// <summary>Lightweight account row used in paginated admin lists.</summary>
/// <param name="Id">Account unique identifier.</param>
/// <param name="Username">Display username.</param>
/// <param name="Email">Account email address.</param>
/// <param name="Roles">Roles currently assigned to this account.</param>
/// <param name="IsBanned">Whether the account is currently banned.</param>
/// <param name="BannedUntil">When a temporary ban expires; <c>null</c> if permanent or not banned.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="LastSeenAt">Last observed activity timestamp.</param>
public record PlayerSummaryDto(
    Guid Id,
    string Username,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsBanned,
    DateTimeOffset? BannedUntil,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt);

/// <summary>Full account detail including effective permissions.</summary>
/// <param name="Id">Account unique identifier.</param>
/// <param name="Username">Display username.</param>
/// <param name="Email">Account email address.</param>
/// <param name="Roles">Roles currently assigned to this account.</param>
/// <param name="EffectivePermissions">Union of all role and per-user permission grants.</param>
/// <param name="UserPermissions">Per-user permission grants (not inherited from a role).</param>
/// <param name="IsBanned">Whether the account is currently banned.</param>
/// <param name="BannedUntil">When a temporary ban expires; <c>null</c> if permanent or not banned.</param>
/// <param name="BanReason">Reason recorded at ban time.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="LastSeenAt">Last observed activity timestamp.</param>
public record PlayerDetailDto(
    Guid Id,
    string Username,
    string? Email,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> EffectivePermissions,
    IReadOnlyList<string> UserPermissions,
    bool IsBanned,
    DateTimeOffset? BannedUntil,
    string? BanReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastSeenAt);

// ── Role / Permission Requests ───────────────────────────────────────────────

/// <summary>Assigns a role to a player account.</summary>
/// <param name="Role">Role name to assign (see <c>Roles</c> constants on the server).</param>
public record AssignRoleRequest(string Role);

/// <summary>Grants a single permission claim directly to a player account.</summary>
/// <param name="Permission">Permission string to grant (see <c>Permissions</c> constants on the server).</param>
public record GrantPermissionRequest(string Permission);

// ── Moderation Requests ──────────────────────────────────────────────────────

/// <summary>Forcibly disconnects an active player session.</summary>
/// <param name="AccountId">Target account whose active sessions will be disconnected.</param>
/// <param name="Reason">Optional reason surfaced in the kick notification sent to the client.</param>
public record KickPlayerRequest(Guid AccountId, string? Reason = null);

/// <summary>Bans a player account, optionally for a limited duration.</summary>
/// <param name="AccountId">Target account to ban.</param>
/// <param name="Reason">Human-readable reason stored on the account and shown to the player.</param>
/// <param name="DurationMinutes">
/// Duration of the ban in minutes. <c>null</c> or omitted means permanent.
/// </param>
public record BanPlayerRequest(Guid AccountId, string Reason, int? DurationMinutes = null);

// ── Broadcast ────────────────────────────────────────────────────────────────

/// <summary>Broadcasts an announcement message to all connected game clients.</summary>
/// <param name="Message">Announcement text displayed in-game.</param>
/// <param name="Severity">
/// Optional severity hint for client-side styling.
/// Suggested values: <c>"info"</c>, <c>"warning"</c>, <c>"critical"</c>.
/// Defaults to <c>"info"</c> when omitted.
/// </param>
public record BroadcastAnnouncementRequest(string Message, string Severity = "info");

// ── Response ─────────────────────────────────────────────────────────────────

/// <summary>Generic success/failure envelope returned by admin action endpoints.</summary>
/// <param name="Success">Whether the operation succeeded.</param>
/// <param name="Message">Human-readable outcome message.</param>
public record AdminActionResponse(bool Success, string Message);
