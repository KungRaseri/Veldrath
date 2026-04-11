using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that crafts an item using a named recipe, deducting the crafting cost from the
/// character's gold. The recipe is validated non-empty and the character must hold enough gold.
/// </summary>
/// <param name="CharacterId">The ID of the character attempting to craft.</param>
/// <param name="RecipeSlug">The slug of the recipe to craft (e.g. <c>"iron-sword"</c>).</param>
public record CraftItemHubCommand(Guid CharacterId, string RecipeSlug) : IRequest<CraftItemHubResult>;

/// <summary>Result returned by <see cref="CraftItemHubCommandHandler"/>.</summary>
public record CraftItemHubResult
{
    /// <summary>Gets a value indicating whether the crafting operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the slug of the crafted item, or <see langword="null"/> when crafting failed.</summary>
    public string? ItemCrafted { get; init; }

    /// <summary>Gets the gold spent on crafting (equals <see cref="DefaultCraftingCost"/> on success).</summary>
    public int GoldSpent { get; init; }

    /// <summary>Gets the character's remaining gold after crafting.</summary>
    public int RemainingGold { get; init; }
}

/// <summary>
/// Handles <see cref="CraftItemHubCommand"/> by reading the server-side character's attributes
/// blob, validating the recipe slug and gold balance, deducting the crafting cost, and persisting
/// the updated blob.
/// </summary>
public class CraftItemHubCommandHandler : IRequestHandler<CraftItemHubCommand, CraftItemHubResult>
{
    /// <summary>Gold cost deducted from the character's total per crafting attempt.</summary>
    internal const int DefaultCraftingCost = 50;

    internal const string KeyGold = "Gold";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly ILogger<CraftItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="CraftItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="logger">Logger instance.</param>
    public CraftItemHubCommandHandler(
        ICharacterRepository characterRepo,
        ILogger<CraftItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _logger        = logger;
    }

    /// <summary>Handles the command and returns the crafting outcome.</summary>
    /// <param name="request">The command containing the character ID and recipe slug.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A <see cref="CraftItemHubResult"/> describing the outcome.</returns>
    public async Task<CraftItemHubResult> Handle(
        CraftItemHubCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RecipeSlug))
            return new CraftItemHubResult { Success = false, ErrorMessage = "Recipe slug cannot be empty" };

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return new CraftItemHubResult { Success = false, ErrorMessage = $"Character {request.CharacterId} not found" };

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

        if (currentGold < DefaultCraftingCost)
            return new CraftItemHubResult
            {
                Success      = false,
                ErrorMessage = "Not enough gold to craft this item",
            };

        var newGold = currentGold - DefaultCraftingCost;
        attrs[KeyGold] = newGold;

        character.Attributes   = JsonSerializer.Serialize(attrs);
        character.LastPlayedAt = DateTimeOffset.UtcNow;

        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} crafted '{RecipeSlug}'. Gold: {Old} → {New}",
            request.CharacterId, request.RecipeSlug, currentGold, newGold);

        return new CraftItemHubResult
        {
            Success       = true,
            ItemCrafted   = request.RecipeSlug,
            GoldSpent     = DefaultCraftingCost,
            RemainingGold = newGold,
        };
    }
}
