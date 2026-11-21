using Microsoft.EntityFrameworkCore;
using RealmEngine.Data.Persistence;

namespace Veldrath.Server.Features.Shop;

/// <summary>
/// Reads the items a zone's merchants offer.
/// </summary>
/// <remarks>
/// The list is authored content (<c>ZoneStockItem</c> rows), shared by every merchant in the zone. A shop reads it
/// before anything else about an item: an item the zone does not stock is not for sale here even if the catalog holds
/// it and the character can pay for it.
/// </remarks>
public interface IZoneStockReader
{
    /// <summary>Reads the item slugs a zone's merchants offer, in authored order.</summary>
    /// <param name="zoneId">The zone whose stock is being read.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The offered item slugs; empty when the zone authorises no stock.</returns>
    Task<IReadOnlyList<string>> GetItemSlugsAsync(string zoneId, CancellationToken cancellationToken);
}

/// <summary>
/// Reads zone stock from the content catalog.
/// </summary>
/// <param name="content">The content database that owns the authored stock rows.</param>
public sealed class ZoneStockReader(ContentDbContext content) : IZoneStockReader
{
    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetItemSlugsAsync(string zoneId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(zoneId))
            return [];

        return await content.ZoneStockItems
            .AsNoTracking()
            .Where(row => row.ZoneId == zoneId)
            .OrderBy(row => row.SortOrder)
            .ThenBy(row => row.ItemSlug)
            .Select(row => row.ItemSlug)
            .ToListAsync(cancellationToken);
    }
}
