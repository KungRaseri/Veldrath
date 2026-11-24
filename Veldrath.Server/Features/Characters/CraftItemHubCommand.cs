using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters;

/// <summary>
/// Hub command that crafts an item from a catalog recipe: it checks the character's level, the
/// required skill rank and every material, consumes the materials, adds the output and awards the
/// recipe's skill experience.
/// </summary>
/// <param name="CharacterId">The ID of the character attempting to craft.</param>
/// <param name="RecipeSlug">The slug of the recipe to craft (e.g. <c>"iron-longsword"</c>).</param>
public record CraftItemHubCommand(Guid CharacterId, string RecipeSlug) : IRequest<CraftItemHubResult>;

/// <summary>One material a craft consumed.</summary>
/// <param name="ItemRef">The material's item reference.</param>
/// <param name="Quantity">How many were consumed.</param>
public record ConsumedMaterialDto(string ItemRef, int Quantity);

/// <summary>Result returned by <see cref="CraftItemHubCommandHandler"/>.</summary>
public record CraftItemHubResult
{
    /// <summary>Gets a value indicating whether the crafting operation succeeded.</summary>
    public bool Success { get; init; }

    /// <summary>Gets the error message when <see cref="Success"/> is <see langword="false"/>.</summary>
    public string? ErrorMessage { get; init; }

    /// <summary>Gets the recipe slug that was attempted.</summary>
    public string RecipeSlug { get; init; } = string.Empty;

    /// <summary>Gets the recipe's display name, or <see langword="null"/> when the recipe was not found.</summary>
    public string? RecipeName { get; init; }

    /// <summary>Gets the item reference produced, or <see langword="null"/> when nothing was crafted.</summary>
    public string? ItemRef { get; init; }

    /// <summary>Gets how many of the output item were produced.</summary>
    public int OutputQuantity { get; init; }

    /// <summary>Gets the materials that were consumed.</summary>
    public IReadOnlyList<ConsumedMaterialDto> ConsumedMaterials { get; init; } = [];

    /// <summary>Gets the skill that earned experience, or <see langword="null"/> when the recipe trains nothing.</summary>
    public string? SkillId { get; init; }

    /// <summary>Gets the skill experience the craft awarded.</summary>
    public int SkillXp { get; init; }

    /// <summary>Gets the skill's experience total after the award, or zero when the recipe trains nothing.</summary>
    public int SkillTotalXp { get; init; }

    /// <summary>Gets the skill's rank after the award.</summary>
    public int SkillRank { get; init; }

    /// <summary>Gets a value indicating whether this award crossed a rank boundary.</summary>
    public bool SkillRankedUp { get; init; }
}

/// <summary>
/// Handles <see cref="CraftItemHubCommand"/> against the recipe catalog and the character's bag.
/// </summary>
/// <remarks>
/// Deliberately does not go through the engine's <c>CraftRecipeCommand</c>: that path takes a
/// hydrated engine character plus a crafting station and depends on a catalog loader, which would
/// make it a second source of truth beside the database. The rules it enforces here are the ones the
/// data can actually answer — level, skill rank and materials. <c>RequiredStation</c> is content the
/// world model cannot yet match to a place (nothing places a station in a location), so it is
/// reported to the client as information rather than pretended to be enforced.
/// <para>
/// Materials are read from and written to the character's own <c>InventoryBlob</c> through
/// <see cref="CharacterBag"/> — the bag every other server path fills and <c>GetInventoryHubCommand</c>
/// returns to the client. The engine's <c>IInventoryService</c> is not used: it writes
/// <c>GameDbContext.InventoryRecords</c>, a table nothing populates, so crafting through it demanded
/// materials the player had and handed back items they never received.
/// </para>
/// </remarks>
public class CraftItemHubCommandHandler : IRequestHandler<CraftItemHubCommand, CraftItemHubResult>
{
    internal const string KeyGold = "Gold";

    private const string SkillKeyPrefix = "Skill_";
    private const string SkillRankSuffix = "_Rank";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly ICharacterRepository _characterRepo;
    private readonly IRecipeRepository _recipes;
    private readonly IMediator _mediator;
    private readonly ILogger<CraftItemHubCommandHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="CraftItemHubCommandHandler"/>.</summary>
    /// <param name="characterRepo">Repository used to load and persist characters.</param>
    /// <param name="recipes">Recipe catalog.</param>
    /// <param name="mediator">Mediator used to award the recipe's skill experience.</param>
    /// <param name="logger">Logger instance.</param>
    public CraftItemHubCommandHandler(
        ICharacterRepository characterRepo,
        IRecipeRepository recipes,
        IMediator mediator,
        ILogger<CraftItemHubCommandHandler> logger)
    {
        _characterRepo = characterRepo;
        _recipes = recipes;
        _mediator = mediator;
        _logger = logger;
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
            return Fail(request.RecipeSlug, "Recipe slug cannot be empty");

        var character = await _characterRepo.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return Fail(request.RecipeSlug, $"Character {request.CharacterId} not found");

        Recipe? recipe;
        try
        {
            recipe = await _recipes.GetBySlugAsync(request.RecipeSlug);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Recipe lookup failed for '{RecipeSlug}'.", request.RecipeSlug);
            return Fail(request.RecipeSlug, "The recipe catalog is unavailable right now");
        }

        if (recipe is null)
            return Fail(request.RecipeSlug, $"No recipe named '{request.RecipeSlug}' exists");

        if (string.IsNullOrWhiteSpace(recipe.OutputItemReference))
        {
            _logger.LogWarning(
                "Recipe '{RecipeSlug}' has no output item; refusing to craft it.", recipe.Slug);
            return Fail(request.RecipeSlug, "That recipe produces nothing");
        }

        if (recipe.RequiredLevel > character.Level)
        {
            return Fail(
                request.RecipeSlug,
                $"{recipe.Name} requires level {recipe.RequiredLevel}; you are level {character.Level}.",
                recipe.Name);
        }

        var attributes = ReadAttributes(character.Attributes, character.Id);
        var skillId = string.IsNullOrWhiteSpace(recipe.RequiredSkill) ? null : recipe.RequiredSkill.Trim();

        if (skillId is not null && recipe.RequiredSkillLevel > 0)
        {
            var rank = attributes.TryGetValue($"{SkillKeyPrefix}{skillId}{SkillRankSuffix}", out var stored)
                ? stored
                : 0;

            if (rank < recipe.RequiredSkillLevel)
            {
                return Fail(
                    request.RecipeSlug,
                    $"{recipe.Name} requires {skillId} rank {recipe.RequiredSkillLevel}; you are rank {rank}.",
                    recipe.Name);
            }
        }

        // The bag is the character's own InventoryBlob — the store harvesting, chests, combat loot and the
        // shop write, and the one GetInventoryHubCommand hands back to the client. Reading it through the
        // engine's inventory service instead looked at an empty table: every material read as unowned and
        // every crafted item went somewhere no client could see.
        var bag = CharacterBag.Read(character.InventoryBlob);

        // Check every material before consuming any of them, so a missing ingredient costs nothing.
        var shortages = new List<string>();
        foreach (var material in recipe.Materials)
        {
            if (string.IsNullOrWhiteSpace(material.ItemReference) || material.Quantity <= 0)
                continue;

            var owned = CharacterBag.Count(bag, material.ItemReference);
            if (owned < material.Quantity)
                shortages.Add($"{material.Quantity - owned} more {material.ItemReference}");
        }

        if (shortages.Count > 0)
        {
            return Fail(
                request.RecipeSlug,
                $"You are short of: {string.Join(", ", shortages)}.",
                recipe.Name);
        }

        // Take is all-or-nothing and the shortages above are already ruled out, so a refused take means the
        // bag changed under us; nothing has been written at that point, so refusing costs the player nothing.
        var consumed = new List<ConsumedMaterialDto>();
        foreach (var material in recipe.Materials)
        {
            if (string.IsNullOrWhiteSpace(material.ItemReference) || material.Quantity <= 0)
                continue;

            if (!CharacterBag.Take(bag, material.ItemReference, material.Quantity))
            {
                _logger.LogError(
                    "Failed to remove {Quantity}x {ItemRef} for character {CharacterId}; craft refused.",
                    material.Quantity, material.ItemReference, character.Id);
                return Fail(request.RecipeSlug, "Your bag could not be updated, so nothing was crafted.", recipe.Name);
            }

            consumed.Add(new ConsumedMaterialDto(material.ItemReference, material.Quantity));
        }

        // Bag entries carry the bare slug because that is what the client resolves in the catalog; a recipe
        // names the full reference, so the output is reduced the same way harvesting and chest loot are.
        var outputQuantity = recipe.OutputQuantity > 0 ? recipe.OutputQuantity : 1;
        CharacterBag.Add(bag, CharacterBag.Slug(recipe.OutputItemReference), outputQuantity);
        character.InventoryBlob = CharacterBag.Serialize(bag);

        // The award itself is a command, so the key convention and the rank size live in one place. The character
        // is handed over already loaded because this handler owns the character-row write: the award handler
        // applies the experience to the attributes blob in memory and leaves it unsaved, and the single
        // UpdateAsync below writes that row once — InventoryBlob (materials taken, output added) and Attributes
        // (the award) together, plus LastPlayedAt. That is one row in one save; it is not a transaction with
        // anything else. The craft does not go through IInventoryService at all, so there is no second store and
        // no second write to clobber the bag.
        AwardSkillXpHubResult? award = null;
        if (skillId is not null && recipe.ExperienceGained > 0)
        {
            award = await _mediator.Send(
                new AwardSkillXpHubCommand
                {
                    CharacterId = character.Id,
                    SkillId = skillId,
                    Amount = recipe.ExperienceGained,
                    LoadedCharacter = character,
                },
                cancellationToken);

            if (!award.Success)
            {
                _logger.LogWarning(
                    "Character {CharacterId} crafted {RecipeSlug} but the skill award failed: {Error}",
                    character.Id, recipe.Slug, award.ErrorMessage);
                award = null;
            }
        }

        if (award is not null)
        {
            character.LastPlayedAt = DateTimeOffset.UtcNow;
        }

        // Always saved: the bag changed even when the recipe trains no skill.
        await _characterRepo.UpdateAsync(character, cancellationToken);

        _logger.LogInformation(
            "Character {CharacterId} crafted {Quantity}x '{ItemRef}' from recipe '{RecipeSlug}'.",
            character.Id, outputQuantity, recipe.OutputItemReference, recipe.Slug);

        return new CraftItemHubResult
        {
            Success = true,
            RecipeSlug = recipe.Slug,
            RecipeName = recipe.Name,
            ItemRef = recipe.OutputItemReference,
            OutputQuantity = outputQuantity,
            ConsumedMaterials = consumed,
            SkillId = award?.SkillId,
            SkillXp = award is null ? 0 : recipe.ExperienceGained,
            SkillTotalXp = award?.TotalXp ?? 0,
            SkillRank = award?.CurrentRank ?? 0,
            SkillRankedUp = award?.RankedUp ?? false,
        };
    }

    private Dictionary<string, int> ReadAttributes(string? attributesJson, Guid characterId)
    {
        if (string.IsNullOrWhiteSpace(attributesJson) || attributesJson == "{}")
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        try
        {
            return JsonSerializer.Deserialize<Dictionary<string, int>>(attributesJson, JsonOptions)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialise attributes for character {Id}; treating as empty.",
                characterId);
            return new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static CraftItemHubResult Fail(string? recipeSlug, string message, string? recipeName = null) =>
        new()
        {
            Success = false,
            ErrorMessage = message,
            RecipeSlug = recipeSlug ?? string.Empty,
            RecipeName = recipeName,
        };
}
