namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Records a player's active combat engagement against a specific zone-level enemy instance.
/// Enemy HP is stored in <see cref="ZoneLocationEnemyStore"/>; this record tracks only
/// per-player state for the current encounter.
/// </summary>
/// <param name="ZoneGroup">The SignalR zone group name where combat is taking place (e.g. <c>"zone:dark-forest_hardcore"</c>).</param>
/// <param name="LocationSlug">The zone location slug where the engaged enemy resides.</param>
/// <param name="EnemyId">The unique instance ID of the <see cref="SpawnedEnemy"/> being fought.</param>
/// <param name="IsPlayerDefending">Whether the player chose the Defend action this round (cleared after each turn).</param>
/// <param name="TurnCount">Number of rounds that have elapsed in this encounter.</param>
/// <param name="StartedAt">UTC timestamp when this combat session began.</param>
public record ActiveCombatSession(
    string ZoneGroup,
    string LocationSlug,
    Guid EnemyId,
    bool IsPlayerDefending,
    int TurnCount,
    DateTimeOffset StartedAt)
{
    /// <summary>Returns a copy with <see cref="IsPlayerDefending"/> set to <paramref name="defending"/>.</summary>
    public ActiveCombatSession WithDefending(bool defending) =>
        this with { IsPlayerDefending = defending };

    /// <summary>Returns a copy with <see cref="TurnCount"/> incremented by one and <see cref="IsPlayerDefending"/> cleared.</summary>
    public ActiveCombatSession NextTurn() =>
        this with { TurnCount = TurnCount + 1, IsPlayerDefending = false };
}
