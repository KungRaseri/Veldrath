namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a single naming component (prefix or suffix) with its token identifier.
/// Used to preserve both the semantic meaning (token) and display value of name parts.
/// </summary>
/// <remarks>
/// Examples:
/// - Token: "size", Value: "Giant"
/// - Token: "element_prefix", Value: "Flaming"
/// - Token: "material", Value: "Mithril"
/// </remarks>
public class NameComponent
{
    /// <summary>
    /// The token name from the pattern (e.g., "size", "type", "material", "element_prefix").
    /// This identifies the semantic category of this component.
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// The actual value selected from components (e.g., "Giant", "Frost", "Mithril").
    /// This is the text that appears in the composed name.
    /// </summary>
    public string Value { get; set; } = string.Empty;
}
