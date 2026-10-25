namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A multiplayer party: a group of characters that travel and fight together.
/// </summary>
/// <remarks>
/// Deliberately separate from <c>RealmEngine.Shared.Models.Party</c>, which is the single-player NPC party that lives
/// in a save file. Sharing that model would tie a multiplayer invariant (one party per character, enforced by the
/// database) to a save-game structure.
/// </remarks>
public class Party
{
    /// <summary>Gets or sets the party identifier.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the character that leads the party.</summary>
    public Guid LeaderCharacterId { get; set; }

    /// <summary>Gets or sets when the party was created (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the loot rule. <c>contribution</c> (the default) splits rewards by damage dealt; further rules
    /// such as <c>leader-assign</c> can be added without a schema change.
    /// </summary>
    public string LootRule { get; set; } = LootRuleContribution;

    /// <summary>Gets the loot rule value that splits rewards by damage contribution.</summary>
    public const string LootRuleContribution = "contribution";

    /// <summary>Gets or sets the party leader.</summary>
    public Character? Leader { get; set; }

    /// <summary>Gets the party's members, including the leader.</summary>
    public ICollection<PartyMemberEntity> Members { get; } = new List<PartyMemberEntity>();

    /// <summary>Gets the party's outstanding invites.</summary>
    public ICollection<PartyInvite> Invites { get; } = new List<PartyInvite>();
}
