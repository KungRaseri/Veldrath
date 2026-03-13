namespace RealmEngine.Data.Entities;

/// <summary>
/// A single name pattern template within a NamePatternSet.
/// Template uses {token} syntax referencing component keys in the same set
/// — e.g. "{prefix} {base}", "{base} the {suffix}".
/// </summary>
public class NamePattern
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>FK to the pattern set this template belongs to.</summary>
    public Guid SetId { get; set; }

    /// <summary>Pattern template — uses {token} syntax referencing component keys in the same set.</summary>
    public string Template { get; set; } = string.Empty;

    /// <summary>Relative selection weight — higher = more likely to be chosen.</summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>Navigation property for the owning pattern set.</summary>
    public NamePatternSet? Set { get; set; }
}
