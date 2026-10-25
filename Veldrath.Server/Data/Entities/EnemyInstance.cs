namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A live enemy standing at a zone location, shared by every character present there.
/// </summary>
/// <remarks>
/// <para>
/// This row replaces the in-process <c>ZoneLocationEnemyStore</c>: health, participants and the pending
/// respawn are durable, so a restart or a rolling deploy no longer wipes the world.
/// </para>
/// <para>
/// The location key is the SignalR zone group (<see cref="ZoneGroup"/>, e.g. <c>"zone:dark-forest_hardcore"</c>)
/// plus <see cref="LocationSlug"/>, which is what every spawn and broadcast path already uses. The group name
/// encodes the difficulty mode as well as the zone, so it is stored verbatim rather than split into parts.
/// </para>
/// </remarks>
public class EnemyInstance
{
    /// <summary>Gets or sets the instance identifier used to target this specific enemy.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the SignalR zone group the enemy is visible in.</summary>
    public string ZoneGroup { get; set; } = string.Empty;

    /// <summary>Gets or sets the zone-location slug the enemy stands at.</summary>
    public string LocationSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the actor archetype slug this enemy was generated from, used to respawn it.</summary>
    public string ArchetypeSlug { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the display name captured at spawn time. Snapshotted on purpose: renaming or deleting the
    /// archetype must not rename an enemy that is already mid-fight.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the combat level captured at spawn time.</summary>
    public int Level { get; set; }

    /// <summary>Gets or sets the current health points.</summary>
    public int CurrentHealth { get; set; }

    /// <summary>Gets or sets the maximum health points.</summary>
    public int MaxHealth { get; set; }

    /// <summary>Gets or sets the base experience awarded on death, captured at spawn time.</summary>
    public int BaseXp { get; set; }

    /// <summary>Gets or sets the base gold awarded on death, captured at spawn time.</summary>
    public int GoldReward { get; set; }

    /// <summary>Gets or sets a value indicating whether the enemy still has health.</summary>
    public bool IsAlive { get; set; } = true;

    /// <summary>Gets or sets when the enemy was spawned (UTC).</summary>
    public DateTimeOffset SpawnedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// Gets or sets when a dead enemy comes back (UTC). <see langword="null"/> while the enemy is alive; set on
    /// death and cleared by the respawn sweeper, so a restart mid-timer simply continues the countdown.
    /// </summary>
    public DateTimeOffset? RespawnAt { get; set; }

    /// <summary>
    /// Gets or sets the concurrency token. The store layer increments it on every write so a second process
    /// cannot serve or overwrite stale health. See the Milestone D plan for the read-through cache contract.
    /// </summary>
    public long Version { get; set; }

    /// <summary>Gets the characters that have engaged this enemy.</summary>
    public ICollection<EnemyParticipant> Participants { get; } = new List<EnemyParticipant>();

    /// <summary>Gets the combat sessions currently targeting this enemy.</summary>
    public ICollection<CombatSession> CombatSessions { get; } = new List<CombatSession>();
}
