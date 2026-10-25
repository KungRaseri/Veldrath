using Veldrath.Server.Features.Characters.Combat;

namespace Veldrath.Server.Tests.Infrastructure;

/// <summary>
/// In-memory <see cref="ICombatSessionStore"/> for handler tests.
/// </summary>
/// <remarks>
/// The sync helpers exist so tests can arrange and assert state without awaiting; the interface methods simply
/// delegate to them. Replaces direct use of the deleted static <c>CombatSessionStore</c>.
/// </remarks>
internal sealed class FakeCombatSessionStore : ICombatSessionStore
{
    private readonly Dictionary<Guid, ActiveCombatSession> _sessions = new();
    private readonly HashSet<Guid> _prunable = new();

    /// <summary>Gets how many times a session has been written.</summary>
    public int SetCount { get; private set; }

    /// <summary>Arranges a session.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="session">Session state.</param>
    public void Set(Guid characterId, ActiveCombatSession session)
    {
        _sessions[characterId] = session;
        SetCount++;
    }

    /// <summary>Returns whether the character has a session.</summary>
    /// <param name="characterId">Character id.</param>
    public bool IsInCombat(Guid characterId) => _sessions.ContainsKey(characterId);

    /// <summary>Removes a session synchronously.</summary>
    /// <param name="characterId">Character id.</param>
    public void Remove(Guid characterId) => _sessions.Remove(characterId);

    /// <summary>Returns the stored session, if any.</summary>
    /// <param name="characterId">Character id.</param>
    public ActiveCombatSession? Find(Guid characterId) =>
        _sessions.TryGetValue(characterId, out var session) ? session : null;

    /// <inheritdoc />
    public Task<ActiveCombatSession?> TryGetAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        Task.FromResult(Find(characterId));

    /// <inheritdoc />
    public Task<bool> IsInCombatAsync(Guid characterId, CancellationToken cancellationToken = default) =>
        Task.FromResult(IsInCombat(characterId));

    /// <inheritdoc />
    public Task SetAsync(Guid characterId, ActiveCombatSession session, CancellationToken cancellationToken = default)
    {
        Set(characterId, session);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveAsync(Guid characterId, CancellationToken cancellationToken = default)
    {
        Remove(characterId);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> RemoveByEnemyIdAsync(Guid enemyId, CancellationToken cancellationToken = default)
    {
        var released = _sessions
            .Where(pair => pair.Value.EnemyId == enemyId)
            .Select(pair => pair.Key)
            .ToList();

        foreach (var characterId in released)
        {
            _sessions.Remove(characterId);
        }

        return Task.FromResult<IReadOnlyList<Guid>>(released);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> PruneExpiredAsync(TimeSpan grace, CancellationToken cancellationToken = default)
    {
        var pruned = _prunable.ToList();
        foreach (var characterId in pruned)
        {
            _sessions.Remove(characterId);
            _prunable.Remove(characterId);
        }

        return Task.FromResult<IReadOnlyList<Guid>>(pruned);
    }

    /// <summary>Arranges a session whose owner disconnected long enough to be pruned.</summary>
    /// <param name="characterId">Character id.</param>
    public void ArrangeExpired(Guid characterId) => _prunable.Add(characterId);
}
