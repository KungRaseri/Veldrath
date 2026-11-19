using System.Text.Json;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Reads and writes the character's bag — the <c>InventoryBlob</c> column the game client reads.
/// </summary>
/// <remarks>
/// The engine's <c>IInventoryService</c> (<c>EfCoreInventoryService</c>) writes save-game records, which is a
/// different store: a drop "added" through it is announced to the player and never appears in their bag. Every
/// server path that gives a character an item has to go through this blob, which is what the shop, ground items
/// and crafting already do.
///
/// The blob is a JSON array of objects with at least <c>ItemRef</c> and <c>Quantity</c>. It is read as
/// dictionaries rather than a typed record on purpose: a field this code does not know about survives a write.
/// </remarks>
public static class CharacterBag
{
    /// <summary>Reads a bag blob, treating absent or unreadable content as an empty bag.</summary>
    /// <param name="blob">The stored <c>InventoryBlob</c>.</param>
    /// <returns>The bag as mutable JSON objects.</returns>
    public static List<Dictionary<string, JsonElement>> Read(string? blob)
    {
        if (string.IsNullOrWhiteSpace(blob)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(blob) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Adds a stack to the bag, stacking onto the same item when it is already carried.</summary>
    /// <param name="bag">The bag to mutate.</param>
    /// <param name="itemRef">The item slug the bag is keyed by.</param>
    /// <param name="quantity">How many to add.</param>
    public static void Add(List<Dictionary<string, JsonElement>> bag, string itemRef, int quantity)
    {
        ArgumentNullException.ThrowIfNull(bag);

        var existing = bag.FirstOrDefault(entry =>
            entry.TryGetValue("ItemRef", out var reference) &&
            reference.ValueKind == JsonValueKind.String &&
            string.Equals(reference.GetString(), itemRef, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            bag.Add(new Dictionary<string, JsonElement>
            {
                ["ItemRef"] = JsonSerializer.SerializeToElement(itemRef),
                ["Quantity"] = JsonSerializer.SerializeToElement(quantity),
            });
            return;
        }

        var current = existing.TryGetValue("Quantity", out var stored) && stored.TryGetInt32(out var value) ? value : 0;
        existing["Quantity"] = JsonSerializer.SerializeToElement(current + quantity);
    }

    /// <summary>Serializes a bag back into the column's shape.</summary>
    /// <param name="bag">The bag to serialize.</param>
    /// <returns>The JSON to store in <c>InventoryBlob</c>.</returns>
    public static string Serialize(List<Dictionary<string, JsonElement>> bag) => JsonSerializer.Serialize(bag);
}
