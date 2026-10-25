using System.Collections.Concurrent;

namespace Veldrath.Server.Services;

/// <summary>
/// Tracks which characters are currently away, and the optional message they left behind.
/// </summary>
/// <remarks>
/// This state is <em>ephemeral by design</em>: it exists only while a player is away and is cleared the moment they
/// act or reconnect. It is never persisted, and losing it on restart costs nothing — the documented exception to the
/// "Postgres is the source of truth" rule that the rest of Milestone D enforces.
/// </remarks>
public interface IAfkTracker
{
    /// <summary>Marks a character as away, optionally with a message their correspondents will see.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="message">Optional AFK message.</param>
    void Set(Guid characterId, string? message);

    /// <summary>Clears the away flag, reporting whether it was set.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="message">The message that was set, if any.</param>
    /// <returns><see langword="true"/> when the character was away.</returns>
    bool TryClear(Guid characterId, out string? message);

    /// <summary>Returns whether the character is away, and the message they left.</summary>
    /// <param name="characterId">Character id.</param>
    /// <param name="message">The message that was set, if any.</param>
    /// <returns><see langword="true"/> when the character is away.</returns>
    bool TryGet(Guid characterId, out string? message);
}

/// <summary>
/// Thread-safe in-memory <see cref="IAfkTracker"/>. Registered as a singleton so every hub invocation shares it.
/// </summary>
public sealed class AfkTracker : IAfkTracker
{
    private readonly ConcurrentDictionary<Guid, string?> _away = new();

    /// <inheritdoc />
    public void Set(Guid characterId, string? message) => _away[characterId] = message;

    /// <inheritdoc />
    public bool TryClear(Guid characterId, out string? message) => _away.TryRemove(characterId, out message);

    /// <inheritdoc />
    public bool TryGet(Guid characterId, out string? message) => _away.TryGetValue(characterId, out message);
}
