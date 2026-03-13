namespace RealmEngine.Data.Entities;

/// <summary>
/// Shared base properties for all typed game content entities.
/// Each subclass maps to its own table — there is no shared base table.
/// </summary>
public abstract class ContentBase
{
    /// <summary>Primary key — stable across imports; generated once on first insert.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>URL-safe, lowercase, hyphenated identifier unique within TypeKey.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Subcategory within the domain — e.g. "wolves", "active/offensive", "swords".</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>Human-readable label for RealmForge and log output.</summary>
    public string? DisplayName { get; set; }

    /// <summary>Selection weight for random draws — higher = more common (range 1–100).</summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>False = excluded from generation and reference resolution.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>Incremented by the import pipeline on each upsert.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Timestamp of the last import pipeline upsert (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
