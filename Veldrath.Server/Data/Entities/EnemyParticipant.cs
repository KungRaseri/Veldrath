namespace Veldrath.Server.Data.Entities;

/// <summary>
/// Records that a character took part in a fight against a specific enemy instance, and how much damage they
/// dealt. The damage total is what rewards are split by when the enemy dies.
/// </summary>
public class EnemyParticipant
{
    /// <summary>Gets or sets the enemy instance being fought.</summary>
    public Guid EnemyInstanceId { get; set; }

    /// <summary>Gets or sets the character taking part in the fight.</summary>
    public Guid CharacterId { get; set; }

    /// <summary>Gets or sets the cumulative damage this character has dealt.</summary>
    public int Damage { get; set; }

    /// <summary>Gets or sets when this character last attacked (UTC).</summary>
    public DateTimeOffset LastAttackedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets the enemy being fought.</summary>
    public EnemyInstance? Enemy { get; set; }

    /// <summary>Gets or sets the participating character.</summary>
    public Character? Character { get; set; }
}
