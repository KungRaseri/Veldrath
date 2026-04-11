using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that adds or removes gold from a character's attributes blob.
/// Positive <see cref="Amount"/> values add gold (looting, quests); negative values
/// spend gold (purchases, fees). The gold total is clamped to zero — the handler
/// rejects requests that would overdraw.
/// </summary>
public record AddGoldHubCommand : IRequest<AddGoldHubResult>
{
    /// <summary>Gets the ID of the character whose gold is being modified.</summary>
    public required Guid CharacterId { get; init; }

    /// <summary>
    /// Gets the amount to add (positive) or spend (negative). Cannot be zero.
    /// Negative values that exceed the current gold total are rejected.
    /// </summary>
    public required int Amount { get; init; }

    /// <summary>Gets an optional label describing the gold source or sink (e.g. <c>"Loot"</c>, <c>"Quest"</c>, <c>"Purchase"</c>).</summary>
    public string? Source { get; init; }
}

/// <summary>Result returned by <see cref="AddGoldHubCommandHandler"/>.</summary>
public record AddGoldHubResult
{
    /// <summary>Gets a value indicating whether the operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the gold amount that was added (positive) or removed (negative).</summary>
    public int GoldAdded { get; init; }

    /// <summary>Gets the character's total gold after the transaction.</summary>
    public int NewGoldTotal { get; init; }
}

/// <summary>
/// Handles <see cref="AddGoldHubCommand"/> by loading the server-side character,
/// deserialising the attributes JSON blob, adding or deducting the requested gold
/// (rejecting over-spend), and persisting the updated blob.
/// </summary>
public class AddGoldHubCommandHandler : IRequestHandler<AddGoldHubCommand, AddGoldHubResult>
{
    internal const string KeyGold = "Gold";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<AddGoldHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="AddGoldHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public AddGoldHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<AddGoldHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the gold transaction outcome.</summary>
    /// <param name="request">The command containing the character ID and gold amount.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>An <see cref="AddGoldHubResult"/> describing the outcome.</returns>
    public async Task<AddGoldHubResult> Handle(
        AddGoldHubCommand request,
        CancellationToken cancellationToken)
    {
        if (request.Amount == 0)
            return new AddGoldHubResult { Success = false, ErrorMessage = "Gold amount cannot be zero" };

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new AddGoldHubResult { Success = false, ErrorMessage = $"Character {request.CharacterId} not found" };

        var attrs = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(character.Attributes) && character.Attributes != "{}")
        {
            try
            {
                attrs = JsonSerializer.Deserialize<Dictionary<string, int>>(
                    character.Attributes, JsonOptions) ?? attrs;
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(
                    ex,
                    "Failed to deserialise attributes for character {Id}; treating as empty.",
                    character.Id);
            }
        }

        var currentGold = attrs.TryGetValue(KeyGold, out var g) ? g : 0;

        if (request.Amount < 0 && -request.Amount > currentGold)
            return new AddGoldHubResult
            {
                Success      = false,
                ErrorMessage = $"Not enough gold. Have {currentGold}, need {-request.Amount}.",
            };

        var newGold = currentGold + request.Amount;
        attrs[KeyGold] = newGold;

        character.Attributes   = JsonSerializer.Serialize(attrs);
        character.LastPlayedAt = DateTimeOffset.UtcNow;

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} gold changed by {Amount} ({Source}). {OldGold} → {NewGold}",
            request.CharacterId, request.Amount, request.Source ?? "Unknown", currentGold, newGold);

        return new AddGoldHubResult
        {
            Success      = true,
            GoldAdded    = request.Amount,
            NewGoldTotal = newGold,
        };
    }
}
