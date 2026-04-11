using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>Inventory slot entry as stored in the character's <c>InventoryBlob</c> JSON.</summary>
/// <param name="ItemRef">Item-reference slug (e.g. <c>"iron_sword"</c>).</param>
/// <param name="Quantity">Stack size (always ≥ 1).</param>
/// <param name="Durability">Current durability (0–100), or <see langword="null"/> for stackable items.</param>
public record InventoryItemDto(string ItemRef, int Quantity, int? Durability);

/// <summary>Hub command that fetches the inventory of the active character.</summary>
/// <param name="CharacterId">ID of the character whose inventory to return.</param>
public record GetInventoryHubCommand(Guid CharacterId) : IRequest<GetInventoryHubResult>;

/// <summary>Result returned by <see cref="GetInventoryHubCommandHandler"/>.</summary>
public record GetInventoryHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the list of items in the character's inventory.</summary>
    public IReadOnlyList<InventoryItemDto> Items { get; init; } = [];
}

/// <summary>
/// Handles <see cref="GetInventoryHubCommand"/> by loading the server-side character
/// and deserialising its <c>InventoryBlob</c> JSON array into a list of <see cref="InventoryItemDto"/> entries.
/// Returns an empty list when the inventory is empty.
/// </summary>
public class GetInventoryHubCommandHandler : IRequestHandler<GetInventoryHubCommand, GetInventoryHubResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<GetInventoryHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetInventoryHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load characters.</param>
    /// <param name="logger">Logger instance.</param>
    public GetInventoryHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<GetInventoryHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the character's current inventory.</summary>
    /// <param name="request">The command containing the character ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="GetInventoryHubResult"/> with the inventory items.</returns>
    public async Task<GetInventoryHubResult> Handle(
        GetInventoryHubCommand request,
        CancellationToken cancellationToken)
    {
        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail($"Character {request.CharacterId} not found.");

        var items = new List<InventoryItemDto>();
        var blob  = character.InventoryBlob;

        if (!string.IsNullOrWhiteSpace(blob) && blob != "[]")
        {
            try
            {
                items = JsonSerializer.Deserialize<List<InventoryItemDto>>(blob, JsonOptions) ?? items;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to deserialise inventory blob for character {Id}; treating as empty.",
                    character.Id);
            }
        }

        return new GetInventoryHubResult { Success = true, Items = items };
    }

    private static GetInventoryHubResult Fail(string message) =>
        new() { Success = false, ErrorMessage = message };
}
