namespace RealmUnbound.Contracts.Admin;

// ── User / Account DTOs ──────────────────────────────────────────────────────

/// <summary>Lightweight account row used in paginated admin lists.</summary>
/// <param name="Id">Account unique identifier.</param>
/// <param name="Username">Display username.</param>
/// <param name="Email">Account email address.</param>
/// <param name="Roles">Roles currently assigned to this account.</param>
/// <param name="IsBanned">Whether the account is currently banned.</param>
/// <param name="BannedUntil">When a temporary ban expires; <c>null</c> if permanent or not banned.</param>
/// <param name="WarnCount">Number of formal warnings the account has received.</param>
/// <param name="IsMuted">Whether the account is currently muted in chat.</param>
/// <param name="CreatedAt">Account creation timestamp.</param>
/// <param name="LastSeenAt">Last observed activity timestamp.</param>
public record PlayerSummaryDto(
    Guid Id,
    string Username,
    string? Email,
    IReadOnlyList<string> Roles,
    bool IsBanned,
    DateTimeOffset? BannedUntil,
    int WarnCount,
    bool IsMuted,
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
/// <param name="WarnCount">Number of formal warnings the account has received.</param>
/// <param name="IsMuted">Whether the account is currently muted in chat.</param>
/// <param name="MutedUntil">When a temporary mute expires; <c>null</c> if permanent or not muted.</param>
/// <param name="MuteReason">Reason recorded at mute time.</param>
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
    int WarnCount,
    bool IsMuted,
    DateTimeOffset? MutedUntil,
    string? MuteReason,
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

// ── Warn / Mute Requests ─────────────────────────────────────────────────────

/// <summary>Issues a formal warning to a player account.</summary>
/// <param name="AccountId">Target account to warn.</param>
/// <param name="Reason">Human-readable reason shown to the player and stored in the audit log.</param>
public record WarnPlayerRequest(Guid AccountId, string Reason);

/// <summary>Mutes a player account so they cannot send chat messages.</summary>
/// <param name="AccountId">Target account to mute.</param>
/// <param name="Reason">Optional reason stored on the account and shown to the player.</param>
/// <param name="DurationMinutes">Duration of the mute in minutes. <c>null</c> means permanent.</param>
public record MutePlayerRequest(Guid AccountId, string? Reason = null, int? DurationMinutes = null);

/// <summary>Lifts an active ban from a player account.</summary>
/// <param name="AccountId">Target account to unban.</param>
public record UnbanPlayerRequest(Guid AccountId);

/// <summary>Lifts an active chat mute from a player account.</summary>
/// <param name="AccountId">Target account to unmute.</param>
public record UnmutePlayerRequest(Guid AccountId);

// ── Web Reports ──────────────────────────────────────────────────────────────

/// <summary>Submits a player report from the Foundry web portal against another account.</summary>
/// <param name="TargetUsername">Username of the account being reported.</param>
/// <param name="Reason">Reason for the report shown to staff.</param>
public record SubmitReportRequest(string TargetUsername, string Reason);

// ── Audit Log DTOs ───────────────────────────────────────────────────────────

/// <summary>Single entry from the admin audit trail.</summary>
/// <param name="Id">Entry unique identifier.</param>
/// <param name="ActorUsername">Username of the staff member who took the action.</param>
/// <param name="TargetAccountId">Account ID of the player targeted, or <c>null</c> for server-wide actions.</param>
/// <param name="TargetUsername">Username of the targeted player, or <c>null</c> for server-wide actions.</param>
/// <param name="Action">Short action key (e.g. <c>"ban"</c>, <c>"warn"</c>, <c>"assign_role"</c>).</param>
/// <param name="Details">Optional detail string (reason, role name, permission name, etc.).</param>
/// <param name="OccurredAt">UTC timestamp of the action.</param>
public record AuditEntryDto(
    Guid Id,
    string ActorUsername,
    Guid? TargetAccountId,
    string? TargetUsername,
    string Action,
    string? Details,
    DateTimeOffset OccurredAt);

// ── Player Report DTOs ───────────────────────────────────────────────────────

/// <summary>A player-submitted report about another player's behaviour.</summary>
/// <param name="Id">Report unique identifier.</param>
/// <param name="ReporterName">Character name of the reporting player.</param>
/// <param name="TargetName">Character name of the reported player.</param>
/// <param name="Reason">Reason provided by the reporter.</param>
/// <param name="SubmittedAt">UTC timestamp when the report was submitted.</param>
/// <param name="IsResolved">Whether a staff member has closed this report.</param>
/// <param name="ResolvedAt">UTC timestamp when resolved, or <c>null</c> if still open.</param>
public record PlayerReportDto(
    Guid Id,
    string ReporterName,
    string TargetName,
    string Reason,
    DateTimeOffset SubmittedAt,
    bool IsResolved,
    DateTimeOffset? ResolvedAt);

// ── Server Status DTOs ───────────────────────────────────────────────────────

/// <summary>Zone-level player count used in the server status snapshot.</summary>
/// <param name="ZoneId">Zone identifier.</param>
/// <param name="ZoneName">Display name of the zone.</param>
/// <param name="PlayerCount">Number of players currently in this zone.</param>
public record ZonePopulationDto(string ZoneId, string ZoneName, int PlayerCount);

/// <summary>Live server health snapshot returned by <c>GET /api/admin/status</c>.</summary>
/// <param name="ConnectedPlayers">Total number of active player sessions.</param>
/// <param name="ZonePopulations">Per-zone player counts, sorted descending by population.</param>
/// <param name="ServerStartedAt">UTC timestamp when the server process started.</param>
public record ServerStatusDto(
    int ConnectedPlayers,
    IReadOnlyList<ZonePopulationDto> ZonePopulations,
    DateTimeOffset ServerStartedAt);

// ── Active Sessions ──────────────────────────────────────────────────────────

/// <summary>Snapshot of a single active player session for the admin sessions view.</summary>
/// <param name="CharacterId">Character unique identifier.</param>
/// <param name="CharacterName">Character display name.</param>
/// <param name="AccountId">Owner account identifier.</param>
/// <param name="AccountUsername">Owner account username.</param>
/// <param name="RegionId">Region the character is currently in.</param>
/// <param name="ZoneId">Zone the character is currently in, or <c>null</c> if on the region map.</param>
/// <param name="EnteredAt">UTC timestamp when the session started.</param>
public record ActiveSessionDto(
    Guid CharacterId,
    string CharacterName,
    Guid AccountId,
    string AccountUsername,
    string RegionId,
    string? ZoneId,
    DateTimeOffset EnteredAt);
