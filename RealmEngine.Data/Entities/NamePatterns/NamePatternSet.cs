namespace RealmEngine.Data.Entities;

/// <summary>
/// Name-generation pattern set for one entity path.
/// EntityPath is the unique key — e.g. "enemies/wolves", "items/weapons", "npcs/merchants".
/// </summary>
public class NamePatternSet
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Entity path this set names — e.g. "enemies/wolves". Unique across all sets.</summary>
    public string EntityPath { get; set; } = string.Empty;

    /// <summary>Optional human-readable label shown in RealmForge.</summary>
    public string? DisplayName { get; set; }
    /// <summary>True if patterns in this set support trait-conditional substitution.</summary>
    public bool SupportsTraits { get; set; }
    /// <summary>Incremented by the import pipeline on each upsert.</summary>
    public int Version { get; set; } = 1;
    /// <summary>Timestamp of the last import pipeline upsert (UTC).</summary>
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Pattern templates in this set.</summary>
    public ICollection<NamePattern> Patterns { get; set; } = [];
    /// <summary>Component word pools referenced by pattern templates in this set.</summary>
    public ICollection<NameComponent> Components { get; set; } = [];
}
