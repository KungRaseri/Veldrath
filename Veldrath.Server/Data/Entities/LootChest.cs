namespace Veldrath.Server.Data.Entities;

/// <summary>
/// A chest a character can open at a zone location, with the loot it still owes.
/// </summary>
/// <remarks>
/// <para>
/// Two kinds share the table, discriminated by <see cref="Kind"/>. An <see cref="LootChestKind.Authored"/>
/// chest is seeded exactly where a quest or a boss room names it, so a quest that says "the chest in the Old
/// Clearing" never points at a wandering target. A <see cref="LootChestKind.Roaming"/> chest is placed by
/// <c>ChestService</c> from the zone's locations, so the world does not hold its treasure in the same spot
/// forever.
/// </para>
/// <para>
/// Opened rows are kept rather than deleted: <see cref="OpenedAt"/> is the roam clock ("when was the last chest
/// opened in this zone"), it is the audit record of who looted what where, and a reopen only has to clear
/// <see cref="IsOpened"/> on the row that is still there.
/// </para>
/// </remarks>
public class LootChest
{
    /// <summary>Gets or sets the primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>
    /// Gets or sets the stable key of the chest. Authored chests carry their seeded slug so a restart finds the
    /// same row; roaming chests carry a drawn id so two spawns in the same location never collide.
    /// </summary>
    public string ChestId { get; set; } = string.Empty;

    /// <summary>Gets or sets whether the chest is seeded content or placed by the sweeper.</summary>
    public LootChestKind Kind { get; set; } = LootChestKind.Authored;

    /// <summary>Gets or sets the zone the chest stands in.</summary>
    public string ZoneId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the instance run the chest belongs to, or <see langword="null"/> in the shared zone. A delve's
    /// chest is per run: purging the run takes its chests with it, the way it takes its floor loot.
    /// </summary>
    public Guid? ZoneInstanceId { get; set; }

    /// <summary>Gets or sets the zone-location slug the chest stands at.</summary>
    public string LocationSlug { get; set; } = string.Empty;

    /// <summary>Gets or sets the name shown in the client.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the loot table references to merge, as a JSON array of references in the engine's form
    /// (<c>@loot-tables/chests/forest:forest-chest-loot</c>).
    /// </summary>
    public string LootTableRefs { get; set; } = "[]";

    /// <summary>Gets or sets the merge strategy passed to the roll (<c>addPools</c>, <c>prioritizeFirst</c>, <c>prioritizeLast</c>).</summary>
    public string MergeStrategy { get; set; } = "addPools";

    /// <summary>Gets or sets a value indicating whether the chest has been emptied and is waiting to reopen.</summary>
    public bool IsOpened { get; set; }

    /// <summary>Gets or sets when the chest last appeared (UTC).</summary>
    public DateTimeOffset SpawnedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Gets or sets when the chest was last opened (UTC), or <see langword="null"/> while it is unopened.</summary>
    public DateTimeOffset? OpenedAt { get; set; }

    /// <summary>
    /// Gets or sets when an opened chest comes back (UTC), or <see langword="null"/> while it is unopened. The
    /// sweeper reopens only rows whose timer has elapsed, so a restart mid-window simply continues the countdown.
    /// </summary>
    public DateTimeOffset? ReopenAt { get; set; }

    /// <summary>Gets or sets the character who opened it last, when still known.</summary>
    public Guid? OpenedByCharacterId { get; set; }
}

/// <summary>How a <see cref="LootChest"/> came to be where it is.</summary>
public enum LootChestKind
{
    /// <summary>Seeded content, always at the same location, because a quest or a room names it.</summary>
    Authored = 0,

    /// <summary>Placed by the chest sweeper from the zone's locations, so treasure moves around.</summary>
    Roaming = 1,
}
