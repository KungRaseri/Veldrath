namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Membership of one character in one multiplayer party.
/// </summary>
/// <remarks>
/// The database enforces one party per character with a unique index on <see cref="CharacterId"/>, so "a character is
/// in at most one party" is a constraint rather than a convention. Named <c>…Entity</c> to stay unambiguous beside the
/// save-game <c>RealmEngine.Shared.Models.PartyMember</c>.
/// </remarks>
public class PartyMemberEntity
{
    /// <summary>Gets or sets the party.</summary>
    public Guid PartyId { get; set; }

    /// <summary>Gets or sets the member character.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Gets or sets when the character joined (UTC).</summary>
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the party.</summary>
    public Party? Party { get; set; }

    /// <summary>Gets or sets the member character.</summary>
    public Character? Character { get; set; }
}
