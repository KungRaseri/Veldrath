namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Persistence contract for <see cref="Entities.CharacterUnlockedLocation"/>.</summary>
public interface ICharacterUnlockedLocationRepository
{
    /// <summary>Returns all location slugs the character has unlocked.</summary>
    Task<HashSet<string>> GetUnlockedSlugsAsync(Guid characterId, CancellationToken ct = default);

    /// <summary>Returns true if the character has already unlocked the given location.</summary>
    Task<bool> IsUnlockedAsync(Guid characterId, string locationSlug, CancellationToken ct = default);

    /// <summary>Persists a new unlock for the character. Silently ignores duplicate unlocks.</summary>
    Task AddUnlockAsync(Guid characterId, string locationSlug, string unlockSource, CancellationToken ct = default);
}
