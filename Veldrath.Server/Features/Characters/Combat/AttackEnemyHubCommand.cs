using MediatR;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using RealmEngine.Shared.Services;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;

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

    /// <summary>Gets the defeated enemy's display name, used to advance kill objectives.</summary>
    public string? EnemyName { get; init; }

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

    /// <summary>
    /// Gets the level and pool state this player's share of the kill left them at, or <see langword="null"/> when
    /// the turn paid no experience (or the caller was not credited with the kill).
    /// </summary>
    public CombatProgression? Progression { get; init; }

    /// <summary>Gets the gold the death cost, or 0 when the character survived.</summary>
    public int GoldLost { get; init; }

    /// <summary>Gets the experience the death cost, or 0 when the character survived.</summary>
    public int XpLost { get; init; }

    /// <summary>Gets the archetype slug of the defeated enemy, used to advance kill objectives by slug.</summary>
    public string? EnemyArchetypeSlug { get; init; }

    /// <summary>
    /// Gets every character credited with the kill, including party members who joined the fight but never landed a hit.
    /// Only populated when <see cref="EnemyDefeated"/> is <see langword="true"/>.
    /// </summary>
    public IReadOnlyList<Guid> ParticipantIds { get; init; } = [];

    /// <summary>
    /// Gets every character whose combat session this kill released — the killer and anyone else engaged with the
    /// same enemy, including a defender who never landed a hit. Empty unless <see cref="EnemyDefeated"/> is
    /// <see langword="true"/>.
    /// </summary>
    /// <remarks>
    /// The enemy row is shared, so killing it ends every fight pointed at it, not only this one. The hub sends
    /// each of these characters a <c>CombatEnded</c>; without that they keep a panel for a fight the server has
    /// already dropped and their next action is answered "Not in combat".
    /// </remarks>
    public IReadOnlyList<Guid> ReleasedCombatCharacterIds { get; init; } = [];

    /// <summary>Gets the run whose room this kill cleared, or <see langword="null"/> when it cleared nothing.</summary>
    public Guid? ClearedInstanceId { get; init; }

    /// <summary>Gets the room slug that was cleared, or <see langword="null"/>.</summary>
    public string? ClearedLocationSlug { get; init; }

    /// <summary>Gets whether this kill is what cleared the room in this run.</summary>
    public bool FirstClearOfRun { get; init; }

    /// <summary>Gets whether this is the first time this character has ever cleared the room.</summary>
    public bool FirstClearForCharacter { get; init; }

    /// <summary>Gets the hidden locations this clear unlocked for the character.</summary>
    public IReadOnlyList<string> UnlockedLocationSlugs { get; init; } = [];

    /// <summary>
    /// Gets what the defeated enemy dropped into the killer's bag. Empty when the archetype names no loot table or
    /// nothing rolled, which is the normal case for most kills.
    /// </summary>
    public IReadOnlyList<CombatLootDrop> ItemsDropped { get; init; } = [];
}

/// <summary>One item a defeated enemy dropped.</summary>
/// <param name="ItemSlug">Slug of the item, as it was added to the bag.</param>
/// <param name="Quantity">How many were added.</param>
public record CombatLootDrop(string ItemSlug, int Quantity);

/// <summary>
/// What a combat reward left the character at: the level, the experience still banked toward the next level, and
/// the derived pools the award rewrote.
/// </summary>
/// <remarks>
/// <para>
/// The caller's own turn carries this so the sheet can move without a reload. It mirrors the fields
/// <see cref="Veldrath.Server.Features.LevelUp.GainExperienceHubResult"/> reports after a quest turn-in: a levelling
/// award banks the remainder instead of adding it to the old bar, and it rewrites <c>MaxHealth</c>/<c>MaxMana</c>.
/// </para>
/// <para>
/// A client that only added <see cref="AttackEnemyHubResult.XpEarned"/> to its own numbers would show the old
/// level beside an experience bar past its threshold. <see cref="NewExperience"/> is the authoritative remainder;
/// <see cref="XpEarned"/> stays the amount the kill paid, for the log line.
/// </para>
/// </remarks>
/// <param name="OldLevel">The character's level before the award.</param>
/// <param name="NewLevel">The character's level after the award.</param>
/// <param name="NewExperience">The experience still banked toward the next level after the award.</param>
/// <param name="LeveledUp">Whether this award crossed at least one level.</param>
/// <param name="MaxHealth">The maximum health stored in the attributes blob after the award.</param>
/// <param name="CurrentHealth">The current health stored in the blob after the award.</param>
/// <param name="MaxMana">The maximum mana stored in the blob after the award.</param>
/// <param name="CurrentMana">The current mana stored in the blob after the award.</param>
/// <param name="UnspentAttributePoints">The attribute points the blob holds unspent after the award.</param>
public record CombatProgression(
    int OldLevel,
    int NewLevel,
    long NewExperience,
    bool LeveledUp,
    int MaxHealth,
    int CurrentHealth,
    int MaxMana,
    int CurrentMana,
    int UnspentAttributePoints);

/// <summary>
/// Handles <see cref="AttackEnemyHubCommand"/> by loading the player and enemy state,
/// computing and applying damage in both directions, handling death outcomes,
/// and persisting updated character state.
/// </summary>
public class AttackEnemyHubCommandHandler
    : IRequestHandler<AttackEnemyHubCommand, AttackEnemyHubResult>
{
    private readonly ICharacterRepository _characterRepo;
    private readonly ICharacterClassRepository _classRepo;
    private readonly IEnemyInstanceStore _enemies;
    private readonly ICombatSessionStore _sessions;
    private readonly Veldrath.Server.Features.Zones.IZoneInstanceService _instances;
    private readonly IOptions<DeathPenaltyOptions> _penalty;
    private readonly ILogger<AttackEnemyHubCommandHandler> _logger;
    private readonly ILootTableRepository _lootTables;
    private readonly IRandomSource _random;

    /// <summary>Initializes a new instance of <see cref="AttackEnemyHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository for loading and persisting character state.</param>
    /// <param name="classRepo">
    /// Catalogue repository used to resolve the killer's class curve when a reward crosses a level.
    /// </param>
    /// <param name="enemies">Store for the live enemy instances.</param>
    /// <param name="sessions">Store for per-character combat sessions.</param>
    /// <param name="instances">Instance-run service, used to record a cleared room.</param>
    /// <param name="penalty">Configured death-penalty values.</param>
    /// <param name="lootTables">Loot-table catalog, rolled when the enemy dies.</param>
    /// <param name="logger">Logger instance.</param>
    /// <param name="randomSource">Optional random source; defaults to <see cref="SystemRandomSource.Shared"/>.</param>
    public AttackEnemyHubCommandHandler(
        ICharacterRepository characterRepo,
        ICharacterClassRepository classRepo,
        IEnemyInstanceStore enemies,
        ICombatSessionStore sessions,
        Veldrath.Server.Features.Zones.IZoneInstanceService instances,
        IOptions<DeathPenaltyOptions> penalty,
        ILootTableRepository lootTables,
        ILogger<AttackEnemyHubCommandHandler> logger,
        IRandomSource? randomSource = null)
    {
        _characterRepo = characterRepo;
        _classRepo = classRepo;
        _enemies = enemies;
        _sessions = sessions;
        _instances = instances;
        _penalty = penalty;
        _lootTables = lootTables;
        _logger = logger;
        _random = randomSource ?? SystemRandomSource.Shared;
    }

    /// <summary>Handles the command and returns the combat turn outcome.</summary>
    /// <param name="request">The command containing the acting character's ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AttackEnemyHubResult"/> describing the turn outcome.</returns>
    public async Task<AttackEnemyHubResult> Handle(
        AttackEnemyHubCommand request,
        CancellationToken cancellationToken)
    {
        var session = await _sessions.TryGetAsync(request.CharacterId, cancellationToken);
        if (session is null)
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Not in combat" };

        var enemy = await _enemies.TryGetAsync(session.EnemyId, cancellationToken);
        if (enemy is null)
        {
            await _sessions.RemoveAsync(request.CharacterId, cancellationToken);
            // Failsafe: a combat session pointing at a missing row is a content bug worth an Error, not a shrug.
            _logger.LogError(
                "Combat session for character {CharacterIdPrefix} pointed at enemy {EnemyId}, which no longer exists",
                request.CharacterId.ToString()[..8], session.EnemyId);
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Enemy no longer exists" };
        }

        if (!enemy.IsAlive)
        {
            await _sessions.RemoveAsync(request.CharacterId, cancellationToken);
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Enemy is already dead" };
        }

        var entity = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (entity is null)
            return new AttackEnemyHubResult { Success = false, ErrorMessage = "Character not found" };

        var attrs = CombatHelpers.ParseAttrs(entity.Attributes, _logger);
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

        await _enemies.SaveAsync(enemy, session.ZoneGroup, session.LocationSlug, cancellationToken);

        // --- Enemy defeated ---
        if (!enemy.IsAlive)
        {
            CombatRewardShare? myReward = null;
            IReadOnlyList<Guid> released = [];
            IReadOnlyList<Guid> participants;
            lock (enemy.SyncRoot)
            {
                participants = enemy.Participants.ToList();
            }

            // Only the caller that wins the death transition distributes rewards.
            if (await _enemies.TryMarkDeadAsync(enemy.Id, cancellationToken))
            {
                var distribution = await CombatHelpers.DistributeRewardsAsync(
                    enemy, _characterRepo, _classRepo, _sessions, cancellationToken, _logger);
                myReward = distribution.Rewards.FirstOrDefault(r => r.CharacterId == request.CharacterId);
                released = distribution.ReleasedCharacterIds;
            }

            _logger.LogInformation(
                "Enemy {EnemyName} defeated by {CharacterIdPrefix} with {ParticipantCount} participant(s)",
                enemy.Name, request.CharacterId.ToString()[..8], participants.Count);

            // Rewards were written straight to the character row, so re-read before writing the cooldowns back:
            // saving the pre-reward attribute blob here would silently undo the gold that was just awarded.
            var rewarded = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
            if (rewarded is not null)
            {
                var rewardedAttrs = CombatHelpers.ParseAttrs(rewarded.Attributes, _logger);
                CombatHelpers.DecrementCooldowns(rewardedAttrs);
                rewarded.Attributes = CombatHelpers.SerializeAttrs(rewardedAttrs);
                await _characterRepo.UpdateAsync(rewarded, cancellationToken);
            }

            // The kill is the only moment the archetype's loot table means anything: it is wired in the seed, but
            // until this ran nothing ever rolled it. Shared with the ability death path so the two cannot drift.
            var droppedItems = await CombatHelpers.RollLootAsync(
                enemy, entity, _lootTables, _characterRepo, _random, cancellationToken, _logger);

            // Clearing the floor is a location fact, not a combat fact: the helper asks the store how many are
            // still standing rather than trusting this one death.
            var cleared = await CombatHelpers.ResolveRoomClearAsync(
                enemy, request.CharacterId, _enemies, _instances, cancellationToken);

            return new AttackEnemyHubResult
            {
                Success = true,
                PlayerDamage = playerDamage,
                EnemyRemainingHealth = 0,
                EnemyDefeated = true,
                EnemyName = enemy.Name,
                EnemyArchetypeSlug = enemy.ArchetypeSlug,
                ParticipantIds = participants,
                ReleasedCombatCharacterIds = released,
                PlayerRemainingHealth = player.Health,
                XpEarned = myReward?.Xp ?? 0,
                GoldEarned = myReward?.Gold ?? 0,
                Progression = myReward?.Progression,
                ClearedInstanceId = cleared?.InstanceId,
                ClearedLocationSlug = cleared?.LocationSlug,
                FirstClearOfRun = cleared?.FirstClearOfRun ?? false,
                FirstClearForCharacter = cleared?.FirstClearForCharacter ?? false,
                UnlockedLocationSlugs = cleared?.UnlockedLocationSlugs ?? [],
                ItemsDropped = droppedItems,
            };
        }

        // --- Enemy counter-attacks ---
        session = session.NextTurn();
        await _sessions.SetAsync(request.CharacterId, session, cancellationToken);

        var (abilityDamage, abilityName) = CombatHelpers.RollEnemyAbility(enemy, player, _random);
        int enemyDamage = abilityDamage > 0
            ? abilityDamage
            : CombatHelpers.CalculateEnemyDamage(enemy, attrs, session.IsPlayerDefending);

        player.Health = Math.Max(0, player.Health - enemyDamage);
        attrs["CurrentHealth"] = player.Health;

        CombatHelpers.DecrementCooldowns(attrs);
        entity.Attributes = CombatHelpers.SerializeAttrs(attrs);
        await _characterRepo.UpdateAsync(entity, cancellationToken);

        var death = await CombatHelpers.HandleDeathIfNeededAsync(
            player, entity, attrs, session, enemy, _characterRepo, _enemies, _sessions,
            _penalty.Value, cancellationToken);

        return new AttackEnemyHubResult
        {
            Success = true,
            PlayerDamage = playerDamage,
            EnemyRemainingHealth = enemyRemainingHealth,
            EnemyDefeated = false,
            EnemyName = enemy.Name,
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
