namespace Veldrath.GameClient.Core.Payloads;

/// <summary>
/// Hub event payload received when an enemy is defeated.
/// Contains the character identifier for context.
/// </summary>
/// <param name="CharacterId">The character that defeated the enemy.</param>
public sealed record EnemyDefeatedPayload(Guid CharacterId);

/// <summary>
/// Hub event payload received when another character in the zone engages an enemy.
/// Matches the anonymous payload sent by <c>GameHub.EngageEnemy</c>.
/// </summary>
/// <param name="CharacterId">Character that engaged the enemy.</param>
/// <param name="EnemyId">Identifier of the engaged enemy.</param>
/// <param name="EnemyName">Display name of the engaged enemy.</param>
public sealed record EnemyEngagedPayload(Guid CharacterId, Guid EnemyId, string EnemyName);

/// <summary>
/// Hub event payload received when an enemy spawns or respawns in the zone.
/// Matches the anonymous payload sent by the server combat helpers.
/// </summary>
/// <param name="Id">Identifier of the spawned enemy.</param>
/// <param name="Name">Display name of the spawned enemy.</param>
/// <param name="Level">Enemy level.</param>
/// <param name="CurrentHealth">Starting current health.</param>
/// <param name="MaxHealth">Maximum health.</param>
public sealed record EnemySpawnedPayload(
    Guid Id,
    string Name,
    int Level,
    int CurrentHealth,
    int MaxHealth);
