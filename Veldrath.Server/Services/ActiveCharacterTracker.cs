using System.Collections.Concurrent;
using Microsoft.Extensions.Options;
using Veldrath.Server.Settings;

namespace Veldrath.Server.Services;

/// <summary>
/// In-memory registry that maps each active character to the SignalR connection that claims it.
/// Registered as a singleton so all hub instances share the same state.
/// Supports a 30-second grace period for unexpected disconnects (e.g. page refresh),
/// allowing the same account to reclaim the character on a new circuit without a race condition.
/// </summary>
public interface IActiveCharacterTracker
{
    /// <summary>
    /// Atomically claims a character for a connection.
    /// Returns <c>true</c> if the claim succeeded (character was not already claimed by another connection,
    /// or the existing claim is stale — its grace period has expired).
    /// Returns <c>false</c> if the character is already claimed by a different connection
    /// and the claim is still active or within its grace period.
    /// Calling this a second time with the same connectionId is idempotent (returns true).
    /// </summary>
    bool TryClaim(Guid characterId, string connectionId);

    /// <summary>Releases the character (if any) currently held by <paramref name="connectionId"/>.</summary>
    void Release(string connectionId);

    /// <returns><c>true</c> if any connection has claimed this character and the claim is active (not disconnecting).</returns>
    bool IsActive(Guid characterId);

    /// <returns>A snapshot of all currently active character IDs (excluding those in grace period).</returns>
    IReadOnlySet<Guid> GetActiveCharacterIds();

    /// <returns>
    /// The character ID claimed by <paramref name="connectionId"/>, or <c>null</c> if none.
    /// </returns>
    Guid? GetCharacterForConnection(string connectionId);

    /// <summary>
    /// Marks a character claim as "disconnecting" with the current timestamp.
    /// After the 30-second grace period, the claim is considered stale and can be
    /// forcibly taken by a new connection.
    /// </summary>
    /// <param name="characterId">The character whose claim to mark as disconnecting.</param>
    void MarkDisconnecting(Guid characterId);
}

/// <summary>
/// In-memory implementation of <see cref="IActiveCharacterTracker"/>.
/// Uses <see cref="ConcurrentDictionary{TKey,TValue}"/> for thread-safe claim tracking
/// with a configurable grace period (default 30 seconds) for unexpected disconnects.
/// </summary>
public class ActiveCharacterTracker : IActiveCharacterTracker
{
    private readonly ConcurrentDictionary<Guid, ClaimEntry> _characterToClaim = new();
    private readonly ConcurrentDictionary<string, Guid> _connectionToCharacter = new();
    private readonly TimeSpan _gracePeriod;

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveCharacterTracker"/> class
    /// with default settings (30-second grace period). For use in tests where DI is not available.
    /// </summary>
    public ActiveCharacterTracker()
    {
        _gracePeriod = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ActiveCharacterTracker"/> class.
    /// </summary>
    /// <param name="options">The active character tracker options.</param>
    public ActiveCharacterTracker(IOptions<ActiveCharacterTrackerOptions> options)
    {
        var seconds = options.Value.GracePeriodSeconds;
        if (seconds < 1)
            seconds = 30;
        _gracePeriod = TimeSpan.FromSeconds(seconds);
    }

    /// <summary>Internal tracking entry for a single character claim.</summary>
    private sealed class ClaimEntry
    {
        /// <summary>The SignalR connection ID that holds the claim.</summary>
        public string ConnectionId { get; set; } = string.Empty;

        /// <summary>UTC time when the claim was first established or refreshed.</summary>
        public DateTimeOffset ClaimedAt { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// UTC time when the connection was marked as disconnecting.
        /// <see langword="null"/> means the connection is still active.
        /// </summary>
        public DateTimeOffset? DisconnectedAt { get; set; }
    }

    /// <inheritdoc/>
    public bool TryClaim(Guid characterId, string connectionId)
    {
        // Allow re-claiming by the same connection (idempotent reconnect).
        if (_characterToClaim.TryGetValue(characterId, out var existing) && existing.ConnectionId == connectionId)
        {
            existing.DisconnectedAt = null; // clear any pending disconnect marker
            return true;
        }

        // If claimed by a different connection, check if the claim is stale.
        if (_characterToClaim.TryGetValue(characterId, out var other))
        {
            // Still actively connected — deny.
            if (other.DisconnectedAt is null)
                return false;

            // Within grace period — deny.
            var age = DateTimeOffset.UtcNow - other.DisconnectedAt.Value;
            if (age < _gracePeriod)
                return false;

            // Claim is stale (grace period expired) — forcibly release the old connection.
            _characterToClaim.TryRemove(characterId, out _);
            _connectionToCharacter.TryRemove(other.ConnectionId, out _);
        }

        // Fresh claim.
        _characterToClaim[characterId] = new ClaimEntry
        {
            ConnectionId = connectionId,
            ClaimedAt = DateTimeOffset.UtcNow,
        };
        _connectionToCharacter[connectionId] = characterId;
        return true;
    }

    /// <inheritdoc/>
    public void MarkDisconnecting(Guid characterId)
    {
        if (_characterToClaim.TryGetValue(characterId, out var entry))
            entry.DisconnectedAt = DateTimeOffset.UtcNow;
    }

    /// <inheritdoc/>
    public void Release(string connectionId)
    {
        if (_connectionToCharacter.TryRemove(connectionId, out var characterId))
            _characterToClaim.TryRemove(characterId, out _);
    }

    /// <inheritdoc/>
    public bool IsActive(Guid characterId) =>
        _characterToClaim.TryGetValue(characterId, out var entry)
            && entry.DisconnectedAt is null;

    /// <inheritdoc/>
    public IReadOnlySet<Guid> GetActiveCharacterIds() =>
        new HashSet<Guid>(_characterToClaim
            .Where(kvp => kvp.Value.DisconnectedAt is null)
            .Select(kvp => kvp.Key));

    /// <inheritdoc/>
    public Guid? GetCharacterForConnection(string connectionId) =>
        _connectionToCharacter.TryGetValue(connectionId, out var id) ? id : null;
}
