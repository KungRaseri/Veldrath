using RealmEngine.Shared.Models;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Stores in-progress character creation sessions keyed by <see cref="CharacterCreationSession.SessionId"/>.
/// </summary>
public interface ICharacterCreationSessionStore
{
    /// <summary>Creates a new empty session and returns it.</summary>
    /// <returns>The newly created <see cref="CharacterCreationSession"/>.</returns>
    Task<CharacterCreationSession> CreateSessionAsync();

    /// <summary>Retrieves a session by its identifier, or returns <see langword="null"/> if not found.</summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <returns>The session, or <see langword="null"/>.</returns>
    Task<CharacterCreationSession?> GetSessionAsync(Guid sessionId);

    /// <summary>Persists changes to an existing session.</summary>
    /// <param name="session">The session to update.</param>
    Task UpdateSessionAsync(CharacterCreationSession session);

    /// <summary>Removes a session from the store.</summary>
    /// <param name="sessionId">The session identifier to remove.</param>
    Task RemoveSessionAsync(Guid sessionId);
}
