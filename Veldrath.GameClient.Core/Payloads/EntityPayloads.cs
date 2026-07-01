namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when an enemy is defeated.
/// Contains the character identifier for context.
/// </summary>
/// <param name="CharacterId">The character that defeated the enemy.</param>
public sealed record EnemyDefeatedPayload(Guid CharacterId);
