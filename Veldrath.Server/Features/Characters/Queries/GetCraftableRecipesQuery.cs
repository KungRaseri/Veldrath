using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Logging;
using RealmEngine.Shared.Abstractions;
using RealmEngine.Shared.Models;
using Veldrath.Server.Data.Repositories;

namespace Veldrath.Server.Features.Characters.Queries;

/// <summary>One material a recipe needs, with how many the character already holds.</summary>
public sealed class CraftMaterialDto
{
    /// <summary>Gets the material's item reference.</summary>
    public string ItemRef { get; init; } = string.Empty;

    /// <summary>Gets how many the recipe needs.</summary>
    public int Required { get; init; }

    /// <summary>Gets how many the character holds.</summary>
    public int Owned { get; init; }
}

/// <summary>
/// A catalog recipe as the crafting panel needs it: what it makes, what it costs, and whether the
/// character can make it right now.
/// </summary>
/// <remarks>
/// <see cref="RequiredStation"/> is reported but never used to refuse: nothing in the world model
/// places a crafting station in a location, so the server cannot honestly check it. The panel shows
/// it as information so the content is not lost, and the gap is recorded in the plan.
/// </remarks>
public sealed class CraftableRecipeDto
{
    /// <summary>Gets the recipe slug.</summary>
    public string Slug { get; init; } = string.Empty;

    /// <summary>Gets the recipe's display name.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Gets the recipe's description.</summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>Gets the recipe category (weapons, armor, consumables…).</summary>
    public string Category { get; init; } = string.Empty;

    /// <summary>Gets the character level the recipe requires.</summary>
    public int RequiredLevel { get; init; }

    /// <summary>Gets the skill the recipe trains, or <see langword="null"/> when it trains none.</summary>
    public string? RequiredSkill { get; init; }

    /// <summary>Gets the skill rank the recipe requires.</summary>
    public int RequiredSkillLevel { get; init; }

    /// <summary>Gets the character's current rank in <see cref="RequiredSkill"/>.</summary>
    public int SkillRank { get; init; }

    /// <summary>Gets the station the recipe names, or <see langword="null"/>. Informational.</summary>
    public string? RequiredStation { get; init; }

    /// <summary>Gets the station tier the recipe names. Informational.</summary>
    public int RequiredStationTier { get; init; }

    /// <summary>Gets the materials the recipe needs, with the character's holdings.</summary>
    public IReadOnlyList<CraftMaterialDto> Materials { get; init; } = [];

    /// <summary>Gets the item reference the recipe produces.</summary>
    public string OutputItemRef { get; init; } = string.Empty;

    /// <summary>Gets how many the recipe produces.</summary>
    public int OutputQuantity { get; init; }

    /// <summary>Gets the skill experience the recipe awards.</summary>
    public int ExperienceGained { get; init; }

    /// <summary>Gets a value indicating whether the character can craft the recipe right now.</summary>
    public bool Available { get; init; }

    /// <summary>Gets why the recipe cannot be crafted, or <see langword="null"/> when it can.</summary>
    public string? UnavailableReason { get; init; }
}

/// <summary>
/// Lists the recipes a character could craft, with material availability and the reason each
/// unavailable one is unavailable.
/// </summary>
/// <param name="CharacterId">The character whose bag and skill ranks to read.</param>
public record GetCraftableRecipesQuery(Guid CharacterId) : IRequest<IReadOnlyList<CraftableRecipeDto>>;

/// <summary>Handles <see cref="GetCraftableRecipesQuery"/>.</summary>
public class GetCraftableRecipesQueryHandler
    : IRequestHandler<GetCraftableRecipesQuery, IReadOnlyList<CraftableRecipeDto>>
{
    private const string SkillKeyPrefix = "Skill_";
    private const string SkillRankSuffix = "_Rank";

    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ICharacterRepository _characters;
    private readonly IRecipeRepository _recipes;
    private readonly IInventoryService _inventory;
    private readonly ILogger<GetCraftableRecipesQueryHandler> _logger;

    /// <summary>Initializes a new instance of <see cref="GetCraftableRecipesQueryHandler"/>.</summary>
    /// <param name="characters">Character repository.</param>
    /// <param name="recipes">Recipe catalog.</param>
    /// <param name="inventory">Inventory service, keyed by character name.</param>
    /// <param name="logger">Logger instance.</param>
    public GetCraftableRecipesQueryHandler(
        ICharacterRepository characters,
        IRecipeRepository recipes,
        IInventoryService inventory,
        ILogger<GetCraftableRecipesQueryHandler> logger)
    {
        _characters = characters;
        _recipes = recipes;
        _inventory = inventory;
        _logger = logger;
    }

    /// <summary>Returns the recipes, easiest first.</summary>
    /// <param name="request">The query carrying the character id.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>One entry per usable recipe; empty when the character is gone or the catalog is down.</returns>
    public async Task<IReadOnlyList<CraftableRecipeDto>> Handle(
        GetCraftableRecipesQuery request,
        CancellationToken cancellationToken)
    {
        var character = await _characters.GetByIdAsync(request.CharacterId, cancellationToken);
        if (character is null)
            return [];

        List<Recipe> recipes;
        try
        {
            recipes = await _recipes.GetAllAsync();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Recipe catalog read failed; returning an empty craft list.");
            return [];
        }

        var ranks = ReadSkillRanks(character.Attributes, character.Id);
        var craftable = new List<CraftableRecipeDto>();

        foreach (var recipe in recipes.Where(recipe => !string.IsNullOrWhiteSpace(recipe.OutputItemReference)))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var materials = new List<CraftMaterialDto>();
            foreach (var material in recipe.Materials)
            {
                if (string.IsNullOrWhiteSpace(material.ItemReference) || material.Quantity <= 0)
                    continue;

                materials.Add(new CraftMaterialDto
                {
                    ItemRef = material.ItemReference,
                    Required = material.Quantity,
                    Owned = await _inventory.GetItemCountAsync(character.Name, material.ItemReference),
                });
            }

            var skillId = string.IsNullOrWhiteSpace(recipe.RequiredSkill) ? null : recipe.RequiredSkill.Trim();
            var rank = skillId is not null && ranks.TryGetValue(skillId, out var stored) ? stored : 0;
            var reason = UnavailableReason(recipe, character.Level, skillId, rank, materials);

            craftable.Add(new CraftableRecipeDto
            {
                Slug = recipe.Slug,
                Name = recipe.Name,
                Description = recipe.Description,
                Category = recipe.Category,
                RequiredLevel = recipe.RequiredLevel,
                RequiredSkill = skillId,
                RequiredSkillLevel = recipe.RequiredSkillLevel,
                SkillRank = rank,
                RequiredStation = string.IsNullOrWhiteSpace(recipe.RequiredStation) ? null : recipe.RequiredStation,
                RequiredStationTier = recipe.RequiredStationTier,
                Materials = materials,
                OutputItemRef = recipe.OutputItemReference,
                OutputQuantity = recipe.OutputQuantity > 0 ? recipe.OutputQuantity : 1,
                ExperienceGained = recipe.ExperienceGained,
                Available = reason is null,
                UnavailableReason = reason,
            });
        }

        return craftable
            .OrderBy(recipe => recipe.Available ? 0 : 1)
            .ThenBy(recipe => recipe.RequiredLevel)
            .ThenBy(recipe => recipe.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// The first reason the recipe cannot be crafted, in the order the player would solve them:
    /// level, then skill rank, then materials.
    /// </summary>
    private static string? UnavailableReason(
        Recipe recipe,
        int characterLevel,
        string? skillId,
        int skillRank,
        IReadOnlyList<CraftMaterialDto> materials)
    {
        if (recipe.RequiredLevel > characterLevel)
            return $"Requires level {recipe.RequiredLevel}; you are level {characterLevel}.";

        if (skillId is not null && recipe.RequiredSkillLevel > skillRank)
            return $"Requires {skillId} rank {recipe.RequiredSkillLevel}; you are rank {skillRank}.";

        var shortages = materials
            .Where(material => material.Owned < material.Required)
            .Select(material => $"{material.Required - material.Owned} more {material.ItemRef}")
            .ToList();

        return shortages.Count > 0 ? $"Short of {string.Join(", ", shortages)}." : null;
    }

    private Dictionary<string, int> ReadSkillRanks(string? attributesJson, Guid characterId)
    {
        var ranks = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(attributesJson) || attributesJson == "{}")
            return ranks;

        Dictionary<string, int> attributes;
        try
        {
            attributes = JsonSerializer.Deserialize<Dictionary<string, int>>(attributesJson, JsonOptions)
                ?? new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to deserialise attributes for character {Id}; treating every skill rank as zero.",
                characterId);
            return ranks;
        }

        foreach (var (key, value) in attributes)
        {
            if (key.StartsWith(SkillKeyPrefix, StringComparison.OrdinalIgnoreCase) &&
                key.EndsWith(SkillRankSuffix, StringComparison.OrdinalIgnoreCase))
            {
                var skillId = key[SkillKeyPrefix.Length..^SkillRankSuffix.Length];
                if (skillId.Length > 0) ranks[skillId] = value;
            }
        }

        return ranks;
    }
}
