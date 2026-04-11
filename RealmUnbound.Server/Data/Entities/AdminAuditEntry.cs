namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Immutable record of a moderation or administrative action taken by a staff member.
/// Written on every ban, kick, warn, mute, role assignment, and permission change.
/// Entries are never deleted — they form the permanent audit trail.
/// </summary>
public class AdminAuditEntry
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Account ID of the staff member who performed the action.</summary>
    public Guid ActorAccountId { get; set; }

    /// <summary>Username of the staff member at the time of the action (denormalised for log readability).</summary>
    public string ActorUsername { get; set; } = string.Empty;

    /// <summary>Account ID of the player targeted by the action, or <c>null</c> for server-wide actions (e.g. broadcast).</summary>
    public Guid? TargetAccountId { get; set; }

    /// <summary>Username of the targeted player at the time of the action, or <c>null</c> for server-wide actions.</summary>
    public string? TargetUsername { get; set; }

    /// <summary>Short action key, e.g. <c>"ban"</c>, <c>"kick"</c>, <c>"warn"</c>, <c>"assign_role"</c>.</summary>
    public string Action { get; set; } = string.Empty;

    /// <summary>Optional human-readable detail string (reason, role name, permission name, etc.).</summary>
    public string? Details { get; set; }

    /// <summary>UTC timestamp when the action occurred.</summary>
    public DateTimeOffset OccurredAt { get; set; } = DateTimeOffset.UtcNow;
}
