namespace RealmEngine.Data.Entities;

/// <summary>Junction: abilities an enemy can use during combat.</summary>
public class EnemyAbilityPool
{
    /// <summary>FK to the enemy that has this ability in its pool.</summary>
    public Guid EnemyId { get; set; }
    /// <summary>FK to the ability in the pool.</summary>
    public Guid AbilityId { get; set; }

    /// <summary>Probability the enemy selects this ability per combat turn (0.0–1.0).</summary>
    public float UseChance { get; set; } = 1.0f;

    /// <summary>Navigation property for the owning enemy.</summary>
    public Enemy? Enemy { get; set; }
    /// <summary>Navigation property for the pooled ability.</summary>
    public Ability? Ability { get; set; }
}
