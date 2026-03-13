namespace RealmEngine.Data.Entities;

/// <summary>
/// Central routing table for polymorphic cross-domain reference resolution.
/// Every content entity row registers itself here on upsert.
/// Resolving @domain/type_key:slug → SELECT entity_id, table_name FROM content_registry
/// then query the correct typed table by entity_id.
/// </summary>
public class ContentRegistry
{
    /// <summary>PK of the entity row in its own typed table.</summary>
    public Guid EntityId { get; set; }

    /// <summary>Name of the typed table — e.g. "Enemies", "Weapons", "Abilities".</summary>
    public string TableName { get; set; } = string.Empty;

    /// <summary>Domain path — e.g. "enemies", "items/weapons", "abilities/active".</summary>
    public string Domain { get; set; } = string.Empty;

    /// <summary>Subcategory within the domain — matches the entity's TypeKey column.</summary>
    public string TypeKey { get; set; } = string.Empty;

    /// <summary>Slug of the entity — matches the entity's Slug column.</summary>
    public string Slug { get; set; } = string.Empty;
}
