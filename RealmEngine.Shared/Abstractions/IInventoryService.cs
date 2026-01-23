using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Shared.Abstractions;

/// <summary>
/// Service for managing character inventory operations.
/// </summary>
public interface IInventoryService
{
    /// <summary>
    /// Adds items to a character's inventory.
    /// </summary>
    /// <param name="characterName">The character's name.</param>
    /// <param name="items">The items to add.</param>
    /// <returns>True if items were successfully added.</returns>
    Task<bool> AddItemsAsync(string characterName, List<ItemDrop> items);

    /// <summary>
    /// Adds a single item to a character's inventory.
    /// </summary>
    /// <param name="characterName">The character's name.</param>
    /// <param name="itemRef">The item reference (e.g., @items/materials/ore:copper-ore).</param>
    /// <param name="quantity">The quantity to add.</param>
    /// <returns>True if the item was successfully added.</returns>
    Task<bool> AddItemAsync(string characterName, string itemRef, int quantity);

    /// <summary>
    /// Checks if a character has enough inventory space.
    /// </summary>
    /// <param name="characterName">The character's name.</param>
    /// <param name="itemCount">The number of items to check.</param>
    /// <returns>True if there is enough space.</returns>
    Task<bool> HasInventorySpaceAsync(string characterName, int itemCount);

    /// <summary>
    /// Gets the current item count in a character's inventory.
    /// </summary>
    /// <param name="characterName">The character's name.</param>
    /// <param name="itemRef">The item reference.</param>
    /// <returns>The quantity of the item, or 0 if not found.</returns>
    Task<int> GetItemCountAsync(string characterName, string itemRef);

    /// <summary>
    /// Removes items from a character's inventory.
    /// </summary>
    /// <param name="characterName">The character's name.</param>
    /// <param name="itemRef">The item reference.</param>
    /// <param name="quantity">The quantity to remove.</param>
    /// <returns>True if items were successfully removed.</returns>
    Task<bool> RemoveItemAsync(string characterName, string itemRef, int quantity);
}
