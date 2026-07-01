namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the player engages an enemy and combat begins.
/// Contains the enemy's current stats and ability list for UI rendering.
/// </summary>
/// <param name="CharacterId">The engaging character's identifier.</param>
/// <param name="EnemyId">The engaged enemy's identifier.</param>
/// <param name="EnemyName">The enemy's display name.</param>
/// <param name="EnemyLevel">The enemy's level.</param>
/// <param name="EnemyCurrentHealth">The enemy's current health points.</param>
/// <param name="EnemyMaxHealth">The enemy's maximum health points.</param>
/// <param name="EnemyAbilityNames">List of ability display names the enemy may use.</param>
public sealed record CombatStartedPayload(
    Guid CharacterId,
    Guid EnemyId,
    string EnemyName,
    int EnemyLevel,
    int EnemyCurrentHealth,
    int EnemyMaxHealth,
    List<string> EnemyAbilityNames);

/// <summary>
/// Hub event payload received after each combat turn resolves.
/// Contains damage dealt, health changes, and status flags.
/// </summary>
/// <param name="Action">The action taken (e.g. "attack", "defend", "ability").</param>
/// <param name="PlayerDamage">Damage dealt by the player this turn.</param>
/// <param name="EnemyRemainingHealth">The enemy's health after receiving damage.</param>
/// <param name="EnemyDefeated">Whether the enemy was defeated by this turn's action.</param>
/// <param name="EnemyDamage">Damage dealt by the enemy this turn.</param>
/// <param name="EnemyAbilityUsed">The ability name used by the enemy, or <c>null</c>.</param>
/// <param name="PlayerRemainingHealth">The player's health after receiving damage.</param>
/// <param name="PlayerDefeated">Whether the player was defeated by this turn's action.</param>
/// <param name="PlayerHardcoreDeath">Whether this death is permanent (hardcore mode).</param>
/// <param name="XpEarned">Experience points earned from this turn.</param>
/// <param name="GoldEarned">Gold earned from this turn.</param>
/// <param name="AbilityId">The ability slug used, or <c>null</c>.</param>
/// <param name="AbilityDamage">Damage dealt by the ability, if applicable.</param>
/// <param name="HealthRestored">Health restored by the ability, if applicable.</param>
/// <param name="ManaCost">Mana consumed by the ability, if applicable.</param>
/// <param name="PlayerRemainingMana">The player's mana after the action.</param>
public sealed record CombatTurnPayload(
    string Action,
    int PlayerDamage,
    int EnemyRemainingHealth,
    bool EnemyDefeated,
    int EnemyDamage,
    string? EnemyAbilityUsed,
    int PlayerRemainingHealth,
    bool PlayerDefeated,
    bool PlayerHardcoreDeath,
    int XpEarned = 0,
    int GoldEarned = 0,
    string? AbilityId = null,
    int AbilityDamage = 0,
    int HealthRestored = 0,
    int ManaCost = 0,
    int PlayerRemainingMana = 0);

/// <summary>
/// Hub event payload received when combat ends (fled, won, or death).
/// </summary>
/// <param name="CharacterId">The character involved in the combat.</param>
/// <param name="Reason">Human-readable reason for combat ending (e.g. "Victory", "Fled", "Defeated").</param>
public sealed record CombatEndedPayload(Guid CharacterId, string Reason);
