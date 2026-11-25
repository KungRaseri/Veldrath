namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Lifecycle state of a <see cref="ZoneInstance"/>.
/// </summary>
public enum ZoneInstanceState
{
    /// <summary>The run is live: at least one session may be pointing at it, or it is waiting out the disconnect grace.</summary>
    Open = 0,

    /// <summary>No session points at the run and the grace window has passed; it is waiting to be purged.</summary>
    Empty = 1,

    /// <summary>The run is being deleted; the purge is in progress and it must not be joined.</summary>
    Purging = 2,
}

/// <summary>
/// One run of an instanced zone — the aggregate root a delve's enemies, ground items, members and room
/// progress point at.
/// </summary>
/// <remarks>
/// <para>
/// An instance is a <em>row</em>, not a derived name: the identity is allocated once and stored, and the
/// SignalR group name in <see cref="ZoneGroup"/> is composed from it. Nothing recovers identity by parsing
/// that name, so a malformed group cannot conjure a dungeon nobody can see.
/// </para>
/// <para>
/// The run is zone-level, not room-level: a party keeps one run while it walks the lobby and every room in
/// it, which is what makes "this attempt" a single object and what lets a wipe, a reset and a first-clear
/// all refer to the same thing. Room-level state lives in its own table keyed by the run.
/// </para>
/// <para>
/// <see cref="OwnerPartyId"/> is provenance, not a live foreign key. A party that disbands inside a dungeon
/// does not invalidate the run; the run ends when it empties, which is what
/// <see cref="LastOccupiedAt"/> and <see cref="State"/> exist to record.
/// </para>
/// </remarks>
public class ZoneInstance
{
    /// <summary>Gets or sets the run identifier. Opaque, server-generated, and never shown to a player.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the zone this run belongs to. Loose reference to <see cref="Zone"/>, like the rest of the content refs.</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the party that opened the run, or <see langword="null"/> for a solo run. Provenance only:
    /// the party may disband and the run stays alive until it empties.
    /// </summary>
    public Guid? OwnerPartyId { get; set; }

    /// <summary>Gets or sets the character who opened the run. Doubles as the solo scope key.</summary>
    public Guid CreatedByCharacterId { get; set; }

    /// <summary>
    /// Gets or sets the SignalR group every member of this run joins, for example
    /// <c>zone:the-halrow#i1:2f9c…</c>. Allocated once, here, so no call site has to compose it.
    /// </summary>
    public string ZoneGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the lifecycle state. Only <see cref="ZoneInstanceState.Open"/> runs can be joined.</summary>
    public ZoneInstanceState State { get; set; } = ZoneInstanceState.Open;

    /// <summary>Gets or sets when the run was opened (UTC).</summary>
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the last time the run was seen occupied (UTC). It is refreshed on a join and again on a
    /// departure, so the disconnect grace starts when the last member leaves rather than at the last join. The purge
    /// only considers runs older than that grace, so a relog rejoins the run instead of finding a new one.
    /// </summary>
    public DateTimeOffset LastOccupiedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets the concurrency token. The service increments it on every state transition so a stale
    /// writer cannot reopen or purge a run another process has already moved on from.
    /// </summary>
    public long Version { get; set; }

    /// <summary>Gets the zone the run belongs to.</summary>
    public Zone? Zone { get; set; }

    /// <summary>Gets the characters who have taken part in this run.</summary>
    public ICollection<ZoneInstanceParticipant> Participants { get; } = new List<ZoneInstanceParticipant>();
}

/// <summary>
/// A character's participation in one <see cref="ZoneInstance"/> run.
/// </summary>
/// <remarks>
/// Membership is derived live from <see cref="PlayerSession.ZoneInstanceId"/>, so this row exists for the
/// facts a session cannot answer: who was on the run after they disconnect, who left early, and therefore who
/// earns credit when the run is completed. The existing <see cref="EnemyParticipant"/> answers a different
/// question — who damaged one mob.
/// </remarks>
public class ZoneInstanceParticipant
{
    /// <summary>Gets or sets the run the character took part in.</summary>
    public Guid ZoneInstanceId { get; set; }

    /// <summary>Gets or sets the participating character.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Gets or sets when the character first entered the run (UTC).</summary>
    public DateTimeOffset JoinedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the character left the run (UTC), or <see langword="null"/> while they are inside it.</summary>
    public DateTimeOffset? LeftAt { get; set; }

    /// <summary>Gets the run.</summary>
    public ZoneInstance? Instance { get; set; }

    /// <summary>Gets the participating character.</summary>
    public Character? Character { get; set; }
}

/// <summary>
/// What one run has cleared in one of its rooms.
/// </summary>
/// <remarks>
/// The run is zone-level, so this is where the room-level granularity lives: a boss that stays dead while the
/// party backtracks, and a reset that is a new run rather than a deletion of somebody's live progress.
/// <see cref="FirstClearedByCharacterId"/> records who opened the room for the party; the character's own
/// first-clear is a separate, persistent fact and does not live here.
/// </remarks>
public class ZoneInstanceLocationState
{
    /// <summary>Gets or sets the run this state belongs to.</summary>
    public Guid ZoneInstanceId { get; set; }

    /// <summary>Gets or sets the zone-location slug the state describes.</summary>
    public string LocationSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether every enemy at the room has been defeated in this run.</summary>
    public bool IsCleared { get; set; }

    /// <summary>Gets or sets when the room was cleared (UTC), or <see langword="null"/> while it is still occupied.</summary>
    public DateTimeOffset? ClearedAt { get; set; }

    /// <summary>Gets or sets the character whose kill cleared the room, or <see langword="null"/> while it is uncleared.</summary>
    public Guid? FirstClearedByCharacterId { get; set; }

    /// <summary>Gets the run.</summary>
    public ZoneInstance? Instance { get; set; }
}
