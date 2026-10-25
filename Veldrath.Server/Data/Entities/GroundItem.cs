namespace Veldrath.Server.Data.Entities;

/// <summary>
/// An item stack lying on the ground at a specific zone location, dropped by a character.
/// </summary>
/// <remarks>
/// Ground items are keyed by <see cref="ZoneId"/> plus <see cref="LocationSlug"/> so a pickup can be
/// validated against the character's current position. Unlike a discard, a dropped item survives
/// until somebody picks it up.
/// </remarks>
public class GroundItem
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Gets or sets the zone the item is lying in.</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>Gets or sets the zone-location slug the item is lying at.</summary>
    public string LocationSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the item-reference slug (e.g. <c>"iron_sword"</c>).</summary>
    public string ItemRef { get; set; } = string.Empty;

    /// <summary>Gets or sets the number of units on the ground.</summary>
    public int Quantity { get; set; } = 1;

    /// <summary>Gets or sets the durability carried over from the bag entry, if any.</summary>
    public int? Durability { get; set; }

    /// <summary>Gets or sets the character that dropped the item, if still known.</summary>
    public Guid? DroppedByCharacterId { get; set; }

    /// <summary>Gets or sets when the item was dropped (UTC).</summary>
    public DateTimeOffset DroppedAt { get; set; } = DateTimeOffset.UtcNow;
}
