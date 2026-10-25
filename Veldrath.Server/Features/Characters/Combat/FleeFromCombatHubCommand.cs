using MediatR;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that attempts to flee from active combat.
/// There is a 50% success chance; on failure the enemy counter-attacks.
/// </summary>
/// <param name="CharacterId">The character attempting to flee.</param>
public record FleeFromCombatHubCommand(Guid CharacterId)
    : IRequest<FleeFromCombatHubResult>;

/// <summary>Result returned by <see cref="FleeFromCombatHubCommandHandler"/>.</summary>
public record FleeFromCombatHubResult
{
    /// <summary>Gets a value indicating whether the action was processed (not the flee success).</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets a value indicating that the character successfully fled combat.</summary>
    public bool Fled { get; init; }

    /// <summary>Gets the damage dealt by the enemy if the flee attempt failed.</summary>
    public int EnemyDamage { get; init; }

    /// <summary>Gets the ability name the enemy used when the flee failed, or <see langword="null"/> for a basic attack.</summary>
    public string? EnemyAbilityUsed { get; init; }

    /// <summary>Gets the remaining HP of the player after a failed flee attempt.</summary>
    public int PlayerRemainingHealth { get; init; }

    /// <summary>Gets whether the player was defeated after a failed flee attempt.</summary>
    public bool PlayerDefeated { get; init; }

    /// <summary>Gets whether the player's character was permanently deleted (hardcore mode death).</summary>
    public bool PlayerHardcoreDeath { get; init; }

    /// <summary>Gets the gold the death cost, or 0 when the player survived or fled.</summary>
    public int GoldLost { get; init; }

    /// <summary>Gets the experience the death cost, or 0 when the player survived or fled.</summary>
    public int XpLost { get; init; }
}

/// <summary>
/// Handles <see cref="FleeFromCombatHubCommand"/> by rolling a flee chance, ending combat on success,
/// or triggering an enemy counter-attack on failure.
/// </summary>
public class FleeFromCombatHubCommandHandler
    : IRequestHandler<FleeFromCombatHubCommand, FleeFromCombatHubResult>
{
    private const int FleeSuccessChance = 50; // percent

    private readonly ICharacterRepository _characterRepo;
    private readonly IEnemyInstanceStore _enemies;
    private readonly ICombatSessionStore _sessions;
    private readonly IOptions<DeathPenaltyOptions> _penalty;
    private readonly ILogger<FleeFromCombatHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="FleeFromCombatHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="enemies">Store for the live enemy instances.</param>
    /// <param name="sessions">Store for per-character combat sessions.</param>
    /// <param name="penalty">Configured death-penalty values.</param>
    /// <param name="logger">Logger instance.</param>
    public FleeFromCombatHubCommandHandler(
        ICharacterRepository characterRepo,
        IEnemyInstanceStore enemies,
        ICombatSessionStore sessions,
        IOptions<DeathPenaltyOptions> penalty,
        ILogger<FleeFromCombatHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _enemies = enemies;
        _sessions = sessions;
        _penalty = penalty;
        _logger = logger;
    }

    /// <summary>Handles the command and returns the flee attempt outcome.</summary>
    /// <param name="request">The command containing the fleeing character's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="FleeFromCombatHubResult"/> describing the outcome.</returns>
    public async Task<FleeFromCombatHubResult> Handle(
        FleeFromCombatHubCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.TryGetAsync(request.CharacterId, cancellationToken);
        if (session is null)
            return new FleeFromCombatHubResult { Success = false, ErrorMessage = "Not in combat" };

        var enemy = await _enemies.TryGetAsync(session.EnemyId, cancellationToken);

        // Flee succeeds: stop being a target and end the session. The damage already dealt stays recorded — it was
        // earned, so a fleeing member still gets that share if the rest of the party finishes the kill.
        if (Random.Shared.Next(100) < FleeSuccessChance)
        {
            if (enemy is not null)
            {
                lock (enemy.SyncRoot)
                {
                    enemy.Participants.Remove(request.CharacterId);
                    if (enemy.LastAttackerId == request.CharacterId)
                        enemy.LastAttackerId = enemy.Participants.FirstOrDefault();
                }

                await _enemies.SaveAsync(enemy, session.ZoneGroup, session.LocationSlug, cancellationToken);
            }

            await _sessions.RemoveAsync(request.CharacterId, cancellationToken);

            _logger.LogInformation(
                "Character {CharacterIdPrefix} fled combat successfully",
                request.CharacterId.ToString()[..8]);

            return new FleeFromCombatHubResult { Success = true, Fled = true };
        }

        // Flee failed — enemy gets a free attack.
        if (enemy is null || !enemy.IsAlive)
        {
            await _sessions.RemoveAsync(request.CharacterId, cancellationToken);
            return new FleeFromCombatHubResult { Success = true, Fled = true };
        }

        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new FleeFromCombatHubResult { Success = false, ErrorMessage = "Character not found" };

        var attrs = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
        var player = CombatCharacterHydrator.Hydrate(entity, attrs);

        session = session.NextTurn();
        await _sessions.SetAsync(request.CharacterId, session, cancellationToken);

        var (abilityDamage, abilityName) = CombatHelpers.RollEnemyAbility(enemy, player);
        int enemyDamage = abilityDamage > 0
            ? abilityDamage
            : CombatHelpers.CalculateEnemyDamage(enemy, attrs, isDefending: false);

        player.Health = Math.Max(0, player.Health - enemyDamage);
        attrs["CurrentHealth"] = player.Health;

        CombatHelpers.DecrementCooldowns(attrs);
        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        var death = await CombatHelpers.HandleDeathIfNeededAsync(
            player, entity, attrs, session, enemy, _characterRepo, _enemies, _sessions,
            _penalty.Value, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} flee failed — enemy dealt {Damage} damage",
            request.CharacterId.ToString()[..8], enemyDamage);

        return new FleeFromCombatHubResult
        {
            Success = true,
            Fled = false,
            EnemyDamage = enemyDamage,
            EnemyAbilityUsed = abilityName,
            PlayerRemainingHealth = player.Health,
            PlayerDefeated = death.IsDead,
            PlayerHardcoreDeath = death.IsHardcore,
            GoldLost = death.GoldLost,
            XpLost = death.XpLost,
        };
    }
}
