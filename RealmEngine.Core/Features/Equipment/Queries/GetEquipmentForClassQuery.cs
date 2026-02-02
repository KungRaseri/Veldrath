using MediatR;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Equipment.Queries;

/// <summary>
/// Query to get equipment (weapons and armor) that a class can use based on proficiencies.
/// </summary>
public class GetEquipmentForClassQuery : IRequest<GetEquipmentForClassResult>
{
    /// <summary>
    /// Gets or sets the class ID (e.g., "cleric:priest", "warrior:fighter").
    /// </summary>
    public string ClassId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the equipment type to filter by.
    /// Examples: "weapons", "armor", "shields"
    /// If null, returns both weapons and armor.
    /// </summary>
    public string? EquipmentType { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of items to return per category.
    /// Default is 10. Use 0 for all items.
    /// </summary>
    public int MaxItemsPerCategory { get; set; } = 10;

    /// <summary>
    /// Gets or sets whether to randomize the selection within proficiency constraints.
    /// Useful for character creation to provide variety.
    /// </summary>
    public bool RandomizeSelection { get; set; } = false;
}

/// <summary>
/// Result containing equipment filtered by class proficiencies.
/// </summary>
public class GetEquipmentForClassResult
{
    /// <summary>
    /// Gets or sets whether the query was successful.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Gets or sets error message if query failed.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Gets or sets the class name for reference.
    /// </summary>
    public string? ClassName { get; set; }

    /// <summary>
    /// Gets or sets the armor proficiencies for this class.
    /// </summary>
    public List<string> ArmorProficiencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the weapon proficiencies for this class.
    /// </summary>
    public List<string> WeaponProficiencies { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of weapons this class can use.
    /// </summary>
    public List<Item> Weapons { get; set; } = new();

    /// <summary>
    /// Gets or sets the list of armor this class can wear.
    /// </summary>
    public List<Item> Armor { get; set; } = new();
}
