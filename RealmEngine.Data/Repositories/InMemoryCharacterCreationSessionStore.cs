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
}
