namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a combat action taken by a combatant.
/// </summary>
public class CombatAction
{
    /// <summary>Gets or sets the type of action.</summary>
    public CombatActionType Type { get; set; }

    /// <summary>Gets or sets the actor's name.</summary>
    public string ActorName { get; set; } = string.Empty;

    /// <summary>Gets or sets the target's name.</summary>
    public string? TargetName { get; set; }

    /// <summary>Gets or sets the item used.</summary>
    public Item? ItemUsed { get; set; }

    /// <summary>Gets or sets the ability ID.</summary>
    public string? AbilityId { get; set; }

    /// <summary>Gets or sets the spell ID.</summary>
    public string? SpellId { get; set; }
}

/// <summary>
/// Types of actions available in combat.
/// </summary>
public enum CombatActionType
{
    /// <summary>Physical or magic attack.</summary>
    Attack,
    /// <summary>Reduce incoming damage.</summary>
    Defend,
    /// <summary>Use consumable item.</summary>
    UseItem,
    /// <summary>Use a learned ability.</summary>
    UseAbility,
    /// <summary>Cast a learned spell.</summary>
    CastSpell,
    /// <summary>Attempt to escape combat.</summary>
    Flee
}

/// <summary>
/// Result of a combat action.
/// </summary>
public class CombatResult
{
    /// <summary>Gets or sets a value indicating whether the action succeeded.</summary>
    public bool Success { get; set; }

    /// <summary>Gets or sets the result message.</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the damage dealt.</summary>
    public int Damage { get; set; } = 0;

    /// <summary>Gets or sets the healing amount.</summary>
    public int Healing { get; set; } = 0;

    /// <summary>Gets or sets a value indicating whether this was a critical hit.</summary>
    public bool IsCritical { get; set; } = false;

    /// <summary>Gets or sets a value indicating whether the attack was dodged.</summary>
    public bool IsDodged { get; set; } = false;

    /// <summary>Gets or sets a value indicating whether the attack was blocked.</summary>
    public bool IsBlocked { get; set; } = false;

    /// <summary>Gets or sets the combat effect applied.</summary>
    public CombatEffect Effect { get; set; } = CombatEffect.None;

    /// <summary>Gets or sets the status effects applied by this action.</summary>
    public List<StatusEffect> StatusEffectsApplied { get; set; } = new();

    /// <summary>Gets or sets the status effects that expired this turn.</summary>
    public List<StatusEffectType> StatusEffectsExpired { get; set; } = new();

    /// <summary>Gets or sets the active status effects on the target after this action.</summary>
    public List<StatusEffect> ActiveStatusEffects { get; set; } = new();

    /// <summary>Gets or sets the damage dealt by DoT effects this turn.</summary>
    public int DotDamage { get; set; } = 0;

    /// <summary>Gets or sets the healing from HoT effects this turn.</summary>
    public int HotHealing { get; set; } = 0;
}

/// <summary>
/// Special combat effects.
/// </summary>
public enum CombatEffect
{
    /// <summary>No effect.</summary>
    None,
    /// <summary>Target is stunned.</summary>
    Stunned,
    /// <summary>Target is poisoned.</summary>
    Poisoned,
    /// <summary>Target is burning.</summary>
    Burning,
    /// <summary>Target is frozen.</summary>
    Frozen,
    /// <summary>Target is bleeding.</summary>
    Bleeding
}

/// <summary>
/// Outcome of a combat encounter.
/// </summary>
public class CombatOutcome
{
    /// <summary>Gets or sets a value indicating whether the player won.</summary>
    public bool PlayerVictory { get; set; }

    /// <summary>Gets or sets the XP gained.</summary>
    public int XPGained { get; set; }

    /// <summary>Gets or sets the gold gained.</summary>
    public int GoldGained { get; set; }

    /// <summary>Gets or sets the loot dropped.</summary>
    public List<Item> LootDropped { get; set; } = new();

    /// <summary>Gets or sets the summary message.</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Gets or sets the ID of the defeated enemy.</summary>
    public string? DefeatedEnemyId { get; set; }

    /// <summary>Gets or sets the type of the defeated enemy.</summary>
    public string? DefeatedEnemyType { get; set; }

    /// <summary>Gets or sets the list of quest objectives that were completed in this combat.</summary>
    public List<string> QuestObjectivesCompleted { get; set; } = new();

    /// <summary>Gets or sets the list of quests that were fully completed in this combat.</summary>
    public List<string> QuestsCompleted { get; set; } = new();
}
