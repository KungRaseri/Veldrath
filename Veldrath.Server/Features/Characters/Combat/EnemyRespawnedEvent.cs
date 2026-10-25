namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Describes an enemy that the respawn sweeper brought back, so the hub can broadcast <c>EnemySpawned</c> for it.
/// </summary>
/// <param name="Id">Enemy instance id.</param>
/// <param name="ZoneGroup">SignalR zone group the enemy belongs to.</param>
/// <param name="LocationSlug">Zone-location slug the enemy stands at.</param>
/// <param name="Name">Display name shown to clients.</param>
/// <param name="Level">Combat level.</param>
/// <param name="CurrentHealth">Health after the respawn (full).</param>
/// <param name="MaxHealth">Maximum health.</param>
public sealed record EnemyRespawnedEvent(
    Guid Id,
    string ZoneGroup,
    string LocationSlug,
    string Name,
    int Level,
    int CurrentHealth,
    int MaxHealth);
