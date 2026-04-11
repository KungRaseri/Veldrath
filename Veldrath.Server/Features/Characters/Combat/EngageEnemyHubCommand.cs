using MediatR;
using Microsoft.Extensions.Logging;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that initiates combat between a character and a live enemy at their current location.
/// </summary>
/// <param name="CharacterId">The character beginning combat.</param>
/// <param name="ZoneGroup">The SignalR group name for the zone (used to key the enemy store).</param>
/// <param name="LocationSlug">The location slug where the enemy resides.</param>
/// <param name="EnemyId">The unique instance ID of the enemy to engage.</param>
public record EngageEnemyHubCommand(Guid CharacterId, string ZoneGroup, string LocationSlug, Guid EnemyId)
    : IRequest<EngageEnemyHubResult>;

/// <summary>Result returned by <see cref="EngageEnemyHubCommandHandler"/>.</summary>
public record EngageEnemyHubResult
{
    /// <summary>Gets a value indicating whether the engagement succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the unique instance ID of the engaged enemy.</summary>
    public Guid EnemyId { get; init; }

    /// <summary>Gets the display name of the engaged enemy.</summary>
    public string EnemyName { get; init; } = string.Empty;

    /// <summary>Gets the combat level of the engaged enemy.</summary>
    public int EnemyLevel { get; init; }

    /// <summary>Gets the current HP of the engaged enemy at the moment of engagement.</summary>
    public int EnemyCurrentHealth { get; init; }

    /// <summary>Gets the maximum HP of the engaged enemy.</summary>
    public int EnemyMaxHealth { get; init; }

    /// <summary>Gets the display names of abilities the engaged enemy can use.</summary>
    public IReadOnlyList<string> EnemyAbilityNames { get; init; } = [];
}

/// <summary>
/// Handles <see cref="EngageEnemyHubCommand"/> by locating the live enemy in
/// <see cref="ZoneLocationEnemyStore"/>, registering the player as a participant,
/// and recording an <see cref="ActiveCombatSession"/> in <see cref="CombatSessionStore"/>.
/// </summary>
public class EngageEnemyHubCommandHandler
    : IRequestHandler<EngageEnemyHubCommand, EngageEnemyHubResult>
{
    private readonly ILogger<EngageEnemyHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="EngageEnemyHubCommandHandler"/>.</summary>
    /// <param name="logger">Logger instance.</param>
    public EngageEnemyHubCommandHandler(ILogger<EngageEnemyHubCommandHandler> logger)
    {
        _logger = logger;
    }

    /// <summary>Handles the command and returns the engagement outcome.</summary>
    /// <param name="request">The command containing character, zone, location, and enemy identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EngageEnemyHubResult"/> describing the outcome.</returns>
    public Task<EngageEnemyHubResult> Handle(
        EngageEnemyHubCommand request,
        CancellationToken cancellationToken)
    {
        if (CombatSessionStore.IsInCombat(request.CharacterId))
            return Task.FromResult(new EngageEnemyHubResult { Success = false, ErrorMessage = "Already in combat" });

        var storeKey = ZoneLocationEnemyStore.MakeKey(request.ZoneGroup, request.LocationSlug);
        var enemy = ZoneLocationEnemyStore.TryGetEnemy(storeKey, request.EnemyId);
        if (enemy is null)
            return Task.FromResult(new EngageEnemyHubResult { Success = false, ErrorMessage = "Enemy not found" });

        if (!enemy.IsAlive)
            return Task.FromResult(new EngageEnemyHubResult { Success = false, ErrorMessage = "Enemy is already dead" });

        // Register this player as a participant.
        lock (enemy.SyncRoot)
        {
            enemy.Participants.Add(request.CharacterId);
        }

        var session = new ActiveCombatSession(
            ZoneGroup:       request.ZoneGroup,
            LocationSlug:    request.LocationSlug,
            EnemyId:         enemy.Id,
            IsPlayerDefending: false,
            TurnCount:       0,
            StartedAt:       DateTimeOffset.UtcNow);

        CombatSessionStore.Set(request.CharacterId, session);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} engaged enemy {EnemyName} ({EnemyId}) at {StoreKey}",
            request.CharacterId.ToString()[..8], enemy.Name, enemy.Id, storeKey);

        var abilityNames = enemy.Template.Abilities
            .Select(a => string.IsNullOrWhiteSpace(a.DisplayName) ? a.Name : a.DisplayName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return Task.FromResult(new EngageEnemyHubResult
        {
            Success            = true,
            EnemyId            = enemy.Id,
            EnemyName          = enemy.Name,
            EnemyLevel         = enemy.Level,
            EnemyCurrentHealth = enemy.CurrentHealth,
            EnemyMaxHealth     = enemy.MaxHealth,
            EnemyAbilityNames  = abilityNames,
        });
    }
}
