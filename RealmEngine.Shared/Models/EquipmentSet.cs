namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a set of equipment that provides bonuses when multiple pieces are worn.
/// </summary>
public class EquipmentSet
{
    /// <summary>Gets or sets the unique identifier.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Item names that belong to this set.
    /// </summary>
    public List<string> SetItemNames { get; set; } = new();

    /// <summary>
    /// Set bonuses indexed by number of pieces equipped.
    /// Example: { 2 => "+10 Defense", 4 => "+20% Fire Resistance", 6 => "+50 Strength" }
    /// </summary>
    public Dictionary<int, SetBonus> Bonuses { get; set; } = new();
}

/// <summary>
/// Represents a bonus granted when wearing a certain number of set pieces.
/// </summary>
public class SetBonus
{
    /// <summary>Gets or sets the number of pieces required.</summary>
    public int PiecesRequired { get; set; }

    /// <summary>Gets or sets the description.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the bonus strength.</summary>
    public int BonusStrength { get; set; } = 0;

    /// <summary>Gets or sets the bonus dexterity.</summary>
    public int BonusDexterity { get; set; } = 0;

    /// <summary>Gets or sets the bonus constitution.</summary>
    public int BonusConstitution { get; set; } = 0;

    /// <summary>Gets or sets the bonus intelligence.</summary>
    public int BonusIntelligence { get; set; } = 0;

    /// <summary>Gets or sets the bonus wisdom.</summary>
    public int BonusWisdom { get; set; } = 0;

    /// <summary>Gets or sets the bonus charisma.</summary>
    public int BonusCharisma { get; set; } = 0;

    /// <summary>Gets or sets the special effect.</summary>
    public string? SpecialEffect { get; set; }
}
