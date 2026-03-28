namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Persistence contract for <see cref="Entities.CharacterUnlockedConnection"/>.</summary>
public interface ICharacterUnlockedConnectionRepository
{
    /// <summary>Returns the set of connection IDs the character has unlocked.</summary>
    Task<HashSet<int>> GetUnlockedIdsAsync(Guid characterId, CancellationToken ct = default);

    /// <summary>Returns true if the character has already unlocked the given connection.</summary>
    Task<bool> IsUnlockedAsync(Guid characterId, int connectionId, CancellationToken ct = default);

    /// <summary>Persists a new unlock for the character. Silently ignores duplicate unlocks.</summary>
    Task AddUnlockAsync(Guid characterId, int connectionId, string unlockSource, CancellationToken ct = default);
}
