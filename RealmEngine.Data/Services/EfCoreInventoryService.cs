using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RealmEngine.Data.Persistence;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models.Harvesting;

namespace RealmEngine.Data.Services;

/// <summary>
/// EF Core-backed implementation of <see cref="IInventoryService"/>.
/// Persists inventory slots as <see cref="InventoryRecord"/> rows in <see cref="GameDbContext"/>.
/// SaveGameId scoping is not yet threaded through the interface; uses an empty string so
/// uniqueness is maintained per (characterName, itemRef) globally until save-game context is wired.
/// </summary>
public class EfCoreInventoryService(
    GameDbContext db,
    ISaveGameContext saveGameContext,
    ILogger<EfCoreInventoryService> logger) : IInventoryService
{

    /// <inheritdoc />
    public async Task<bool> AddItemsAsync(string characterName, List<ItemDrop> items)
    {
        foreach (var item in items)
            if (!await AddItemAsync(characterName, item.ItemRef, item.Quantity))
                return false;
        return true;
    }

    /// <inheritdoc />
    public async Task<bool> AddItemAsync(string characterName, string itemRef, int quantity)
    {
        var saveGameId = saveGameContext.SaveGameId;
        try
        {
            var record = await db.InventoryRecords
                .FirstOrDefaultAsync(r => r.SaveGameId == saveGameId
                                       && r.CharacterName == characterName
                                       && r.ItemRef == itemRef);
            if (record is null)
            {
                db.InventoryRecords.Add(new InventoryRecord
                {
                    SaveGameId = saveGameId,
                    CharacterName = characterName,
                    ItemRef = itemRef,
                    Quantity = quantity,
                    UpdatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                record.Quantity += quantity;
                record.UpdatedAt = DateTime.UtcNow;
            }

            await db.SaveChangesAsync();
            logger.LogDebug("Added {Quantity}x {ItemRef} to {CharacterName}'s inventory", quantity, itemRef, characterName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to add {ItemRef} to {CharacterName}'s inventory", itemRef, characterName);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<bool> HasInventorySpaceAsync(string characterName, int itemCount)
    {
        const int maxSlots = 100;
        var saveGameId = saveGameContext.SaveGameId;
        var currentSlots = await db.InventoryRecords
            .CountAsync(r => r.SaveGameId == saveGameId && r.CharacterName == characterName);
        return currentSlots + itemCount <= maxSlots;
    }

    /// <inheritdoc />
    public async Task<int> GetItemCountAsync(string characterName, string itemRef)
    {
        var saveGameId = saveGameContext.SaveGameId;
        var record = await db.InventoryRecords.AsNoTracking()
            .FirstOrDefaultAsync(r => r.SaveGameId == saveGameId
                                   && r.CharacterName == characterName
                                   && r.ItemRef == itemRef);
        return record?.Quantity ?? 0;
    }

    /// <inheritdoc />
    public async Task<bool> RemoveItemAsync(string characterName, string itemRef, int quantity)
    {
        var saveGameId = saveGameContext.SaveGameId;
        try
        {
            var record = await db.InventoryRecords
                .FirstOrDefaultAsync(r => r.SaveGameId == saveGameId
                                       && r.CharacterName == characterName
                                       && r.ItemRef == itemRef);

            if (record is null)
            {
                logger.LogWarning("Cannot remove {ItemRef} from {CharacterName}: item not found", itemRef, characterName);
                return false;
            }

            if (record.Quantity < quantity)
            {
                logger.LogWarning("Cannot remove {Quantity}x {ItemRef} from {CharacterName}: only has {Current}",
                    quantity, itemRef, characterName, record.Quantity);
                return false;
            }

            record.Quantity -= quantity;
            record.UpdatedAt = DateTime.UtcNow;
            if (record.Quantity == 0)
                db.InventoryRecords.Remove(record);

            await db.SaveChangesAsync();
            logger.LogDebug("Removed {Quantity}x {ItemRef} from {CharacterName}'s inventory", quantity, itemRef, characterName);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to remove {ItemRef} from {CharacterName}'s inventory", itemRef, characterName);
            return false;
        }
    }
}
