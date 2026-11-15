using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>
/// Persistence for items lying on the ground in a zone location.
/// </summary>
public interface IGroundItemRepository
{
    /// <summary>Lists the ground items at a zone location within one scope, oldest first.</summary>
    /// <param name="zoneId">Zone identifier.</param>
    /// <param name="locationSlug">Zone-location slug.</param>
    /// <param name="zoneInstanceId">
    /// The instance run to read, or <see langword="null"/> to read the shared zone. The scope is required
    /// rather than defaulted so a caller cannot accidentally show one run's loot to another.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ground items at that location in that scope.</returns>
    Task<IReadOnlyList<GroundItem>> GetAtLocationAsync(
        string zoneId,
        string locationSlug,
        Guid? zoneInstanceId,
        CancellationToken cancellationToken = default);

    /// <summary>Finds a ground item by id.</summary>
    /// <param name="id">Ground-item identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ground item, or <see langword="null"/> when it no longer exists.</returns>
    Task<GroundItem?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Adds a ground item.</summary>
    /// <param name="item">Item to add.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the item is persisted.</returns>
    Task AddAsync(GroundItem item, CancellationToken cancellationToken = default);

    /// <summary>Removes a ground item.</summary>
    /// <param name="item">Item to remove.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A task that completes when the item is deleted.</returns>
    Task RemoveAsync(GroundItem item, CancellationToken cancellationToken = default);
}
