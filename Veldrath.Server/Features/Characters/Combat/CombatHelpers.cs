using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Hubs;
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

    // Calculates player attack damage from attrs. Returns at least 1.
    internal static int CalculatePlayerDamage(Dictionary<string, int> attrs, int level)
    {
        attrs.TryGetValue("Strength", out int str);
        if (str == 0) str = 10;
        int strMod = (str - 10) / 2;
        int lvlMod = level / 2;
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
        int damage = 0;

        if (ability.EffectType == RealmEngine.Shared.Models.PowerEffectType.Damage)
        {
            int baseDmg = int.TryParse(ability.BaseDamage, out var d) ? d : 0;
            damage = Math.Max(1, (baseDmg > 0 ? baseDmg : 5) + enemy.Level);
        }

        return (damage, ability.Name);
    }

    // Splits the enemy's XP and gold across its contributors, awards each share and releases their combat
    // sessions. The shares always add up to the enemy's configured rewards — see CombatRewards.Split.
    // Returns a list of (CharacterId, Xp, Gold) for broadcasting.
    internal static async Task<List<(Guid CharacterId, int Xp, int Gold)>> DistributeRewardsAsync(
        SpawnedEnemy enemy,
        ICharacterRepository repo,
        ICombatSessionStore sessions,
        CancellationToken ct)
    {
        // Atomic guard: only distribute once even under concurrent kills.
        bool alreadyRewarded;
        lock (enemy.SyncRoot)
        {
            alreadyRewarded = enemy.WasRewarded;
            if (!alreadyRewarded) enemy.WasRewarded = true;
        }
        if (alreadyRewarded) return [];

        List<(Guid CharacterId, int Damage)> contributions;
        lock (enemy.SyncRoot)
        {
            contributions = enemy.DamageContributions
                .Select(entry => (entry.Key, entry.Value))
                .ToList();
        }

        // A participant who never landed a hit earns nothing; only the damaged set is paid.
        if (contributions.Count == 0 || contributions.Sum(contribution => contribution.Damage) <= 0)
            return [];

        var rewards = new List<(Guid, int, int)>(contributions.Count);

        foreach (var share in CombatRewards.Split(enemy.BaseXp, enemy.GoldReward, contributions))
        {
            var character = await repo.GetByIdAsync(share.CharacterId, ct);
            if (character is not null)
            {
                var attrs = ParseAttrs(character.Attributes);
                attrs["Gold"] = attrs.GetValueOrDefault("Gold") + share.Gold;
                character.Experience += share.Xp;
                character.Attributes = SerializeAttrs(attrs);
                await repo.UpdateAsync(character, ct);
            }

            rewards.Add((share.CharacterId, share.Xp, share.Gold));
        }

        // Release every engaged player; the enemy row stays behind as a dead row with a respawn time, so the
        // world remembers it owes a respawn (D3's sweeper revives it).
        await sessions.RemoveByEnemyIdAsync(enemy.Id, ct);

        return rewards;
    }

    // Handles player death: HC → soft-delete; normal → experience/gold penalty and death state.
    // Returns (isDead, isHardcore) for broadcast.
    internal static async Task<(bool isDead, bool isHardcore)> HandleDeathIfNeededAsync(
        Character player,
        ServerCharacter entity,
        Dictionary<string, int> attrs,
        ActiveCombatSession session,
        SpawnedEnemy? enemy,
        ICharacterRepository repo,
        IEnemyInstanceStore enemies,
        ICombatSessionStore sessions,
        CancellationToken ct)
    {
        if (player.Health > 0) return (false, false);

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
