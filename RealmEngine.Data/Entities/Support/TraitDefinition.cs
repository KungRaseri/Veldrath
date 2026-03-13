namespace RealmEngine.Data.Entities;

/// <summary>
/// Vocabulary table defining every known trait key across all entity types.
/// Does NOT store values — values live in each entity's Traits JSONB owned type.
/// Adding a new trait = one INSERT here + add a nullable property to the relevant C# owned class.
/// No migration required for the entity table.
/// </summary>
public class TraitDefinition
{
    /// <summary>The trait key used in entity Traits columns — e.g. "aggressive", "fireResist".</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>"bool" | "int" | "float" | "string"</summary>
    public string ValueType { get; set; } = "bool";

    /// <summary>Human-readable description of what the trait controls.</summary>
    public string? Description { get; set; }

    /// <summary>Comma-separated entity types this trait applies to — e.g. "enemies,weapons" or "*".</summary>
    public string? AppliesTo { get; set; }
}
