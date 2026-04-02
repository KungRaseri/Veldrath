using System.Collections.Concurrent;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;

namespace RealmEngine.Data.Repositories;

/// <summary>
/// In-memory implementation of <see cref="ICharacterCreationSessionStore"/> backed by a
/// <see cref="ConcurrentDictionary{TKey,TValue}"/>. Register as a singleton.
/// </summary>
public class InMemoryCharacterCreationSessionStore : ICharacterCreationSessionStore
{
    private readonly ConcurrentDictionary<Guid, CharacterCreationSession> _sessions = new();

    /// <inheritdoc />
    public Task<CharacterCreationSession> CreateSessionAsync()
    {
        var session = new CharacterCreationSession();
        _sessions[session.SessionId] = session;
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task<CharacterCreationSession?> GetSessionAsync(Guid sessionId)
    {
        _sessions.TryGetValue(sessionId, out var session);
        return Task.FromResult(session);
    }

    /// <inheritdoc />
    public Task UpdateSessionAsync(CharacterCreationSession session)
    {
        session.LastUpdatedAt = DateTime.UtcNow;
        _sessions[session.SessionId] = session;
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task RemoveSessionAsync(Guid sessionId)
    {
        _sessions.TryRemove(sessionId, out _);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Removes all sessions whose <see cref="CharacterCreationSession.LastUpdatedAt"/> is older than
    /// <paramref name="maxIdle"/>. Called periodically by the server's cleanup background service.
    /// </summary>
    /// <param name="maxIdle">Maximum allowed idle time before a session is evicted.</param>
    /// <returns>The number of sessions that were removed.</returns>
    public int EvictExpiredSessions(TimeSpan maxIdle)
    {
        var cutoff = DateTime.UtcNow - maxIdle;
        var expired = _sessions
            .Where(kv => kv.Value.LastUpdatedAt < cutoff)
            .Select(kv => kv.Key)
            .ToList();

        foreach (var id in expired)
            _sessions.TryRemove(id, out _);

        return expired.Count;
    }
}
