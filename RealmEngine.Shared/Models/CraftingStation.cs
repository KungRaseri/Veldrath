namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents a crafting station where recipes can be crafted.
/// </summary>
public class CraftingStation
{
    /// <summary>Gets or sets the unique station identifier.</summary>
    public required string Id { get; set; }

    /// <summary>Gets or sets the display name of the station.</summary>
    public required string Name { get; set; }

    /// <summary>Gets or sets the descriptive text for the station.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Gets or sets the slug for URL-friendly identification.</summary>
    public string Slug { get; set; } = string.Empty;

    /// <summary>Gets or sets the current tier level of this station (1-3).</summary>
    public int Tier { get; set; } = 1;

    /// <summary>Gets or sets the recipe categories this station can craft (e.g., ["weapons", "armor"]).</summary>
    public List<string> Categories { get; set; } = new();

    /// <summary>Gets or sets the JSON v4.1 reference to locations where this station is available.</summary>
    public string? AvailableAt { get; set; }

    /// <summary>Gets or sets the minimum character level required to use this station.</summary>
    public int RequiredLevel { get; set; }

    /// <summary>Gets or sets the upgrade requirements for each tier.</summary>
    public Dictionary<int, StationUpgrade> UpgradeRequirements { get; set; } = new();
}

/// <summary>
/// Represents the requirements to upgrade a crafting station to a higher tier.
/// </summary>
public class StationUpgrade
{
    /// <summary>Gets or sets the gold cost to upgrade to this tier.</summary>
    public int GoldCost { get; set; }

    /// <summary>Gets or sets the materials required to upgrade to this tier.</summary>
    public List<RecipeMaterial> Materials { get; set; } = new();
}
