namespace RealmEngine.Data.Entities;

/// <summary>
/// One word or token in a component pool for a NamePatternSet.
/// ComponentKey matches the {token} in pattern templates — e.g. "prefix", "base", "suffix".
/// </summary>
public class NameComponent
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary>FK to the pattern set this component belongs to.</summary>
    public Guid SetId { get; set; }

    /// <summary>Token name referenced in pattern templates — e.g. "prefix", "base".</summary>
    public string ComponentKey { get; set; } = string.Empty;

    /// <summary>The actual word or phrase — e.g. "Dark", "Shadow", "Frost".</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Relative selection weight — higher = more likely to be chosen. Defaults to 50.</summary>
    public int RarityWeight { get; set; } = 50;

    /// <summary>
    /// Optional budget modifier used by quality components (e.g. "Masterwork" = 0.3 extra budget).
    /// Zero for all non-quality components.
    /// </summary>
    public double BudgetModifier { get; set; } = 0.0;

    /// <summary>Display order within the component pool in RealmForge.</summary>
    public int SortOrder { get; set; }

    /// <summary>Navigation property for the owning pattern set.</summary>
    public NamePatternSet? Set { get; set; }
}
