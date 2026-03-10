using RealmUnbound.Server.Data.Entities;

namespace RealmUnbound.Server.Data.Repositories;

/// <summary>Persistence contract for <see cref="Character"/>.</summary>
public interface ICharacterRepository
{
    /// <summary>Returns all non-deleted characters for an account, ordered by LastPlayedAt desc.</summary>
    Task<IReadOnlyList<Character>> GetByAccountIdAsync(Guid accountId, CancellationToken ct = default);

    Task<Character?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>Returns the most recently played non-deleted character for an account.</summary>
    Task<Character?> GetLastPlayedAsync(Guid accountId, CancellationToken ct = default);

    /// <summary>Returns true if a character with this name exists anywhere on the server (for global uniqueness).</summary>
    Task<bool> NameExistsAsync(string name, CancellationToken ct = default);

    /// <summary>Returns the number of active (non-deleted) characters for an account.</summary>
    Task<int> GetActiveCountAsync(Guid accountId, CancellationToken ct = default);

    Task<Character> CreateAsync(Character character, CancellationToken ct = default);
    Task UpdateAsync(Character character, CancellationToken ct = default);

    /// <summary>Sets <see cref="Character.DeletedAt"/> to now (soft delete).</summary>
    Task SoftDeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Updates <see cref="Character.CurrentZoneId"/> without loading the full entity.</summary>
    Task UpdateCurrentZoneAsync(Guid id, string zoneId, CancellationToken ct = default);
}
