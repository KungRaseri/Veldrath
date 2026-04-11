using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Hubs;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that performs a basic attack against the character's currently engaged enemy.
/// Applies player damage to the enemy, records contribution for proportional reward distribution,
/// and triggers a counter-attack if the enemy survives.
/// </summary>
/// <param name="CharacterId">The character performing the attack.</param>
public record AttackEnemyHubCommand(Guid CharacterId)
    : IRequest<AttackEnemyHubResult>;

/// <summary>Result returned by <see cref="AttackEnemyHubCommandHandler"/>.</summary>
public record AttackEnemyHubResult
{
    /// <summary>Gets a value indicating whether the action succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the damage dealt by the player this turn.</summary>
    public int PlayerDamage { get; init; }

    /// <summary>Gets the remaining HP of the enemy after the player's attack.</summary>
    public int EnemyRemainingHealth { get; init; }

    /// <summary>Gets whether the enemy was defeated this turn.</summary>
    public bool EnemyDefeated { get; init; }

    /// <summary>Gets the damage dealt by the enemy counter-attack (0 if enemy was defeated).</summary>
    public int EnemyDamage { get; init; }

    /// <summary>Gets the ability name the enemy used for its counter-attack, or <see langword="null"/> for a basic attack.</summary>
    public string? EnemyAbilityUsed { get; init; }

    /// <summary>Gets the remaining HP of the player after the counter-attack.</summary>
    public int PlayerRemainingHealth { get; init; }

    /// <summary>Gets whether the player was defeated by the counter-attack this turn.</summary>
    public bool PlayerDefeated { get; init; }

    /// <summary>Gets whether the player's character was permanently deleted (hardcore mode death).</summary>
    public bool PlayerHardcoreDeath { get; init; }

    /// <summary>Gets the XP awarded to this player when the enemy was defeated.</summary>
    public int XpEarned { get; init; }

    /// <summary>Gets the gold awarded to this player when the enemy was defeated.</summary>
    public int GoldEarned { get; init; }
}

/// <summary>
/// Handles <see cref="AttackEnemyHubCommand"/> by loading the player and enemy state,
/// computing and applying damage in both directions, handling death outcomes,
/// and persisting updated character state.
/// </summary>
public class AttackEnemyHubCommandHandler
    : IRequestHandler<AttackEnemyHubCommand, AttackEnemyHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<AttackEnemyHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="AttackEnemyHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="scopeFactory">Factory for creating scopes used in fire-and-forget respawn tasks.</param>
    /// <param name="hubContext">Hub context used to broadcast respawn notifications.</param>
    /// <param name="logger">Logger instance.</param>
    public AttackEnemyHubCommandHandler(
        ICharacterRepository characterRepo,
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub> hubContext,
        ILogger<AttackEnemyHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _scopeFactory  = scopeFactory;
        _hubContext    = hubContext;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the combat turn outcome.</summary>
    /// <param name="request">The command containing the acting character's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AttackEnemyHubResult"/> describing the turn outcome.</returns>
    public async Task<AttackEnemyHubResult> Handle(
        AttackEnemyHubCommand request,
        CancellationToken cancellationToken)
    {
        if (!CombatSessionStore.TryGet(request.CharacterId, out var session))
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Not in combat" };

        var storeKey = ZoneLocationEnemyStore.MakeKey(session.ZoneGroup, session.LocationSlug);
        var enemy = ZoneLocationEnemyStore.TryGetEnemy(storeKey, session.EnemyId);
        if (enemy is null)
        {
            CombatSessionStore.Remove(request.CharacterId);
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Enemy no longer exists" };
        }

        if (!enemy.IsAlive)
        {
            CombatSessionStore.Remove(request.CharacterId);
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Enemy is already dead" };
        }

        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Character not found" };

        var attrs  = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
        var player = CombatCharacterHydrator.Hydrate(entity, attrs);

        // --- Player attacks enemy ---
        int playerDamage = CombatHelpers.CalculatePlayerDamage(attrs, entity.Level);
        int enemyRemainingHealth;

        lock (enemy.SyncRoot)
        {
            enemy.CurrentHealth = Math.Max(0, enemy.CurrentHealth - playerDamage);
            enemy.LastAttackerId = request.CharacterId;
            enemy.Participants.Add(request.CharacterId);
            enemy.DamageContributions[request.CharacterId] =
                enemy.DamageContributions.GetValueOrDefault(request.CharacterId) + playerDamage;
            enemyRemainingHealth = enemy.CurrentHealth;
        }

        // --- Enemy defeated ---
        if (!enemy.IsAlive)
        {
            var rewardList = await CombatHelpers.DistributeRewardsAsync(enemy, storeKey, _characterRepo, cancellationToken);
            var myReward   = rewardList.FirstOrDefault(r => r.CharacterId == request.CharacterId);

            CombatHelpers.ScheduleRespawn(
                storeKey, session.ZoneGroup, enemy.ArchetypeSlug,
                _scopeFactory, _hubContext, _logger);

            _logger.LogInformation(
                "Enemy {EnemyName} defeated by {CharacterIdPrefix}",
                enemy.Name, request.CharacterId.ToString()[..8]);

            CombatHelpers.DecrementCooldowns(attrs);
            entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
            await _characterRepo.UpdateAsync(entity, cancellationToken);

            return new AttackEnemyHubResult
            {
                Success              = true,
                PlayerDamage         = playerDamage,
                EnemyRemainingHealth = 0,
                EnemyDefeated        = true,
                PlayerRemainingHealth = player.Health,
                XpEarned             = myReward.Xp,
                GoldEarned           = myReward.Gold,
            };
        }

        // --- Enemy counter-attacks ---
        session = session.NextTurn();
        CombatSessionStore.Set(request.CharacterId, session);

        var (abilityDamage, abilityName) = CombatHelpers.RollEnemyAbility(enemy, player);
        int enemyDamage = abilityDamage > 0
            ? abilityDamage
            : CombatHelpers.CalculateEnemyDamage(enemy, attrs, session.IsPlayerDefending);

        player.Health = Math.Max(0, player.Health - enemyDamage);
        attrs["CurrentHealth"] = player.Health;

        CombatHelpers.DecrementCooldowns(attrs);
        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        var (isDead, isHardcore) = await CombatHelpers.HandleDeathIfNeededAsync(
            player, entity, attrs, session, enemy, _characterRepo, cancellationToken);

        return new AttackEnemyHubResult
        {
            Success               = true,
            PlayerDamage          = playerDamage,
            EnemyRemainingHealth  = enemyRemainingHealth,
            EnemyDefeated         = false,
            EnemyDamage           = enemyDamage,
            EnemyAbilityUsed      = abilityName,
            PlayerRemainingHealth = player.Health,
            PlayerDefeated        = isDead,
            PlayerHardcoreDeath   = isHardcore,
        };
    }
}
