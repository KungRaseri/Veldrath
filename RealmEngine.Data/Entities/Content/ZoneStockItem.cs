namespace RealmEngine.Data.Entities;

/// <summary>
/// One item a zone's merchant network offers for sale.
/// </summary>
/// <remarks>
/// <para>
/// The shop is per zone, not per person: a town's merchants draw on one list
/// (<c>docs/design/content/towns.md</c> §C6), so the row is keyed by <see cref="ZoneId"/> and the item, never by
/// an NPC. Two merchant locations in one town therefore offer the same goods without either of them authoring a
/// copy.
/// </para>
/// <para>
/// The list is what the shop <em>offers</em>; it is not a stock level. Nothing decrements these rows on a
/// purchase — availability, not inventory depth, is the model this table establishes. A row is content, so it
/// lives in <c>ContentDbContext</c> beside the items it names, not in the gameplay database.
/// </para>
/// </remarks>
public class ZoneStockItem
{
    /// <summary>Gets or sets the primary key. Generated once on insert and never reused.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the zone whose merchants offer this item — a soft reference to a gameplay Zone id.</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>Gets or sets the slug of the <see cref="Item"/> on the shelf — a soft reference, no FK.</summary>
    public string ItemSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the order the item is listed in, so the shop reads in an authored order.</summary>
    public int SortOrder { get; set; }
}
