using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Features.Party;

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

    /// <summary>Gets the party members who joined this fight because they were standing at the same location.</summary>
    public IReadOnlyList<Guid> PartyMemberIds { get; init; } = [];
}

/// <summary>
/// Handles <see cref="EngageEnemyHubCommand"/> by loading the live enemy from
/// <see cref="IEnemyInstanceStore"/>, registering the player as a participant,
/// and recording an <see cref="ActiveCombatSession"/> in <see cref="ICombatSessionStore"/>.
/// </summary>
public class EngageEnemyHubCommandHandler
    : IRequestHandler<EngageEnemyHubCommand, EngageEnemyHubResult>
{
    private readonly IEnemyInstanceStore _enemies;
    private readonly ICombatSessionStore _sessions;
    private readonly IPartyCombatQuery _party;
    private readonly ILogger<EngageEnemyHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="EngageEnemyHubCommandHandler"/>.</summary>
    /// <param name="enemies">Store for the live enemy instances.</param>
    /// <param name="sessions">Store for per-character combat sessions.</param>
    /// <param name="party">Reads which party members are standing at the same location.</param>
    /// <param name="logger">Logger instance.</param>
    public EngageEnemyHubCommandHandler(
        IEnemyInstanceStore enemies,
        ICombatSessionStore sessions,
        IPartyCombatQuery party,
        ILogger<EngageEnemyHubCommandHandler> logger)
    {
        _enemies = enemies;
        _sessions = sessions;
        _party = party;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the engagement outcome.</summary>
    /// <param name="request">The command containing character, zone, location, and enemy identifiers.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="EngageEnemyHubResult"/> describing the outcome.</returns>
    public async Task<EngageEnemyHubResult> Handle(
        EngageEnemyHubCommand request,
        CancellationToken cancellationToken)
    {
        if (await _sessions.IsInCombatAsync(request.CharacterId, cancellationToken))
            return new EngageEnemyHubResult { Success = false, ErrorMessage = "Already in combat" };

        var enemy = await _enemies.TryGetAsync(request.EnemyId, cancellationToken);
        if (enemy is null)
        {
            // Failsafe: a fight against a row that no longer exists is said out loud, never a silent no-op.
            _logger.LogError(
                "Character {CharacterIdPrefix} tried to engage enemy {EnemyId}, which no longer exists",
                request.CharacterId.ToString()[..8], request.EnemyId);
            return new EngageEnemyHubResult { Success = false, ErrorMessage = "Enemy not found" };
        }

        if (!enemy.IsAlive)
            return new EngageEnemyHubResult { Success = false, ErrorMessage = "Enemy is already dead" };

        // Party members standing at this location join the same fight: the enemy row is shared, so their damage
        // and their kill progress belong in one record. A member who never acts earns no XP or gold — only progress.
        var partyMemberIds = await _party.GetOnlinePartyMembersAtLocationAsync(request.CharacterId, cancellationToken);

        // Register the participants and persist them, so the engagement survives a restart.
        lock (enemy.SyncRoot)
        {
            enemy.Participants.Add(request.CharacterId);
            foreach (var memberId in partyMemberIds)
                enemy.Participants.Add(memberId);
        }

        await _enemies.SaveAsync(enemy, request.ZoneGroup, request.LocationSlug, cancellationToken);

        var session = new ActiveCombatSession(
            ZoneGroup: request.ZoneGroup,
            LocationSlug: request.LocationSlug,
            EnemyId: enemy.Id,
            IsPlayerDefending: false,
            TurnCount: 0,
            StartedAt: DateTimeOffset.UtcNow);

        await _sessions.SetAsync(request.CharacterId, session, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} engaged enemy {EnemyName} ({EnemyId}) at {ZoneGroup}/{LocationSlug}",
            request.CharacterId.ToString()[..8], enemy.Name, enemy.Id, request.ZoneGroup, request.LocationSlug);

        var abilityNames = enemy.Template.Abilities
            .Select(a => string.IsNullOrWhiteSpace(a.DisplayName) ? a.Name : a.DisplayName)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .ToList();

        return new EngageEnemyHubResult
        {
            Success = true,
            EnemyId = enemy.Id,
            EnemyName = enemy.Name,
            EnemyLevel = enemy.Level,
            EnemyCurrentHealth = enemy.CurrentHealth,
            EnemyMaxHealth = enemy.MaxHealth,
            EnemyAbilityNames = abilityNames,
            PartyMemberIds = partyMemberIds,
        };
    }
}
