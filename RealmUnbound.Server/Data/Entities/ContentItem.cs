namespace RealmUnbound.Server.Data.Entities;

/// <summary>
/// Unified storage for all game content — catalog items, name-generation patterns,
/// and system configuration — in a single table.
///
/// Domain / TypeKey / Slug form the unique content address and mirror the
/// @reference syntax used across game data:  @&lt;Domain&gt;/&lt;TypeKey&gt;:&lt;Slug&gt;
///
/// Domain namespaces:
///   Catalog content  — "enemies", "items/weapons", "abilities/active", etc.
///                       Each item in a catalog.json becomes one row.
///   Name patterns    — "name-patterns"
///                       One row per entity category; TypeKey = entity path
///                       (e.g. "enemies/wolves"), Slug = "default".
///   Config           — "config"
///                       One row per config file; TypeKey = config key
///                       (e.g. "experience", "rarity"), Slug = "default".
/// </summary>
public class ContentItem
{
    public Guid Id { get; set; }

    /// <summary>
    /// Content category path. Examples: "enemies", "items/weapons/swords",
    /// "name-patterns", "config". Matches the domain portion of the @reference syntax.
    /// </summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>
    /// Subcategory within the domain. For catalog items this is the type key
    /// (e.g. "wolves"). For name-patterns it is the entity path being named
    /// (e.g. "enemies/wolves"). For config it is the config key (e.g. "experience").
    /// </summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>
    /// Unique item identifier within Domain + TypeKey. For catalog items this is
    /// the item slug (e.g. "grey-wolf"). For name-patterns and config rows this is
    /// "default".
    /// </summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Human-readable display name. Used by RealmForge and log output.</summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Selection weight for random catalog draws. Higher = more common.
    /// Not meaningful for name-pattern or config rows (stored as 1).
    /// </summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>Inactive items are excluded from generation and reference resolution.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Incremented by the import pipeline on each upsert.</summary>
    public int Version { get; set; } = 1;

    /// <summary>
    /// Full item payload stored as JSONB. Shape depends on Domain:
    ///   Catalog items  — { attributes: {}, stats: {}, traits: {}, properties: {} }
    ///   name-patterns  — { version, supportsTraits, patterns: [...], components: {} }
    ///   config         — domain-specific configuration structure
    /// </summary>
    public string Data { get; set; } = "{}";

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
