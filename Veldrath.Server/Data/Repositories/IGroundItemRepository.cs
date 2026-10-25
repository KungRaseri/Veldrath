using Veldrath.Server.Data.Entities;

namespace Veldrath.Server.Data.Repositories;

/// <summary>
/// Persistence for items lying on the ground in a zone location.
/// </summary>
public interface IGroundItemRepository
{
    /// <summary>Lists the ground items at a zone location, oldest first.</summary>
    /// <param name="zoneId">Zone identifier.</param>
    /// <param name="locationSlug">Zone-location slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The ground items at that location.</returns>
    Task<IReadOnlyList<GroundItem>> GetAtLocationAsync(
        string zoneId,
        string locationSlug,
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
