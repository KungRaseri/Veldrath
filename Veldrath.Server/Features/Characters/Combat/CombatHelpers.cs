using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.LevelUp;
using Veldrath.Server.Features.Zones;
using Veldrath.Server.Hubs;
using Veldrath.Server.Settings;
using ServerCharacter = Veldrath.Server.Data.Entities.Character;

namespace Veldrath.Server.Features.Characters.Combat;

// Internal helpers shared by all combat hub command handlers.
// Marked internal so they require no XML doc comments (CS1591 only applies to public members).
internal static class CombatHelpers
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    // Deserializes the character attributes blob; returns an empty dict on failure.
    internal static Dictionary<string, int> ParseAttrs(string? attrsJson, ILogger? logger = null)
    {
        var attrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(attrsJson) || attrsJson == "{}") return attrs;
        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(attrsJson, JsonOptions) ?? attrs;
        }
        catch (JsonException ex)
        {
            logger?.LogWarning(ex, "Failed to deserialise character attributes; treating as empty");
            return attrs;
        }
    }

    // Serializes attrs back to JSON for storage.
    internal static string SerializeAttrs(Dictionary<string, int> attrs) =>
        JsonSerializer.Serialize(attrs);

    // Decrements all ability and spell cooldown entries in the blob by 1 (floor 0).
    internal static void DecrementCooldowns(Dictionary<string, int> attrs)
    {
        foreach (var key in attrs.Keys.ToList())
        {
            if (key.StartsWith("AbilityCooldown_", StringComparison.OrdinalIgnoreCase) ||
                key.StartsWith("SpellCooldown_", StringComparison.OrdinalIgnoreCase))
            {
                attrs[key] = Math.Max(0, attrs[key] - 1);
            }
        }
    }

    // Calculates player attack damage from attrs. Returns at least 1. Death sickness saps it while it lasts.
    internal static int CalculatePlayerDamage(Dictionary<string, int> attrs, int level)
    {
        attrs.TryGetValue("Strength", out int str);
        if (str == 0) str = 10;
        int strMod = (str - 10) / 2;
        int lvlMod = level / 2;
        int damage = Math.Max(1, strMod + lvlMod + 5);

        return DeathPenalties.ApplySicknessToDamage(damage, attrs, DateTimeOffset.UtcNow);
    }

    // The tier the client draws around an enemy portrait: boss beats elite; both are inert content labels,
    // so this is display metadata and never affects a fight.
    internal static string ResolveTier(Enemy? template)
    {
        if (template is null) return "regular";
        if (template.Traits.TryGetValue("boss", out var boss) && boss.AsBool()) return "boss";
        if (template.Traits.TryGetValue("elite", out var elite) && elite.AsBool()) return "elite";
        return "regular";
    }

    // The archetype's content type key, which the client maps onto a silhouette class to pick the fallback
    // glyph (and the townsfolk plate). Display metadata like ResolveTier; no combat rule reads it.
    internal static string ResolveTypeKey(Enemy? template)
    {
        return template is not null && template.Traits.TryGetValue("typeKey", out var typeKey)
            ? typeKey.AsString()
            : string.Empty;
    }

    // Calculates enemy attack damage against the player. Halved when defending.
    internal static int CalculateEnemyDamage(SpawnedEnemy enemy, Dictionary<string, int> attrs, bool isDefending)
    {
        int base_dmg = Math.Max(1, enemy.Template.BasePhysicalDamage + (enemy.Level - 1));
        attrs.TryGetValue("Constitution", out int con);
        if (con == 0) con = 10;
        int defense = Math.Max(0, (con - 10) / 2);
        if (isDefending) defense = defense + base_dmg / 2; // additional block
        return Math.Max(1, base_dmg - defense);
    }

    // Picks a random enemy ability (30% chance when abilities exist), returns display name or null.
    internal static (int damage, string? abilityName) RollEnemyAbility(SpawnedEnemy enemy, Character player, IRandomSource randomSource)
    {
        ArgumentNullException.ThrowIfNull(randomSource);

        if (enemy.Template.Abilities.Count == 0 || randomSource.Next(100) >= 30)
            return (0, null);

        var ability = enemy.Template.Abilities[randomSource.Next(enemy.Template.Abilities.Count)];
        int damage = 0;

        if (ability.EffectType == RealmEngine.Shared.Models.PowerEffectType.Damage)
        {
            int baseDmg = int.TryParse(ability.BaseDamage, out var d) ? d : 0;
            damage = Math.Max(1, (baseDmg > 0 ? baseDmg : 5) + enemy.Level);
        }

        return (damage, ability.Name);
    }

    // Rolls the defeated archetype's loot table and puts the result in the killer's bag.
    //
    // The bag is the character's own `InventoryBlob` — the store the client reads. `IInventoryService`
    // writes save-game records, so drops "added" through it were announced and never carried.
    //
    // Each entry rolls independently against its own weight, which is what the seed authored ("drop weight",
    // not "pick one"), and a guaranteed entry always lands. A catalog read that fails is logged and skipped: a
    // kill must not fail because loot could not be read.
    //
    // Lives here rather than in one handler so that every death path rolls the same table with the same source:
    // an ability that lands the killing blow drops what a melee blow of the same archetype drops.
    internal static async Task<IReadOnlyList<CombatLootDrop>> RollLootAsync(
        SpawnedEnemy enemy,
        ServerCharacter entity,
        ILootTableRepository lootTables,
        ICharacterRepository repo,
        IRandomSource random,
        CancellationToken ct,
        ILogger? logger = null)
    {
        ArgumentNullException.ThrowIfNull(random);

        var tableSlugs = enemy.Template?.LootTableIds ?? [];
        if (tableSlugs.Count == 0) return [];

        var bag = CharacterBag.Read(entity.InventoryBlob);
        var drops = new List<CombatLootDrop>();

        foreach (var tableSlug in tableSlugs)
        {
            LootTableData? table;
            try
            {
                table = await lootTables.GetBySlugAsync(tableSlug);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger?.LogError(ex, "Loot table {TableSlug} could not be read; the kill drops nothing", tableSlug);
                continue;
            }

            if (table is null || table.Entries.Count == 0) continue;

            var totalWeight = table.Entries.Sum(entry => Math.Max(1, entry.DropWeight));

            foreach (var entry in table.Entries)
            {
                if (!entry.IsGuaranteed && random.Next(totalWeight) >= Math.Max(1, entry.DropWeight)) continue;

                var quantity = entry.QuantityMax > entry.QuantityMin
                    ? random.Next(entry.QuantityMin, entry.QuantityMax + 1)
                    : Math.Max(1, entry.QuantityMin);

                CharacterBag.Add(bag, entry.ItemSlug, quantity);
                drops.Add(new CombatLootDrop(entry.ItemSlug, quantity));
            }
        }

        if (drops.Count > 0)
        {
            entity.InventoryBlob = CharacterBag.Serialize(bag);
            await repo.UpdateAsync(entity, ct);

            logger?.LogInformation(
                "Enemy {EnemyName} dropped {DropCount} stack(s) into {CharacterName}'s bag",
                enemy.Name, drops.Count, entity.Name);
        }

        return drops;
    }

    // Records what the killing blow that emptied a location earned, or null when the room still holds an enemy.
    //
    // Clearing the floor is a location fact, not a combat fact: ask the store how many are still standing rather
    // than trusting this one death. Only a run records the clear itself — a shared-zone room has no run to credit,
    // so it never unlocks anything — and the room still stays quiet for the cleared-room window in either case.
    //
    // Shared by both death paths, so an ability kill clears a room exactly as a melee kill does.
    internal static async Task<CombatRoomClear?> ResolveRoomClearAsync(
        SpawnedEnemy enemy,
        Guid characterId,
        IEnemyInstanceStore enemies,
        IZoneInstanceService instances,
        CancellationToken ct)
    {
        if (await enemies.CountAliveAsync(enemy.ZoneGroup, enemy.LocationSlug, ct) > 0) return null;

        // A cleared room stays quiet for the cleared-room window, in a public dungeon as much as in a
        // run: the promise in plans/dungeons.md §6 is "longer respawn", not no respawn.
        await enemies.ExtendClearedRespawnsAsync(enemy.ZoneGroup, enemy.LocationSlug, ct);

        if (enemy.ZoneInstanceId is not Guid runId) return null;

        var cleared = await instances.RecordClearAsync(runId, enemy.LocationSlug, characterId, ct);
        if (!cleared.Cleared) return null;

        return new CombatRoomClear(
            runId,
            enemy.LocationSlug,
            cleared.FirstClearOfRun,
            cleared.FirstClearForCharacter,
            cleared.UnlockedLocationSlugs);
    }

    // Settles a death exactly once, whichever blow landed it. The caller that wins the store's death transition is
    // the one that takes everything the death produces: the credited participants, the rewards, the loot and the
    // room clear. Every other caller that walked into the same dead enemy still reports the death to its own client
    // (its fight is over) but takes none of it — a second credit would advance every participant's kill objective
    // twice for one kill, and a second loot roll would hand out a second drop.
    //
    // The participant set comes from the transition itself rather than from the caller's SpawnedEnemy copy: the EF
    // store materialises a fresh view per read, so the copy can be older than the kill. Reading it at the
    // transition credits a participant whose turn committed while the winner was still loading, which the winner's
    // own snapshot would have missed.
    //
    // Lives here so the ability and melee death paths cannot disagree about any of that again.
    internal static async Task<CombatKillSettlement> SettleKillAsync(
        SpawnedEnemy enemy,
        ServerCharacter entity,
        Guid characterId,
        IEnemyInstanceStore enemies,
        ICharacterRepository characters,
        ICharacterClassRepository classes,
        ICombatSessionStore sessions,
        ILootTableRepository lootTables,
        IZoneInstanceService instances,
        IRandomSource random,
        CancellationToken ct,
        ILogger? logger = null)
    {
        var credited = await enemies.TryMarkDeadAsync(enemy.Id, ct);
        if (credited is null)
        {
            return CombatKillSettlement.Uncredited;
        }

        var distribution = await DistributeRewardsAsync(enemy, characters, classes, sessions, ct, logger);

        return new CombatKillSettlement(
            credited,
            distribution.Rewards.FirstOrDefault(share => share.CharacterId == characterId),
            distribution.ReleasedCharacterIds,
            await RollLootAsync(enemy, entity, lootTables, characters, random, ct, logger),
            await ResolveRoomClearAsync(enemy, characterId, enemies, instances, ct));
    }

    // Splits the enemy's XP and gold across its contributors, awards each share and releases their combat
    // sessions. The shares always add up to the enemy's configured rewards — see CombatRewards.Split.
    // Returns one CombatRewardShare per credited participant, carrying what the award left them at, plus the ids
    // of every session the kill released: a fight is shared, so the callers that only heard about their own turn
    // are told the rest from that list.
    internal static async Task<CombatRewardDistribution> DistributeRewardsAsync(
        SpawnedEnemy enemy,
        ICharacterRepository repo,
        ICharacterClassRepository classes,
        ICombatSessionStore sessions,
        CancellationToken ct,
        ILogger? logger = null)
    {
        // Atomic guard: only distribute once even under concurrent kills.
        bool alreadyRewarded;
        lock (enemy.SyncRoot)
        {
            alreadyRewarded = enemy.WasRewarded;
            if (!alreadyRewarded) enemy.WasRewarded = true;
        }
        if (alreadyRewarded) return new CombatRewardDistribution([], []);

        List<(Guid CharacterId, int Damage)> contributions;
        lock (enemy.SyncRoot)
        {
            contributions = enemy.DamageContributions
                .Select(entry => (entry.Key, entry.Value))
                .ToList();
        }

        // A participant who never landed a hit earns nothing; only the damaged set is paid.
        if (contributions.Count == 0 || contributions.Sum(contribution => contribution.Damage) <= 0)
            return new CombatRewardDistribution([], []);

        var rewards = new List<CombatRewardShare>(contributions.Count);

        foreach (var share in CombatRewards.Split(enemy.BaseXp, enemy.GoldReward, contributions))
        {
            CombatProgression? progression = null;

            var character = await repo.GetByIdAsync(share.CharacterId, ct);
            if (character is not null)
            {
                var attrs = ParseAttrs(character.Attributes);
                attrs["Gold"] = attrs.GetValueOrDefault("Gold") + share.Gold;

                // Combat XP goes through the same helper the quest turn-in and the dev endpoint use, so a kill
                // that crosses a threshold moves Level and rewrites the derived pools exactly as they do.
                // Writing Experience directly (what this did) left a career killer pinned at level 1 while the
                // bar filled past every threshold. The class row keeps the new pools on the creation curve.
                var progress = CharacterProgression.AwardExperience(
                    character,
                    share.Xp,
                    attrs,
                    ResolveCharacterClass(classes, character.ClassName, logger));

                character.Attributes = SerializeAttrs(attrs);
                await repo.UpdateAsync(character, ct);

                progression = ToProgression(progress, attrs);
            }

            rewards.Add(new CombatRewardShare(share.CharacterId, share.Xp, share.Gold, progression));
        }

        // Release every engaged player; the enemy row stays behind as a dead row with a respawn time, so the
        // world remembers it owes a respawn (D3's sweeper revives it). The released ids are part of the result
        // because a fight is shared: everyone whose session this removed is now holding a panel for a fight the
        // server has dropped, and CombatEnded (sent from those ids) is the only thing that closes it.
        var released = await sessions.RemoveByEnemyIdAsync(enemy.Id, ct);

        return new CombatRewardDistribution(rewards, released);
    }

    // Resolves the catalogue row the character was created from so a combat level-up extends the class's own
    // pool curve. A miss leaves CharacterProgression's engine formulas as the fallback.
    private static CharacterClass? ResolveCharacterClass(
        ICharacterClassRepository classes,
        string? className,
        ILogger? logger)
    {
        if (string.IsNullOrWhiteSpace(className))
            return null;

        try
        {
            return classes.GetByName(className);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // A content read must never swallow a kill's reward; the award still lands, on the engine curve.
            logger?.LogWarning(
                ex,
                "Could not resolve character class {ClassName}; deriving combat level-up pools from the engine formulas instead.",
                className);
            return null;
        }
    }

    // Projects what the award left in the blob, mirroring the fields GainExperienceHubResult reports.
    private static CombatProgression ToProgression(
        CharacterProgression.Progress progress,
        Dictionary<string, int> attrs) =>
        new(
            progress.OldLevel,
            progress.NewLevel,
            progress.RemainingExperience,
            progress.LevelledUp,
            attrs.GetValueOrDefault(CharacterProgression.KeyMaxHealth),
            attrs.GetValueOrDefault(
                CharacterProgression.KeyCurrentHealth,
                attrs.GetValueOrDefault(CharacterProgression.KeyMaxHealth)),
            attrs.GetValueOrDefault(CharacterProgression.KeyMaxMana),
            attrs.GetValueOrDefault(
                CharacterProgression.KeyCurrentMana,
                attrs.GetValueOrDefault(CharacterProgression.KeyMaxMana)),
            attrs.GetValueOrDefault(CharacterProgression.KeyUnspentAttributePoints));

    // Handles player death: HC → soft-delete; normal → the configured gold-or-XP penalty plus the death state.
    // Returns a DeathOutcome carrying whether the character died and what the death cost.
    internal static async Task<DeathOutcome> HandleDeathIfNeededAsync(
        Character player,
        ServerCharacter entity,
        Dictionary<string, int> attrs,
        ActiveCombatSession session,
        SpawnedEnemy? enemy,
        ICharacterRepository repo,
        IEnemyInstanceStore enemies,
        ICombatSessionStore sessions,
        DeathPenaltyOptions penaltyOptions,
        CancellationToken ct)
    {
        if (player.Health > 0) return DeathOutcome.Alive;

        // Remove from enemy participation, and persist it so a dead player is not still counted as a contributor.
        if (enemy is not null)
        {
            lock (enemy.SyncRoot)
            {
                enemy.Participants.Remove(entity.Id);
                enemy.DamageContributions.Remove(entity.Id);
                if (enemy.LastAttackerId == entity.Id)
                    enemy.LastAttackerId = enemy.Participants.FirstOrDefault();
            }

            await enemies.SaveAsync(enemy, session.ZoneGroup, session.LocationSlug, ct);
        }

        await sessions.RemoveAsync(entity.Id, ct);

        bool isHardcore = string.Equals(entity.DifficultyMode, "hardcore", StringComparison.OrdinalIgnoreCase);

        if (isHardcore)
        {
            // Hardcore is permanent; there is nothing left to punish, so no penalty is applied or reported.
            entity.DeletedAt = DateTimeOffset.UtcNow;
            await repo.UpdateAsync(entity, ct);
            return new DeathOutcome(true, true, new DeathPenalty(0, 0));
        }

        // Gold first, experience only when the character is essentially broke — never both. The applied amounts are
        // returned so the client can name them instead of leaving the player to notice the difference.
        attrs.TryGetValue("Gold", out int gold);
        var applied = DeathPenalties.Calculate(gold, entity.Experience, penaltyOptions);

        attrs["Gold"] = Math.Max(0, gold - applied.GoldLost);
        entity.Experience = Math.Max(0, entity.Experience - applied.XpLost);

        // Death is a state, not a health floor: CurrentHealth stays 0 so the character really is dead.
        //
        // This used to restore 1 HP "so the character isn't permanently dead", which made the character look alive
        // to everything downstream — and made RespawnHubCommand refuse, because it only respawns a character at 0 HP.
        // The measured result was a locked-out player: the client and the combat session agreed they were dead
        // ("You cannot act while dead."), the server answered respawn with "Character is not dead", and nothing
        // could move them. Respawn is the recovery path, and it restores 25% of max health.
        attrs["CurrentHealth"] = 0;

        entity.Attributes = SerializeAttrs(attrs);
        await repo.UpdateAsync(entity, ct);

        return new DeathOutcome(true, false, applied);
    }
}

/// <summary>What one contributor's share of a kill paid, and the level state the award left them at.</summary>
/// <param name="CharacterId">The credited character.</param>
/// <param name="Xp">The experience share.</param>
/// <param name="Gold">The gold share.</param>
/// <param name="Progression">
/// The level and pools after the award, or <see langword="null"/> when the character row could not be loaded.
/// </param>
internal sealed record CombatRewardShare(Guid CharacterId, int Xp, int Gold, CombatProgression? Progression);

/// <summary>
/// What distributing one kill's rewards paid out, and which combat sessions the kill released.
/// </summary>
/// <remarks>
/// A fight is shared: the enemy row is one instance and every engaged character holds a session pointed at it, so
/// the killing blow releases all of them, not only the killer's. <paramref name="ReleasedCharacterIds"/> is that
/// set — including a defender who never landed a hit and therefore earns no share — and it is what lets the hub
/// tell each of them their fight is over instead of answering their next action with "Not in combat".
/// </remarks>
/// <param name="Rewards">One share per credited contributor, in the order the split paid them.</param>
/// <param name="ReleasedCharacterIds">Characters whose combat session the kill removed.</param>
internal sealed record CombatRewardDistribution(
    IReadOnlyList<CombatRewardShare> Rewards,
    IReadOnlyList<Guid> ReleasedCharacterIds);

/// <summary>What the killing blow that emptied a location cleared, ready to project onto a hub result.</summary>
/// <param name="InstanceId">The run the room belonged to.</param>
/// <param name="LocationSlug">The room that was cleared.</param>
/// <param name="FirstClearOfRun">Whether this kill is what cleared the room in the run.</param>
/// <param name="FirstClearForCharacter">Whether the killer had ever cleared the room before.</param>
/// <param name="UnlockedLocationSlugs">The hidden locations this clear made visible to the killer.</param>
internal sealed record CombatRoomClear(
    Guid InstanceId,
    string LocationSlug,
    bool FirstClearOfRun,
    bool FirstClearForCharacter,
    IReadOnlyList<string> UnlockedLocationSlugs);

/// <summary>
/// What the caller that won an enemy's death transition took from the kill: the participants it credits, its own
/// share of the rewards, the sessions the kill released, the loot it rolled and the room clear it recorded.
/// </summary>
/// <remarks>
/// One kill is settled once. A caller that loses the transition gets <see cref="Uncredited"/> — the death still
/// travels on its result (its own fight is over), but it credits nobody, rolls nothing and clears nothing.
/// </remarks>
/// <param name="ParticipantIds">Characters the kill credits; empty when this caller lost the transition.</param>
/// <param name="Reward">This caller's share of the kill, or <see langword="null"/> when it earned none.</param>
/// <param name="ReleasedCombatCharacterIds">Characters whose combat session the kill removed.</param>
/// <param name="ItemsDropped">Loot rolled into the caller's bag.</param>
/// <param name="Clear">The room clear this kill recorded, or <see langword="null"/> when it cleared nothing.</param>
internal sealed record CombatKillSettlement(
    IReadOnlyList<Guid> ParticipantIds,
    CombatRewardShare? Reward,
    IReadOnlyList<Guid> ReleasedCombatCharacterIds,
    IReadOnlyList<CombatLootDrop> ItemsDropped,
    CombatRoomClear? Clear)
{
    /// <summary>The settlement of a caller that lost the death transition to another caller.</summary>
    public static CombatKillSettlement Uncredited { get; } = new([], null, [], [], null);
}
