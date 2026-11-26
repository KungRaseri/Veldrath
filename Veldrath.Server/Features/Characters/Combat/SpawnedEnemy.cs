using RealmEngine.Shared.Models;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// A live enemy instance spawned in a zone location whose health and reward data are shared
/// by all players currently at that location.
/// </summary>
public class SpawnedEnemy
{
    /// <summary>Gets or sets the unique instance ID used for targeting this specific enemy.</summary>
    public Guid Id { get; init; } = Guid.NewGuid();

    /// <summary>Gets the SignalR zone group the enemy is visible in, carried from the stored row.</summary>
    public string ZoneGroup { get; init; } = string.Empty;

    /// <summary>Gets the zone-location slug the enemy stands at, carried from the stored row.</summary>
    public string LocationSlug { get; init; } = string.Empty;

    /// <summary>Gets the instance run the enemy belongs to, or <see langword="null"/> for the shared zone.</summary>
    public Guid? ZoneInstanceId { get; init; }

    /// <summary>Gets the archetype slug this enemy was generated from (used to re-spawn on death).</summary>
    public string ArchetypeSlug { get; init; } = string.Empty;

    /// <summary>
    /// Gets the actor instance slug this enemy was spawned from, or <see langword="null"/> when it came from a bare
    /// pool entry. Carried so a reload can rebuild the composed template — the instance alone knows the overrides,
    /// and without it a reloaded named actor would fight at its archetype's damage.
    /// </summary>
    public string? InstanceSlug { get; init; }

    /// <summary>Gets the display name of this enemy.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the combat level of this enemy.</summary>
    public int Level { get; init; }

    /// <summary>Gets or sets the current health points. Mutated with <see cref="Lock"/> held.</summary>
    public int CurrentHealth { get; set; }

    /// <summary>Gets the maximum health points.</summary>
    public int MaxHealth { get; init; }

    /// <summary>Gets the base XP rewarded when this enemy is defeated, before contribution scaling.</summary>
    public int BaseXp { get; init; }

    /// <summary>Gets the base gold rewarded when this enemy is defeated, before contribution scaling.</summary>
    public int GoldReward { get; init; }

    /// <summary>
    /// Gets or sets the character ID of the most recent attacker.
    /// This determines which player the enemy counter-attacks.
    /// </summary>
    public Guid? LastAttackerId { get; set; }

    /// <summary>
    /// Gets or sets whether rewards for this enemy's death have already been distributed,
    /// preventing double-award in concurrent kill scenarios.
    /// </summary>
    public bool WasRewarded { get; set; }

    /// <summary>Gets the set of character IDs who have attacked this enemy at least once.</summary>
    public HashSet<Guid> Participants { get; } = [];

    /// <summary>
    /// Gets the cumulative damage dealt per character ID.
    /// Used to calculate proportional XP and gold distribution on death.
    /// </summary>
    public Dictionary<Guid, int> DamageContributions { get; } = [];

    /// <summary>Gets the enemy template containing stats and spell cooldown state.</summary>
    public Enemy Template { get; init; } = new();

    /// <summary>Gets the sync root object used to synchronise HP mutations across concurrent attackers.</summary>
    public object SyncRoot { get; } = new();

    /// <summary>Gets a value indicating whether this enemy has any remaining health.</summary>
    public bool IsAlive => CurrentHealth > 0;
}
