using Microsoft.AspNetCore.Identity;

namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Extends ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/> with game-specific fields.
/// Identity manages: username, email, password hash, lockout, tokens (email confirm, 2FA, external login).
/// We manage: character slot capacity, game timestamps, and moderation state.
/// </summary>
public class PlayerAccount : IdentityUser<Guid>
{
    /// <summary>Maximum number of character slots available on this account. Default: 5.</summary>
    public int MaxCharacterSlots { get; set; } = 5;

    /// <summary>UTC timestamp when the account was created.</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>UTC timestamp of the most recent observed activity, or <c>null</c> if never seen.</summary>
    public DateTimeOffset? LastSeenAt { get; set; }

    // ── Moderation ───────────────────────────────────────────────────────────

    /// <summary>
    /// When <c>true</c> the account is banned and cannot connect to the game server.
    /// A temporary ban is lifted once <see cref="BannedUntil"/> has passed (checked at connection time).
    /// </summary>
    public bool IsBanned { get; set; } = false;

    /// <summary>
    /// UTC expiry of a temporary ban. <c>null</c> means the ban is permanent (when <see cref="IsBanned"/> is <c>true</c>).
    /// </summary>
    public DateTimeOffset? BannedUntil { get; set; }

    /// <summary>Human-readable reason recorded at ban time and shown to the player on reconnect.</summary>
    public string? BanReason { get; set; }
}
