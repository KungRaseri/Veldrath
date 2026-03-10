using Microsoft.AspNetCore.Identity;

namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Extends ASP.NET Core Identity's <see cref="IdentityUser{TKey}"/> with game-specific fields.
/// Identity manages: username, email, password hash, lockout, tokens (email confirm, 2FA, external login).
/// We manage: character slot capacity and game timestamps.
/// </summary>
public class PlayerAccount : IdentityUser<Guid>
{
    /// <summary>Maximum number of character slots available on this account. Default: 5.</summary>
    public int MaxCharacterSlots { get; set; } = 5;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? LastSeenAt { get; set; }
}
