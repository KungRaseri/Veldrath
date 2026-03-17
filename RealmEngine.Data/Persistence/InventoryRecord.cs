namespace RealmEngine.Data.Persistence;

/// <summary>
/// Persisted inventory slot: a quantity of one item ref owned by a named character in a save game.
/// Stored in <see cref="GameDbContext"/> so inventory operations do not require
/// deserialising the full save game JSON on every add/remove.
/// </summary>
public class InventoryRecord
{
    /// <summary>Surrogate primary key.</summary>
    public int Id { get; set; }

    /// <summary>Save game the inventory belongs to.</summary>
    public string SaveGameId { get; set; } = string.Empty;

    /// <summary>Character name within the save (denormalised for fast lookups).</summary>
    public string CharacterName { get; set; } = string.Empty;

    /// <summary>Item reference slug (e.g. "@items/materials/ore:copper-ore").</summary>
    public string ItemRef { get; set; } = string.Empty;

    /// <summary>Stack size.</summary>
    public int Quantity { get; set; }

    /// <summary>Last write timestamp (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
