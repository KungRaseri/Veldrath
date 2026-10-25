namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A pending invitation for a character to join a party.
/// </summary>
/// <remarks>
/// Invites are rows rather than in-memory state so an offline invitee still sees them on their next login, and so a
/// restart does not silently drop an invite mid-conversation. <see cref="ExpiresAt"/> bounds how long an unanswered
/// invite lingers.
/// </remarks>
public class PartyInvite
{
    /// <summary>Gets or sets the invite identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the party being offered.</summary>
    public Guid PartyId { get; set; }

    /// <summary>Gets or sets the character being invited.</summary>
    public Guid InvitedCharacterId { get; set; }

    /// <summary>Gets or sets the character who sent the invite.</summary>
    public Guid InvitedByCharacterId { get; set; }

    /// <summary>Gets or sets when the invite stops being valid (UTC).</summary>
    public DateTimeOffset ExpiresAt { get; set; } = DateTimeOffset.UtcNow.AddMinutes(5);

    /// <summary>Gets or sets the party being offered.</summary>
    public Party? Party { get; set; }

    /// <summary>Gets or sets the character being invited.</summary>
    public Character? InvitedCharacter { get; set; }

    /// <summary>Gets or sets the character who sent the invite.</summary>
    public Character? InvitedByCharacter { get; set; }
}
