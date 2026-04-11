using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Hubs;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>
/// Hub command that activates a named ability during combat, consuming mana and applying
/// its effect (damage or healing) before the enemy counter-attacks.
/// </summary>
/// <param name="CharacterId">The character using the ability.</param>
/// <param name="AbilityId">The identifier of the ability to use.</param>
public record UseAbilityInCombatHubCommand(Guid CharacterId, string AbilityId)
    : IRequest<UseAbilityInCombatHubResult>;

/// <summary>Result returned by <see cref="UseAbilityInCombatHubCommandHandler"/>.</summary>
public record UseAbilityInCombatHubResult
{
    /// <summary>Gets a value indicating whether the action succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the ability ID that was used.</summary>
    public string AbilityId { get; init; } = string.Empty;

    /// <summary>Gets the damage dealt to the enemy by the ability (zero for healing abilities).</summary>
    public int AbilityDamage { get; init; }

    /// <summary>Gets the HP restored to the player by the ability (zero for damage abilities).</summary>
    public int HealthRestored { get; init; }

    /// <summary>Gets the mana cost deducted for using the ability.</summary>
    public int ManaCost { get; init; }

    /// <summary>Gets the player's remaining mana after the ability.</summary>
    public int PlayerRemainingMana { get; init; }

    /// <summary>Gets the remaining HP of the enemy after the ability (if it dealt damage).</summary>
    public int EnemyRemainingHealth { get; init; }

    /// <summary>Gets whether the enemy was defeated by this ability.</summary>
    public bool EnemyDefeated { get; init; }

    /// <summary>Gets the damage dealt by the enemy counter-attack (zero if enemy was defeated).</summary>
    public int EnemyDamage { get; init; }

    /// <summary>Gets the ability name the enemy used for its counter-attack, or <see langword="null"/> for a basic attack.</summary>
    public string? EnemyAbilityUsed { get; init; }

    /// <summary>Gets the remaining HP of the player after the counter-attack.</summary>
    public int PlayerRemainingHealth { get; init; }

    /// <summary>Gets whether the player was defeated by the counter-attack.</summary>
    public bool PlayerDefeated { get; init; }

    /// <summary>Gets whether the player's character was permanently deleted (hardcore mode death).</summary>
    public bool PlayerHardcoreDeath { get; init; }

    /// <summary>Gets the XP awarded to this player when the enemy was defeated.</summary>
    public int XpEarned { get; init; }

    /// <summary>Gets the gold awarded to this player when the enemy was defeated.</summary>
    public int GoldEarned { get; init; }
}

/// <summary>
/// Handles <see cref="UseAbilityInCombatHubCommand"/> by validating mana and cooldown,
/// applying the ability effect, and triggering an enemy counter-attack if the enemy survives.
/// </summary>
public class UseAbilityInCombatHubCommandHandler
    : IRequestHandler<UseAbilityInCombatHubCommand, UseAbilityInCombatHubResult>
{
    private const int FallbackManaCost      = 10;
    private const int FallbackCooldownTurns = 3;
    private const int FallbackHealingAmount = 20;

    private readonly ICharacterRepository _characterRepo;
    private readonly IPowerRepository _powerRepo;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHubContext<GameHub> _hubContext;
    private readonly ILogger<UseAbilityInCombatHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="UseAbilityInCombatHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="powerRepo">Repository for resolving ability costs and effects from the content database.</param>
    /// <param name="scopeFactory">Factory for creating scopes used in fire-and-forget respawn tasks.</param>
    /// <param name="hubContext">Hub context used to broadcast respawn notifications.</param>
    /// <param name="logger">Logger instance.</param>
    public UseAbilityInCombatHubCommandHandler(
        ICharacterRepository characterRepo,
        IPowerRepository powerRepo,
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub> hubContext,
        ILogger<UseAbilityInCombatHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _powerRepo     = powerRepo;
        _scopeFactory  = scopeFactory;
        _hubContext    = hubContext;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the ability-use combat turn outcome.</summary>
    /// <param name="request">The command containing the acting character's ID and ability ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="UseAbilityInCombatHubResult"/> describing the turn outcome.</returns>
    public async Task<UseAbilityInCombatHubResult> Handle(
        UseAbilityInCombatHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.AbilityId))
            return new UseAbilityInCombatHubResult { Success = false, ErrorMessage = "Ability ID cannot be empty" };

        if (!CombatSessionStore.TryGet(request.CharacterId, out var session))
            return new UseAbilityInCombatHubResult { Success = false, ErrorMessage = "Not in combat" };

        var storeKey = ZoneLocationEnemyStore.MakeKey(session.ZoneGroup, session.LocationSlug);
        var enemy = ZoneLocationEnemyStore.TryGetEnemy(storeKey, session.EnemyId);
        if (enemy is null)
        {
            CombatSessionStore.Remove(request.CharacterId);
            return new UseAbilityInCombatHubResult { Success = false, ErrorMessage = "Enemy no longer exists" };
        }

        if (!enemy.IsAlive)
        {
            CombatSessionStore.Remove(request.CharacterId);
            return new UseAbilityInCombatHubResult { Success = false, ErrorMessage = "Enemy is already dead" };
        }

        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new UseAbilityInCombatHubResult { Success = false, ErrorMessage = "Character not found" };

        // Validate that the character has learned this ability.
        var learnedSlugs = System.Text.Json.JsonSerializer.Deserialize<List<string>>(entity.AbilitiesBlob) ?? [];
        if (!learnedSlugs.Contains(request.AbilityId, StringComparer.OrdinalIgnoreCase))
            return new UseAbilityInCombatHubResult { Success = false, ErrorMessage = $"Ability '{request.AbilityId}' is not learned" };

        // Load the power definition from the content DB.
        var power = await _powerRepo.GetBySlugAsync(request.AbilityId);
        int manaCost      = power?.ManaCost   ?? FallbackManaCost;
        int cooldownTurns = power?.Cooldown   ?? FallbackCooldownTurns;

        var attrs  = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
        var player = CombatCharacterHydrator.Hydrate(entity, attrs);

        // Check cooldown.
        string cooldownKey = $"AbilityCooldown_{request.AbilityId}";
        if (attrs.TryGetValue(cooldownKey, out int remaining) && remaining > 0)
            return new UseAbilityInCombatHubResult
            {
                Success      = false,
                ErrorMessage = $"Ability '{request.AbilityId}' is on cooldown for {remaining} more turn(s)",
            };

        // Check mana.
        if (player.Mana < manaCost)
            return new UseAbilityInCombatHubResult
            {
                Success      = false,
                ErrorMessage = "Not enough mana",
            };

        // Deduct mana and apply cooldown.
        player.Mana -= manaCost;
        attrs["CurrentMana"] = player.Mana;
        attrs[cooldownKey]   = cooldownTurns;

        bool isHeal = power?.EffectType == PowerEffectType.Heal
            || request.AbilityId.Contains("heal", StringComparison.OrdinalIgnoreCase);

        int abilityDamage   = 0;
        int healthRestored  = 0;
        int enemyRemainingHealth;

        if (isHeal)
        {
            // Derive healing amount from the DB fallback; Power.HealMin/HealMax are on the
            // EF entity, not the shared model, so we use the fallback constant for now.
            int healingAmount = FallbackHealingAmount;
            int healed = Math.Min(healingAmount, player.MaxHealth - player.Health);
            player.Health += healed;
            attrs["CurrentHealth"] = player.Health;
            healthRestored         = healed;
            enemyRemainingHealth   = enemy.CurrentHealth;
        }
        else
        {
            // Deal magic damage based on Intelligence.
            attrs.TryGetValue("Intelligence", out int intel);
            if (intel == 0) intel = 10;
            int intelMod = (intel - 10) / 2;
            abilityDamage = Math.Max(1, intelMod + entity.Level + 5);

            lock (enemy.SyncRoot)
            {
                enemy.CurrentHealth = Math.Max(0, enemy.CurrentHealth - abilityDamage);
                enemy.LastAttackerId = request.CharacterId;
                enemy.Participants.Add(request.CharacterId);
                enemy.DamageContributions[request.CharacterId] =
                    enemy.DamageContributions.GetValueOrDefault(request.CharacterId) + abilityDamage;
                enemyRemainingHealth = enemy.CurrentHealth;
            }
        }

        // --- Enemy defeated ---
        if (!isHeal && !enemy.IsAlive)
        {
            var rewardList = await CombatHelpers.DistributeRewardsAsync(enemy, storeKey, _characterRepo, cancellationToken);
            var myReward   = rewardList.FirstOrDefault(r => r.CharacterId == request.CharacterId);

            CombatHelpers.ScheduleRespawn(
                storeKey, session.ZoneGroup, enemy.ArchetypeSlug,
                _scopeFactory, _hubContext, _logger);

            CombatHelpers.DecrementCooldowns(attrs);
            entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
            await _characterRepo.UpdateAsync(entity, cancellationToken);

            return new UseAbilityInCombatHubResult
            {
                Success               = true,
                AbilityId             = request.AbilityId,
                AbilityDamage         = abilityDamage,
                ManaCost              = manaCost,
                PlayerRemainingMana   = player.Mana,
                EnemyRemainingHealth  = 0,
                EnemyDefeated         = true,
                PlayerRemainingHealth = player.Health,
                XpEarned              = myReward.Xp,
                GoldEarned            = myReward.Gold,
            };
        }

        // --- Enemy counter-attacks ---
        session = session.NextTurn();
        CombatSessionStore.Set(request.CharacterId, session);

        var (counterDamage, counterAbility) = CombatHelpers.RollEnemyAbility(enemy, player);
        int enemyDamage = counterDamage > 0
            ? counterDamage
            : CombatHelpers.CalculateEnemyDamage(enemy, attrs, session.IsPlayerDefending);

        player.Health = Math.Max(0, player.Health - enemyDamage);
        attrs["CurrentHealth"] = player.Health;

        CombatHelpers.DecrementCooldowns(attrs);
        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        var (isDead, isHardcore) = await CombatHelpers.HandleDeathIfNeededAsync(
            player, entity, attrs, session, enemy, _characterRepo, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterIdPrefix} used ability {AbilityId} — dealt {Damage}, healed {Heal}",
            request.CharacterId.ToString()[..8], request.AbilityId, abilityDamage, healthRestored);

        return new UseAbilityInCombatHubResult
        {
            Success               = true,
            AbilityId             = request.AbilityId,
            AbilityDamage         = abilityDamage,
            HealthRestored        = healthRestored,
            ManaCost              = manaCost,
            PlayerRemainingMana   = player.Mana,
            EnemyRemainingHealth  = enemyRemainingHealth,
            EnemyDefeated         = false,
            EnemyDamage           = enemyDamage,
            EnemyAbilityUsed      = counterAbility,
            PlayerRemainingHealth = player.Health,
            PlayerDefeated        = isDead,
            PlayerHardcoreDeath   = isHardcore,
        };
    }
}
