using System.Text.Json;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Reads and writes the character's bag — the <c>InventoryBlob</c> column the game client reads.
/// </summary>
/// <remarks>
/// This is the store the game client reads: every server path that gives or takes an item has to go through this
/// blob. The engine's <c>IInventoryService</c> (<c>EfCoreInventoryService</c>) writes save-game records in a
/// different table (<c>GameDbContext.InventoryRecords</c>) that no server path populates, so an item "added"
/// through it is announced to the player and never appears in their bag.
///
/// The blob is a JSON array of objects with at least <c>ItemRef</c> and <c>Quantity</c>. It is read as
/// dictionaries rather than a typed record on purpose: a field this code does not know about survives a write.
/// Entries written by harvesting, chests and combat hold the bare slug (<c>iron-ingot</c>), while recipes and the
/// shop name the full reference (<c>@items/materials/ingots:iron-ingot</c>); <see cref="Count"/> and
/// <see cref="Take"/> accept either form so the two cannot drift apart.
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
        ArgumentNullException.ThrowIfNull(itemRef);

        // Match by leaf as well as by literal reference: harvest writes `iron-ingot`, the shop and recipes
        // write `@items/...:iron-ingot`, and one item must not become two stacks.
        var slug = Slug(itemRef);
        var existing = bag.FirstOrDefault(entry => Matches(entry, itemRef, slug));

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

    /// <summary>
    /// The bare item slug a reference ends with: <c>@items/materials/ingots:iron-ingot</c> becomes
    /// <c>iron-ingot</c>, and so does <c>@items/iron-ingot</c>. A bare slug is returned unchanged.
    /// </summary>
    /// <param name="itemRef">The full item reference or slug.</param>
    /// <returns>The slug.</returns>
    public static string Slug(string itemRef)
    {
        ArgumentNullException.ThrowIfNull(itemRef);

        var separator = Math.Max(itemRef.LastIndexOf(':'), itemRef.LastIndexOf('/'));
        return separator >= 0 && separator < itemRef.Length - 1 ? itemRef[(separator + 1)..] : itemRef;
    }

    /// <summary>How many of an item the bag carries, matching either the full reference or its slug.</summary>
    /// <param name="bag">The bag to read.</param>
    /// <param name="itemRef">The item reference or slug to count.</param>
    /// <returns>The carried quantity, or zero when the bag has none.</returns>
    public static int Count(IEnumerable<Dictionary<string, JsonElement>> bag, string itemRef)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(itemRef);

        var slug = Slug(itemRef);
        return bag
            .Where(entry => Matches(entry, itemRef, slug))
            .Sum(entry => entry.TryGetValue("Quantity", out var stored) && stored.TryGetInt32(out var value) ? value : 0);
    }

    /// <summary>
    /// Removes a quantity of an item from the bag, matching either the full reference or its slug. Nothing is
    /// removed when the bag does not carry enough, so a caller that checked first never sees a partial take.
    /// </summary>
    /// <param name="bag">The bag to mutate.</param>
    /// <param name="itemRef">The item reference or slug to take.</param>
    /// <param name="quantity">How many to take.</param>
    /// <returns><see langword="true"/> when the whole quantity was removed.</returns>
    public static bool Take(List<Dictionary<string, JsonElement>> bag, string itemRef, int quantity)
    {
        ArgumentNullException.ThrowIfNull(bag);
        ArgumentNullException.ThrowIfNull(itemRef);

        if (quantity <= 0) return true;
        if (Count(bag, itemRef) < quantity) return false;

        var slug = Slug(itemRef);
        var remaining = quantity;

        // Walk backwards so an emptied entry can be removed without shifting the ones still to inspect.
        for (var index = bag.Count - 1; index >= 0 && remaining > 0; index--)
        {
            if (!Matches(bag[index], itemRef, slug)) continue;

            var carried = bag[index].TryGetValue("Quantity", out var stored) && stored.TryGetInt32(out var value)
                ? value
                : 0;
            var taken = Math.Min(carried, remaining);
            remaining -= taken;

            if (carried - taken > 0)
                bag[index]["Quantity"] = JsonSerializer.SerializeToElement(carried - taken);
            else
                bag.RemoveAt(index);
        }

        return remaining == 0;
    }

    private static bool Matches(Dictionary<string, JsonElement> entry, string itemRef, string slug)
    {
        if (!entry.TryGetValue("ItemRef", out var reference) || reference.ValueKind != JsonValueKind.String)
            return false;

        var stored = reference.GetString();
        return string.Equals(stored, itemRef, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(stored, slug, StringComparison.OrdinalIgnoreCase) ||
               string.Equals(Slug(stored ?? string.Empty), slug, StringComparison.OrdinalIgnoreCase);
    }
}
