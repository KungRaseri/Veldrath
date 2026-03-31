using MediatR;
using Microsoft.Extensions.Logging;
using RealmUnbound.Server.Data.Repositories;

namespace RealmUnbound.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that sets the character to a defending stance for the current combat turn.
/// Reduces incoming damage and triggers a counter-attack at reduced damage.
/// </summary>
/// <param name="CharacterId">The character taking the defend action.</param>
public record DefendActionHubCommand(Guid CharacterId)
    : IRequest<DefendActionHubResult>;

/// <summary>Result returned by <see cref="DefendActionHubCommandHandler"/>.</summary>
public record DefendActionHubResult
{
    /// <summary>Gets a value indicating whether the action succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the damage dealt by the enemy's counter-attack this turn.</summary>
    public int EnemyDamage { get; init; }

    /// <summary>Gets the ability name the enemy used for its counter-attack, or <see langword="null"/> for a basic attack.</summary>
    public string? EnemyAbilityUsed { get; init; }

    /// <summary>Gets the remaining HP of the player after the counter-attack.</summary>
    public int PlayerRemainingHealth { get; init; }

    /// <summary>Gets whether the player was defeated despite defending.</summary>
    public bool PlayerDefeated { get; init; }

    /// <summary>Gets whether the player's character was permanently deleted (hardcore mode death).</summary>
    public bool PlayerHardcoreDeath { get; init; }
}

/// <summary>
/// Handles <see cref="DefendActionHubCommand"/> by applying a defending flag to the
/// active session, allowing the enemy counter-attack to use the boosted defense formula.
/// </summary>
public class DefendActionHubCommandHandler
    : IRequestHandler<DefendActionHubCommand, DefendActionHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<DefendActionHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="DefendActionHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="logger">Logger instance.</param>
    public DefendActionHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<DefendActionHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the defend-turn outcome.</summary>
    /// <param name="request">The command containing the acting character's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DefendActionHubResult"/> describing the turn outcome.</returns>
    public async Task<DefendActionHubResult> Handle(
        DefendActionHubCommand request,
        CancellationToken cancellationToken)
    {
        if (!CombatSessionStore.TryGet(request.CharacterId, out var session))
            return new DefendActionHubResult { Success = false, ErrorMessage = "Not in combat" };

        var storeKey = ZoneLocationEnemyStore.MakeKey(session.ZoneGroup, session.LocationSlug);
        var enemy = ZoneLocationEnemyStore.TryGetEnemy(storeKey, session.EnemyId);
        if (enemy is null)
        {
            CombatSessionStore.Remove(request.CharacterId);
            return new DefendActionHubResult { Success = false, ErrorMessage = "Enemy no longer exists" };
        }

        if (!enemy.IsAlive)
        {
            CombatSessionStore.Remove(request.CharacterId);
            return new DefendActionHubResult { Success = false, ErrorMessage = "Enemy is already dead" };
        }

        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new DefendActionHubResult { Success = false, ErrorMessage = "Character not found" };

        var attrs  = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
        var player = CombatCharacterHydrator.Hydrate(entity, attrs);

        session = session.WithDefending(true).NextTurn();
        CombatSessionStore.Set(request.CharacterId, session);

        var (abilityDamage, abilityName) = CombatHelpers.RollEnemyAbility(enemy, player);
        int enemyDamage = abilityDamage > 0
            ? Math.Max(1, abilityDamage / 2) // halve ability damage when defending
            : CombatHelpers.CalculateEnemyDamage(enemy, attrs, isDefending: true);

        player.Health = Math.Max(0, player.Health - enemyDamage);
        attrs["CurrentHealth"] = player.Health;

        CombatHelpers.DecrementCooldowns(attrs);
        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        var (isDead, isHardcore) = await CombatHelpers.HandleDeathIfNeededAsync(
            player, entity, attrs, session, enemy, _characterRepo, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} defended — enemy dealt {Damage} damage",
            request.CharacterId.ToString()[..8], enemyDamage);

        return new DefendActionHubResult
        {
            Success               = true,
            EnemyDamage           = enemyDamage,
            EnemyAbilityUsed      = abilityName,
            PlayerRemainingHealth = player.Health,
            PlayerDefeated        = isDead,
            PlayerHardcoreDeath   = isHardcore,
        };
    }
}
