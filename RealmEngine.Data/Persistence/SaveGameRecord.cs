namespace RealmEngine.Data.Persistence;

/// <summary>
/// Thin EF Core entity that wraps a serialised <see cref="RealmEngine.Shared.Models.SaveGame"/>.
/// Storing the rich object graph as a JSON blob avoids complex relational mapping
/// while still allowing indexed lookups on the common query fields.
/// </summary>
public class SaveGameRecord
{
    /// <summary>Matches <see cref="RealmEngine.Shared.Models.SaveGame.Id"/>.</summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Denormalised for efficient queries without deserialising DataJson.</summary>
    public string PlayerName { get; set; } = string.Empty;

    /// <summary>Save slot number (1-based). Allows per-slot lookup without deserialising.</summary>
    public int SlotIndex { get; set; }

    /// <summary>Gets or sets the save timestamp (UTC).</summary>
    public DateTime SaveDate { get; set; } = DateTime.UtcNow;

    /// <summary>Full <see cref="RealmEngine.Shared.Models.SaveGame"/> serialised as JSON.</summary>
    public string DataJson { get; set; } = "{}";
}
