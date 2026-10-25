using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Entities;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>A ground item as shown to the client.</summary>
/// <param name="Id">Ground-item identifier used for pickups.</param>
/// <param name="ItemRef">Item-reference slug.</param>
/// <param name="Name">Display name resolved from the catalog (falls back to the slug).</param>
/// <param name="Rarity">Rarity name, or <c>Common</c> when the item is not in the catalog.</param>
/// <param name="Quantity">Number of units on the ground.</param>
public record GroundItemView(Guid Id, string ItemRef, string Name, string Rarity, int Quantity);

/// <summary>Hub command that lists the items lying on the ground where the character is standing.</summary>
/// <param name="CharacterId">Character whose current location to inspect.</param>
public record GetGroundItemsHubCommand(Guid CharacterId) : IRequest<GetGroundItemsHubResult>;

/// <summary>Result returned by the ground-item commands.</summary>
public record GetGroundItemsHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the ground items at the character's current location.</summary>
    public IReadOnlyList<GroundItemView> Items { get; init; } = [];
}

/// <summary>Hub command that drops one unit of a bag item onto the ground at the current location.</summary>
/// <param name="CharacterId">Character dropping the item.</param>
/// <param name="ItemRef">Item-reference slug to drop.</param>
public record DropItemToGroundHubCommand(Guid CharacterId, string ItemRef)
    : IRequest<DropItemToGroundHubResult>;

/// <summary>Result returned by <see cref="DropItemToGroundHubCommandHandler"/>.</summary>
public record DropItemToGroundHubResult
{
    /// <summary>Gets a value indicating whether the item was dropped.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the number of units left in the bag.</summary>
    public int RemainingQuantity { get; init; }

    /// <summary>Gets the ground items at the location after the drop.</summary>
    public IReadOnlyList<GroundItemView> Items { get; init; } = [];
}

/// <summary>Hub command that picks up a ground item at the character's current location.</summary>
/// <param name="CharacterId">Character picking the item up.</param>
/// <param name="GroundItemId">Ground-item identifier.</param>
public record PickUpGroundItemHubCommand(Guid CharacterId, Guid GroundItemId)
    : IRequest<PickUpGroundItemHubResult>;

/// <summary>Result returned by <see cref="PickUpGroundItemHubCommandHandler"/>.</summary>
public record PickUpGroundItemHubResult
{
    /// <summary>Gets a value indicating whether the item was picked up.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the item-reference slug that was picked up.</summary>
    public string ItemRef { get; init; } = string.Empty;

    /// <summary>Gets the ground items left at the location.</summary>
    public IReadOnlyList<GroundItemView> Items { get; init; } = [];
}

/// <summary>
/// Shared helpers for the ground-item commands: bag parsing, catalog lookups and view projection.
/// </summary>
/// <remarks>
/// Lives here rather than in a repository so the three commands stay in one file with their records.
/// </remarks>
internal static class GroundItemHelpers
{
    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    internal static List<InventoryItemDto> ParseInventory(string? blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "[]")
            return [];

        try
        {
            return JsonSerializer.Deserialize<List<InventoryItemDto>>(blob, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    /// <summary>Projects ground items into client views, resolving names from the catalog.</summary>
    /// <param name="rows">Ground items to project.</param>
    /// <param name="itemRepository">Catalog repository.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The projected views.</returns>
    internal static async Task<IReadOnlyList<GroundItemView>> ToViewsAsync(
        IReadOnlyList<GroundItem> rows,
        IItemRepository itemRepository,
        CancellationToken cancellationToken)
    {
        var views = new List<GroundItemView>(rows.Count);
        var cache = new Dictionary<string, Item?>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            if (!cache.TryGetValue(row.ItemRef, out var item))
            {
                try
                {
                    item = await itemRepository.GetBySlugAsync(row.ItemRef);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    item = null;
                }

                cache[row.ItemRef] = item;
            }

            views.Add(new GroundItemView(
                row.Id,
                row.ItemRef,
                string.IsNullOrWhiteSpace(item?.Name) ? row.ItemRef : item!.Name,
                item?.Rarity.ToString() ?? nameof(ItemRarity.Common),
                row.Quantity));
        }

        return views;
    }
}

/// <summary>
/// Handles <see cref="GetGroundItemsHubCommand"/> by listing the items lying at the character's
/// current zone location.
/// </summary>
public class GetGroundItemsHubCommandHandler(
    ICharacterRepository characterRepo,
    IGroundItemRepository groundItemRepo,
    IItemRepository itemRepository,
    ILogger<GetGroundItemsHubCommandHandler> logger)
    : IRequestHandler<GetGroundItemsHubCommand, GetGroundItemsHubResult>
{
    /// <inheritdoc />
    public async Task<GetGroundItemsHubResult> Handle(
        GetGroundItemsHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new GetGroundItemsHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        if (string.IsNullOrWhiteSpace(character.CurrentZoneLocationSlug))
            return new GetGroundItemsHubResult { Success = true, Items = [] };

        var rows = await groundItemRepo.GetAtLocationAsync(
            character.CurrentZoneId,
            character.CurrentZoneLocationSlug,
            cancellationToken);

        logger.LogDebug(
            "Ground items at {Zone}/{Location}: {Count}",
            character.CurrentZoneId,
            character.CurrentZoneLocationSlug,
            rows.Count);

        return new GetGroundItemsHubResult
        {
            Success = true,
            Items = await GroundItemHelpers.ToViewsAsync(rows, itemRepository, cancellationToken),
        };
    }
}

/// <summary>
/// Handles <see cref="DropItemToGroundHubCommand"/> by removing one unit from the character's bag and
/// creating a <see cref="GroundItem"/> at their current zone location.
/// </summary>
public class DropItemToGroundHubCommandHandler(
    ICharacterRepository characterRepo,
    IGroundItemRepository groundItemRepo,
    IItemRepository itemRepository,
    ILogger<DropItemToGroundHubCommandHandler> logger)
    : IRequestHandler<DropItemToGroundHubCommand, DropItemToGroundHubResult>
{
    /// <inheritdoc />
    public async Task<DropItemToGroundHubResult> Handle(
        DropItemToGroundHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new DropItemToGroundHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var locationSlug = character.CurrentZoneLocationSlug;
        if (string.IsNullOrWhiteSpace(locationSlug))
            return new DropItemToGroundHubResult
            {
                Success = false,
                ErrorMessage = "Move to a location before dropping items.",
            };

        var inventory = GroundItemHelpers.ParseInventory(character.InventoryBlob);
        var index = inventory.FindIndex(
            entry => string.Equals(entry.ItemRef, request.ItemRef, StringComparison.OrdinalIgnoreCase));

        if (index < 0)
            return new DropItemToGroundHubResult
            {
                Success = false,
                ErrorMessage = $"You do not have '{request.ItemRef}' in your bag.",
            };

        var entry = inventory[index];
        var remaining = entry.Quantity - 1;
        if (remaining > 0)
            inventory[index] = entry with { Quantity = remaining };
        else
            inventory.RemoveAt(index);

        character.InventoryBlob = JsonSerializer.Serialize(inventory);
        await characterRepo.UpdateAsync(character, cancellationToken);

        await groundItemRepo.AddAsync(
            new GroundItem
            {
                ZoneId = character.CurrentZoneId,
                LocationSlug = locationSlug,
                ItemRef = entry.ItemRef,
                Quantity = 1,
                Durability = entry.Durability,
                DroppedByCharacterId = character.Id,
            },
            cancellationToken);

        var rows = await groundItemRepo.GetAtLocationAsync(
            character.CurrentZoneId,
            locationSlug,
            cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} dropped '{ItemRef}' at {Zone}/{Location}",
            character.Id,
            entry.ItemRef,
            character.CurrentZoneId,
            locationSlug);

        return new DropItemToGroundHubResult
        {
            Success = true,
            RemainingQuantity = Math.Max(0, remaining),
            Items = await GroundItemHelpers.ToViewsAsync(rows, itemRepository, cancellationToken),
        };
    }
}

/// <summary>
/// Handles <see cref="PickUpGroundItemHubCommand"/> by moving a ground item into the character's bag,
/// stacking it when the catalog item allows it.
/// </summary>
public class PickUpGroundItemHubCommandHandler(
    ICharacterRepository characterRepo,
    IGroundItemRepository groundItemRepo,
    IItemRepository itemRepository,
    ILogger<PickUpGroundItemHubCommandHandler> logger)
    : IRequestHandler<PickUpGroundItemHubCommand, PickUpGroundItemHubResult>
{
    /// <inheritdoc />
    public async Task<PickUpGroundItemHubResult> Handle(
        PickUpGroundItemHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new PickUpGroundItemHubResult
            {
                Success = false,
                ErrorMessage = $"Character {request.CharacterId} not found.",
            };

        var locationSlug = character.CurrentZoneLocationSlug;
        var groundItem = await groundItemRepo.GetByIdAsync(request.GroundItemId, cancellationToken);
        if (groundItem is null)
            return new PickUpGroundItemHubResult
            {
                Success = false,
                ErrorMessage = "That item is no longer on the ground.",
            };

        if (!string.Equals(groundItem.ZoneId, character.CurrentZoneId, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(
                groundItem.LocationSlug,
                locationSlug,
                StringComparison.OrdinalIgnoreCase))
        {
            return new PickUpGroundItemHubResult
            {
                Success = false,
                ErrorMessage = "You are not standing where that item was dropped.",
            };
        }

        Item? catalogItem = null;
        try
        {
            catalogItem = await itemRepository.GetBySlugAsync(groundItem.ItemRef);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Failed to resolve item '{ItemRef}' from the catalog.", groundItem.ItemRef);
        }

        var inventory = GroundItemHelpers.ParseInventory(character.InventoryBlob);
        var existingIndex = catalogItem?.IsStackable == true
            ? inventory.FindIndex(
                entry => string.Equals(entry.ItemRef, groundItem.ItemRef, StringComparison.OrdinalIgnoreCase))
            : -1;

        if (existingIndex >= 0)
        {
            var existing = inventory[existingIndex];
            inventory[existingIndex] = existing with { Quantity = existing.Quantity + groundItem.Quantity };
        }
        else
        {
            inventory.Add(new InventoryItemDto(groundItem.ItemRef, groundItem.Quantity, groundItem.Durability));
        }

        character.InventoryBlob = JsonSerializer.Serialize(inventory);
        await characterRepo.UpdateAsync(character, cancellationToken);
        await groundItemRepo.RemoveAsync(groundItem, cancellationToken);

        var rows = await groundItemRepo.GetAtLocationAsync(
            character.CurrentZoneId,
            locationSlug ?? string.Empty,
            cancellationToken);

        logger.LogInformation(
            "Character {CharacterId} picked up '{ItemRef}' x{Quantity}",
            character.Id,
            groundItem.ItemRef,
            groundItem.Quantity);

        return new PickUpGroundItemHubResult
        {
            Success = true,
            ItemRef = groundItem.ItemRef,
            Items = await GroundItemHelpers.ToViewsAsync(rows, itemRepository, cancellationToken),
        };
    }
}
