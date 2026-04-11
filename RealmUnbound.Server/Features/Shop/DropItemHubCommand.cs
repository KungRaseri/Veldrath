using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;
using Veldrath.Server.Features.Characters;

namespace Veldrath.Server.Features.Shop;

/// <summary>Hub command that drops one unit of an item from the character's inventory.</summary>
/// <param name="CharacterId">The character dropping the item.</param>
/// <param name="ItemRef">The item slug to drop.</param>
public record DropItemHubCommand(Guid CharacterId, string ItemRef) : IRequest<DropItemHubResult>;

/// <summary>Result returned by <see cref="DropItemHubCommandHandler"/>.</summary>
public record DropItemHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the item slug that was dropped.</summary>
    public string ItemRef { get; init; } = string.Empty;

    /// <summary>Gets the quantity remaining in the inventory slot after the drop (0 when the slot was removed).</summary>
    public int RemainingQuantity { get; init; }
}

/// <summary>
/// Handles <see cref="DropItemHubCommand"/> by removing one unit of the specified item from
/// the character's inventory blob. The entire slot is removed when the quantity would reach zero.
/// </summary>
public class DropItemHubCommandHandler : IRequestHandler<DropItemHubCommand, DropItemHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<DropItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="DropItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public DropItemHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<DropItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the drop and returns the outcome.</summary>
    /// <param name="request">The command containing the character and item.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="DropItemHubResult"/> describing the outcome.</returns>
    public async Task<DropItemHubResult> Handle(DropItemHubCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ItemRef))
            return Fail("ItemRef is required.");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        var inventory = ParseInventory(character.InventoryBlob);
        var existing = inventory.FirstOrDefault(i =>
            string.Equals(i.ItemRef, request.ItemRef, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
            return Fail($"Item '{request.ItemRef}' not found in inventory.");

        int remaining;
        if (existing.Quantity > 1)
        {
            var idx = inventory.IndexOf(existing);
            inventory[idx] = existing with { Quantity = existing.Quantity - 1 };
            remaining = inventory[idx].Quantity;
        }
        else
        {
            inventory.Remove(existing);
            remaining = 0;
        }

        character.InventoryBlob = JsonSerializer.Serialize(inventory);
        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} dropped '{ItemRef}'; {Remaining} remaining",
            request.CharacterId, request.ItemRef, remaining);

        return new DropItemHubResult
        {
            Success           = true,
            ItemRef           = request.ItemRef,
            RemainingQuantity = remaining,
        };
    }

    private static List<InventoryItemDto> ParseInventory(string blob)
    {
        if (string.IsNullOrWhiteSpace(blob) || blob == "[]") return [];
        try { return JsonSerializer.Deserialize<List<InventoryItemDto>>(blob, JsonOptions) ?? []; }
        catch { return []; }
    }

    private static DropItemHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
