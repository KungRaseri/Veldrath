using System.Collections.Concurrent;

namespace Veldrath.Server.Features.Characters.Combat;

/// <summary>In-process store of active per-player combat sessions, keyed by character ID.</summary>
public static class CombatSessionStore
{
    private static readonly ConcurrentDictionary<Guid, ActiveCombatSession> _sessions = new();

    /// <summary>Tries to retrieve the active combat session for a character.</summary>
    public static bool TryGet(Guid characterId, [System.Diagnostics.CodeAnalysis.MaybeNullWhen(false)] out ActiveCombatSession session) =>
        _sessions.TryGetValue(characterId, out session);

    /// <summary>Adds or replaces the combat session for a character.</summary>
    public static void Set(Guid characterId, ActiveCombatSession session) =>
        _sessions[characterId] = session;

    /// <summary>Removes any active combat session for a character.</summary>
    public static void Remove(Guid characterId) =>
        _sessions.TryRemove(characterId, out _);

    /// <summary>
    /// Removes all sessions that reference the given enemy instance and returns the affected character IDs.
    /// Called when an enemy is defeated so all engaged players are released from combat.
    /// </summary>
    public static IReadOnlyList<Guid> RemoveByEnemyId(Guid enemyId)
    {
        var removed = new List<Guid>();
        foreach (var (charId, session) in _sessions)
        {
            if (session.EnemyId == enemyId && _sessions.TryRemove(charId, out _))
                removed.Add(charId);
        }
        return removed;
    }

    /// <summary>Returns whether the given character has an active combat session.</summary>
    public static bool IsInCombat(Guid characterId) => _sessions.ContainsKey(characterId);
}
