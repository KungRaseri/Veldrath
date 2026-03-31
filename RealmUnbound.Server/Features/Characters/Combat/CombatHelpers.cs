using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;
using RealmUnbound.Server.Data.Entities;
using RealmUnbound.Server.Data.Repositories;
using RealmUnbound.Server.Hubs;

namespace RealmUnbound.Server.Features.Characters.Combat;

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
                key.StartsWith("SpellCooldown_",   StringComparison.OrdinalIgnoreCase))
            {
                attrs[key] = Math.Max(0, attrs[key] - 1);
            }
        }
    }

    // Calculates player attack damage from attrs. Returns at least 1.
    internal static int CalculatePlayerDamage(Dictionary<string, int> attrs, int level)
    {
        attrs.TryGetValue("Strength", out int str);
        if (str == 0) str = 10;
        int strMod  = (str - 10) / 2;
        int lvlMod  = level / 2;
        return Math.Max(1, strMod + lvlMod + 5);
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
    internal static (int damage, string? abilityName) RollEnemyAbility(SpawnedEnemy enemy, Character player)
    {
        if (enemy.Template.Abilities.Count == 0 || Random.Shared.Next(100) >= 30)
            return (0, null);

        var ability = enemy.Template.Abilities[Random.Shared.Next(enemy.Template.Abilities.Count)];
        int damage  = 0;

        if (ability.EffectType == RealmEngine.Shared.Models.PowerEffectType.Damage)
            damage = Math.Max(1, (ability.BaseDamage > 0 ? ability.BaseDamage : 5) + enemy.Level);

        return (damage, ability.Name);
    }

    // Distributes proportional XP and gold to all contributors and clears the combat session.
    // Returns a list of (CharacterId, Xp, Gold) for broadcasting.
    internal static async Task<List<(Guid CharacterId, int Xp, int Gold)>> DistributeRewardsAsync(
        SpawnedEnemy enemy,
        string storeKey,
        ICharacterRepository repo,
        CancellationToken ct)
    {
        // Atomic guard: only distribute once even under concurrent kills.
        bool alreadyRewarded;
        lock (enemy.Lock)
        {
            alreadyRewarded = enemy.WasRewarded;
            if (!alreadyRewarded) enemy.WasRewarded = true;
        }
        if (alreadyRewarded) return [];

        int totalDamage = enemy.DamageContributions.Values.Sum();
        if (totalDamage == 0) return [];

        var rewards = new List<(Guid, int, int)>();

        foreach (var (charId, damage) in enemy.DamageContributions)
        {
            double share = (double)damage / totalDamage;
            int xp   = Math.Max(1, (int)(enemy.BaseXp    * share));
            int gold = Math.Max(0, (int)(enemy.GoldReward * share));

            var character = await repo.GetByIdAsync(charId, ct);
            if (character is not null)
            {
                var attrs = ParseAttrs(character.Attributes);
                attrs["Gold"] = attrs.GetValueOrDefault("Gold") + gold;
                character.Experience += xp;
                character.Attributes  = SerializeAttrs(attrs);
                await repo.UpdateAsync(character, ct);
            }

            rewards.Add((charId, xp, gold));
        }

        CombatSessionStore.RemoveByEnemyId(enemy.Id);
        ZoneLocationEnemyStore.RemoveEnemy(storeKey, enemy.Id);

        return rewards;
    }

    // Fires a delayed task that respawns a single enemy at the given location after the timer expires.
    internal static void ScheduleRespawn(
        string storeKey,
        string zoneGroup,
        string archetypeSlug,
        IServiceScopeFactory scopeFactory,
        IHubContext<GameHub> hubContext,
        ILogger logger,
        int delaySeconds = 60)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds));

                using var scope   = scopeFactory.CreateScope();
                var resolver      = scope.ServiceProvider.GetRequiredService<ActorPoolResolver>();
                var pool          = new List<ActorPoolEntry> { new(archetypeSlug, 1) };
                var roster        = await resolver.SpawnRosterAsync(pool);

                if (roster.Count > 0)
                {
                    var enemy = roster[0];
                    ZoneLocationEnemyStore.AddEnemy(storeKey, enemy);

                    await hubContext.Clients.Group(zoneGroup).SendAsync("EnemySpawned", new
                    {
                        enemy.Id,
                        enemy.Name,
                        enemy.Level,
                        enemy.CurrentHealth,
                        enemy.MaxHealth,
                    });
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error during enemy respawn for {StoreKey}", storeKey);
            }
        });
    }

    // Handles player death: HC → soft-delete; normal → experience/gold penalty and death state.
    // Returns (isDead, isHardcore) for broadcast.
    internal static async Task<(bool isDead, bool isHardcore)> HandleDeathIfNeededAsync(
        Character player,
        Data.Entities.Character entity,
        Dictionary<string, int> attrs,
        ActiveCombatSession session,
        SpawnedEnemy? enemy,
        ICharacterRepository repo,
        CancellationToken ct)
    {
        if (player.Health > 0) return (false, false);

        // Remove from enemy participation
        if (enemy is not null)
        {
            lock (enemy.Lock)
            {
                enemy.Participants.Remove(entity.Id);
                enemy.DamageContributions.Remove(entity.Id);
                if (enemy.LastAttackerId == entity.Id)
                    enemy.LastAttackerId = enemy.Participants.FirstOrDefault();
            }
        }

        CombatSessionStore.Remove(entity.Id);

        bool isHardcore = string.Equals(entity.DifficultyMode, "hardcore", StringComparison.OrdinalIgnoreCase);

        if (isHardcore)
        {
            entity.DeletedAt = DateTimeOffset.UtcNow;
            await repo.UpdateAsync(entity, ct);
        }
        else
        {
            // Standard death: apply XP and gold penalty (10% each, floor 0).
            long xpPenalty = entity.Experience / 10;
            entity.Experience = Math.Max(0, entity.Experience - xpPenalty);

            attrs.TryGetValue("Gold", out int gold);
            int goldPenalty = gold / 10;
            attrs["Gold"] = Math.Max(0, gold - goldPenalty);

            // Restore 1 HP so the character isn't permanently dead
            attrs["CurrentHealth"] = 1;

            entity.Attributes = SerializeAttrs(attrs);
            await repo.UpdateAsync(entity, ct);
        }

        return (true, isHardcore);
    }
}
