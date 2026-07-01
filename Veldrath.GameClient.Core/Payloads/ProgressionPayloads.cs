namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when the character gains experience points.
/// </summary>
/// <param name="NewLevel">The character's new level after the XP gain.</param>
/// <param name="NewXP">The character's total accumulated XP.</param>
/// <param name="LeveledUp">Whether the character gained a level from this XP gain.</param>
/// <param name="LeveledUpTo">The new level reached, or 0 if no level-up occurred.</param>
public sealed record ExperienceGainedPayload(int NewLevel, int NewXP, bool LeveledUp, int LeveledUpTo);

/// <summary>
/// Hub event payload received when the character's gold balance changes.
/// </summary>
/// <param name="GoldAdded">The amount of gold added (positive) or spent (negative).</param>
/// <param name="NewGoldTotal">The character's total gold after the transaction.</param>
public sealed record GoldChangedPayload(int GoldAdded, int NewGoldTotal);

/// <summary>
/// Hub event payload received when the character takes damage.
/// </summary>
/// <param name="Damage">The amount of damage taken.</param>
/// <param name="CurrentHP">The character's health after taking damage.</param>
/// <param name="MaxHP">The character's maximum health.</param>
/// <param name="IsDead">Whether the character died from this damage.</param>
public sealed record DamageTakenPayload(int Damage, int CurrentHP, int MaxHP, bool IsDead);

/// <summary>
/// Hub event payload received after attribute points are allocated.
/// </summary>
/// <param name="RemainingPoints">The number of unspent attribute points remaining.</param>
/// <param name="Attrs">The current attribute values keyed by attribute name (Strength, Dexterity, etc.).</param>
public sealed record AttributePointsAllocatedPayload(int RemainingPoints, Dictionary<string, int> Attrs);

/// <summary>
/// Hub event payload received when the character rests and recovers health and mana.
/// </summary>
/// <param name="HP">The character's health after resting.</param>
/// <param name="MaxHP">The character's maximum health.</param>
/// <param name="MP">The character's mana after resting.</param>
/// <param name="MaxMP">The character's maximum mana.</param>
/// <param name="Gold">The character's gold after resting (may be deducted for inn costs).</param>
public sealed record CharacterRestedPayload(int HP, int MaxHP, int MP, int MaxMP, int Gold);

/// <summary>
/// Hub event payload received when the character uses an ability.
/// </summary>
/// <param name="AbilityId">The ability identifier slug.</param>
/// <param name="RemainingMana">The character's mana after using the ability.</param>
/// <param name="HPRestored">The amount of health restored by the ability, if any.</param>
public sealed record AbilityUsedPayload(string AbilityId, int RemainingMana, int HPRestored);

/// <summary>
/// Hub event payload received when the character gains skill experience.
/// </summary>
/// <param name="SkillId">The skill identifier slug.</param>
/// <param name="XpGained">The amount of skill XP gained.</param>
/// <param name="NewRank">The skill's new rank after the XP gain.</param>
/// <param name="RankedUp">Whether the skill gained a rank from this XP gain.</param>
public sealed record SkillXpGainedPayload(string SkillId, int XpGained, int NewRank, bool RankedUp);
