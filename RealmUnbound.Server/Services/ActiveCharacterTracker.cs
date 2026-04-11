using System.Collections.Concurrent;

namespace Veldrath.Server.Services;

/// <summary>
/// In-memory registry that maps each active character to the SignalR connection that claims it.
/// Registered as a singleton so all hub instances share the same state.
/// </summary>
public interface IActiveCharacterTracker
{
    /// <summary>
    /// Atomically claims a character for a connection.
    /// Returns <c>true</c> if the claim succeeded (character was not already claimed by another connection).
    /// Returns <c>false</c> if the character is already claimed by a different connection.
    /// Calling this a second time with the same connectionId is idempotent (returns true).
    /// </summary>
    bool TryClaim(Guid characterId, string connectionId);

    /// <summary>Releases the character (if any) currently held by <paramref name="connectionId"/>.</summary>
    void Release(string connectionId);

    /// <returns><c>true</c> if any connection has claimed this character.</returns>
    bool IsActive(Guid characterId);

    /// <returns>A snapshot of all currently active character IDs.</returns>
    IReadOnlySet<Guid> GetActiveCharacterIds();

    /// <returns>
    /// The character ID claimed by <paramref name="connectionId"/>, or <c>null</c> if none.
    /// </returns>
    Guid? GetCharacterForConnection(string connectionId);
}

public class ActiveCharacterTracker : IActiveCharacterTracker
{
    private readonly ConcurrentDictionary<Guid, string> _characterToConnection = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToCharacter = new();

    public bool TryClaim(Guid characterId, string connectionId)
    {
        // Allow re-claiming by the same connection (idempotent reconnect)
        if (_characterToConnection.TryGetValue(characterId, out var existing) && existing == connectionId)
            return true;

        // Attempt to atomically add the entry — fails if already held by another connection
        if (!_characterToConnection.TryAdd(characterId, connectionId))
            return false;

        _connectionToCharacter[connectionId] = characterId;
        return true;
    }

    public void Release(string connectionId)
    {
        if (_connectionToCharacter.TryRemove(connectionId, out var characterId))
            _characterToConnection.TryRemove(characterId, out _);
    }

    public bool IsActive(Guid characterId) =>
        _characterToConnection.ContainsKey(characterId);

    public IReadOnlySet<Guid> GetActiveCharacterIds() =>
        new HashSet<Guid>(_characterToConnection.Keys);

    public Guid? GetCharacterForConnection(string connectionId) =>
        _connectionToCharacter.TryGetValue(connectionId, out var id) ? id : null;
}
