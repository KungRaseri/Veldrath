using RealmEngine.Shared.Models;
using RealmEngine.Core.Services;
using RealmEngine.Core.Services.Budget;
using RealmEngine.Core.Generators.Modern;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Crafting.Services;

/// <summary>
/// Service for managing crafting operations.
/// Validates recipes, checks materials, and determines crafting outcomes.
/// </summary>
public class CraftingService
{
    private readonly RecipeDataService _recipeCatalogLoader;
    private readonly BudgetHelperService? _budgetHelper;
    private readonly ItemGenerator? _itemGenerator;
    private readonly Random _random = new();
    private readonly ILogger<CraftingService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="CraftingService"/> class.
    /// </summary>
    /// <param name="recipeCatalogLoader">The recipe catalog loader.</param>
    /// <param name="logger">The logger.</param>
    /// <param name="budgetHelper">Optional budget helper for quality calculations.</param>
    /// <param name="itemGenerator">Optional item generator for procedural crafted items.</param>
    public CraftingService(
        RecipeDataService recipeCatalogLoader,
        ILogger<CraftingService> logger,
        BudgetHelperService? budgetHelper = null,
        ItemGenerator? itemGenerator = null)
    {
        _recipeCatalogLoader = recipeCatalogLoader ?? throw new ArgumentNullException(nameof(recipeCatalogLoader));
        _logger = logger;
        _budgetHelper = budgetHelper;
        _itemGenerator = itemGenerator;
    }

    /// <summary>
    /// Gets all recipes available to a character based on their skill levels.
    /// </summary>
    /// <param name="character">The character to check.</param>
    /// <param name="stationId">Optional crafting station ID filter.</param>
    /// <returns>List of recipes the character can see (not necessarily craft).</returns>
    public List<Recipe> GetAvailableRecipes(Character character, string? stationId = null)
    {
        if (character == null)
            throw new ArgumentNullException(nameof(character));

        var allRecipes = _recipeCatalogLoader.LoadAllRecipes();
        var availableRecipes = new List<Recipe>();

        foreach (var recipe in allRecipes)
        {
            // Filter by station if specified (case-insensitive)
            if (!string.IsNullOrEmpty(stationId) && 
                !recipe.RequiredStation.Equals(stationId, StringComparison.OrdinalIgnoreCase))
                continue;

            // Check if character has unlocked this recipe
            if (IsRecipeUnlocked(character, recipe))
            {
                availableRecipes.Add(recipe);
            }
        }

        return availableRecipes;
    }

    /// <summary>
    /// Checks if a character can craft a specific recipe.
    /// </summary>
    /// <param name="character">The character attempting to craft.</param>
    /// <param name="recipe">The recipe to craft.</param>
    /// <param name="failureReason">Output parameter with failure reason if crafting is not possible.</param>
    /// <returns>True if the character can craft the recipe, false otherwise.</returns>
    public bool CanCraftRecipe(Character character, Recipe recipe, out string failureReason)
    {
        if (character == null)
        {
            failureReason = "Character cannot be null";
            return false;
        }

        if (recipe == null)
        {
            failureReason = "Recipe cannot be null";
            return false;
        }

        // Check skill level requirement FIRST (for better error messages)
        var craftingSkill = GetCraftingSkillForStation(recipe.RequiredStation);
        var skill = character.Skills?.GetValueOrDefault(craftingSkill);
        var characterSkillLevel = skill?.CurrentRank ?? 0;

        if (characterSkillLevel < recipe.RequiredSkillLevel)
        {
            failureReason = $"Requires {craftingSkill} skill level {recipe.RequiredSkillLevel} (current: {characterSkillLevel})";
            return false;
        }

        // Then check if recipe is unlocked (for Trainer/Quest/Discovery)
        if (!IsRecipeUnlocked(character, recipe))
        {
            failureReason = $"Recipe '{recipe.Name}' is not unlocked";
            return false;
        }

        // Check materials
        if (!ValidateMaterials(character, recipe, out var materialFailure))
        {
            failureReason = materialFailure;
            return false;
        }

        failureReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Validates that the character has all required materials for a recipe.
    /// </summary>
    /// <param name="character">The character to check.</param>
    /// <param name="recipe">The recipe requiring materials.</param>
    /// <param name="failureReason">Output parameter with missing material details.</param>
    /// <returns>True if all materials are available, false otherwise.</returns>
    public bool ValidateMaterials(Character character, Recipe recipe, out string failureReason)
    {
        if (character.Inventory == null)
        {
            failureReason = "Character has no inventory";
            return false;
        }

        foreach (var material in recipe.Materials)
        {
            var availableCount = CountMaterialInInventory(character, material.ItemReference);

            if (availableCount < material.Quantity)
            {
                failureReason = $"Missing {material.ItemReference}: need {material.Quantity}, have {availableCount}";
                return false;
            }
        }

        failureReason = string.Empty;
        return true;
    }

    /// <summary>
    /// Attempts to craft an item from a recipe, with variance and failure handling.
    /// Failure reduces quality instead of destroying materials.
    /// </summary>
    /// <param name="character">The character attempting to craft.</param>
    /// <param name="recipe">The recipe to craft.</param>
    /// <param name="consumeMaterials">Whether to consume materials from inventory.</param>
    /// <returns>Crafting result with item and outcome details.</returns>
    public async Task<CraftingResult> CraftItemAsync(Character character, Recipe recipe, bool consumeMaterials = true)
    {
        if (!CanCraftRecipe(character, recipe, out var failureReason))
        {
            return new CraftingResult
            {
                Success = false,
                FailureReason = failureReason
            };
        }

        var craftingSkill = GetCraftingSkillForStation(recipe.RequiredStation);
        var skill = character.Skills?.GetValueOrDefault(craftingSkill);
        var characterSkillLevel = skill?.CurrentRank ?? 0;

        // Calculate success and critical chances
        int successChance, criticalChance;
        if (_budgetHelper != null)
        {
            successChance = _budgetHelper.GetCraftingSuccessChance(characterSkillLevel, recipe.RequiredSkillLevel);
            criticalChance = _budgetHelper.GetCraftingCriticalChance(characterSkillLevel, recipe.RequiredSkillLevel);
        }
        else
        {
            // Fallback if no budget helper
            var skillDiff = characterSkillLevel - recipe.RequiredSkillLevel;
            successChance = Math.Clamp(50 + (skillDiff * 2), 5, 99);
            criticalChance = Math.Clamp(5 + (Math.Max(0, skillDiff) / 10), 5, 20);
        }

        // Roll for success
        var successRoll = _random.Next(1, 101);
        var criticalRoll = _random.Next(1, 101);
        
        var isSuccess = successRoll <= successChance;
        var isCritical = isSuccess && criticalRoll <= criticalChance;

        // Determine failure severity if failed
        int failureSeverity = 0;
        int qualityReduction = 0;
        int materialRefundPercent = 0;

        if (!isSuccess && _budgetHelper != null)
        {
            failureSeverity = _budgetHelper.GetCraftingFailureSeverity(successRoll, successChance);
            qualityReduction = _budgetHelper.GetQualityReductionForFailure(failureSeverity);
            materialRefundPercent = _budgetHelper.GetMaterialRefundPercentage(failureSeverity);
        }
        else if (!isSuccess)
        {
            // Fallback if no budget helper
            var margin = successRoll - successChance;
            failureSeverity = margin <= 10 ? 1 : margin <= 30 ? 2 : 3;
            qualityReduction = failureSeverity;
            materialRefundPercent = failureSeverity == 3 ? 50 : 0;
        }

        // Calculate quality tier
        int qualityBonus;
        if (_budgetHelper != null)
        {
            qualityBonus = _budgetHelper.GetCraftingQualityBonus(characterSkillLevel, recipe.RequiredSkillLevel, isCritical);
        }
        else
        {
            qualityBonus = (characterSkillLevel - recipe.RequiredSkillLevel) / 10;
            if (isCritical) qualityBonus++;
            qualityBonus = Math.Clamp(qualityBonus, 0, 3);
        }

        // Apply quality reduction on failure
        if (!isSuccess)
        {
            qualityBonus = Math.Max(0, qualityBonus - qualityReduction);
            
            var severityText = failureSeverity switch
            {
                1 => "marginal failure",
                2 => "moderate failure",
                3 => "critical failure",
                _ => "failure"
            };
            
            _logger.LogInformation("Crafting {Severity} for {Character} (roll={Roll} vs {Chance}%), producing quality {Quality}", 
                severityText, character.Name, successRoll, successChance, qualityBonus);
        }

        // Handle materials and refunds
        var refundedMaterials = new List<(string ItemReference, int Quantity)>();
        
        if (consumeMaterials)
        {
            foreach (var material in recipe.Materials)
            {
                var consumedQuantity = material.Quantity;
                var refundQuantity = 0;

                // Calculate refund for critical failures
                if (materialRefundPercent > 0)
                {
                    refundQuantity = (int)Math.Ceiling(material.Quantity * (materialRefundPercent / 100.0));
                    consumedQuantity = material.Quantity - refundQuantity;
                    
                    if (refundQuantity > 0)
                    {
                        refundedMaterials.Add((material.ItemReference, refundQuantity));
                        _logger.LogInformation("Critical failure: Refunded {Quantity}x {Material} ({Percent}%)", 
                            refundQuantity, material.ItemReference, materialRefundPercent);
                    }
                }

                // Remove consumed materials
                RemoveMaterialsFromInventory(character, material.ItemReference, consumedQuantity);
            }
        }

        // Create the crafted item
        var craftedItem = await CreateItemFromRecipeAsync(recipe, qualityBonus, character);

        return new CraftingResult
        {
            Success = true,
            Item = craftedItem,
            WasCritical = isCritical,
            QualityBonus = qualityBonus,
            ActualQuality = (ItemRarity)Math.Clamp((int)recipe.MinQuality + qualityBonus, 
                (int)recipe.MinQuality, (int)recipe.MaxQuality),
            FailureSeverity = failureSeverity,
            RefundedMaterials = refundedMaterials
        };
    }

    /// <summary>
    /// Calculates the quality of a crafted item based on character skill and recipe parameters.
    /// </summary>
    /// <param name="character">The character crafting the item.</param>
    /// <param name="recipe">The recipe being crafted.</param>
    /// <returns>Quality value (0-100).</returns>
    public int CalculateQuality(Character character, Recipe recipe)
    {
        if (character == null || recipe == null)
            return (int)(recipe?.MinQuality ?? ItemRarity.Common);

        var craftingSkill = GetCraftingSkillForStation(recipe.RequiredStation);
        var skill = character.Skills?.GetValueOrDefault(craftingSkill);
        var characterSkillLevel = skill?.CurrentRank ?? 0;

        // Calculate rarity based on skill level
        var minRarity = (int)recipe.MinQuality;
        var maxRarity = (int)recipe.MaxQuality;
        var rarityRange = maxRarity - minRarity;

        // Add skill bonus (skill above requirement improves rarity chance)
        var skillOverRequirement = characterSkillLevel - recipe.RequiredSkillLevel;
        var rarityBonus = skillOverRequirement > 0 ? Math.Min(skillOverRequirement / 10, rarityRange) : 0;

        // Random variance within rarity range
        var finalRarity = minRarity + rarityBonus + _random.Next(0, Math.Max(1, rarityRange - rarityBonus + 1));

        // Clamp to valid rarity range
        return Math.Clamp(finalRarity, minRarity, maxRarity);
    }

    /// <summary>
    /// Creates an item from a recipe with quality bonus applied.
    /// Uses ItemGenerator if available for full procedural generation, otherwise creates basic item.
    /// </summary>
    private async Task<Item> CreateItemFromRecipeAsync(Recipe recipe, int qualityBonus, Character character)
    {
        var baseRarity = (int)recipe.MinQuality + qualityBonus;
        baseRarity = Math.Clamp(baseRarity, (int)recipe.MinQuality, (int)recipe.MaxQuality);
        var rarity = (ItemRarity)baseRarity;

        // Try to use ItemGenerator if available and output is a JSON reference
        if (_itemGenerator != null && recipe.OutputItemReference.StartsWith("@"))
        {
            try
            {
                _logger.LogDebug("Using ItemGenerator for crafted item: {Ref}", recipe.OutputItemReference);

                // Parse reference format: @items/weapons/swords:iron-longsword
                var parts = recipe.OutputItemReference.TrimStart('@').Split(':');
                if (parts.Length == 2)
                {
                    var pathParts = parts[0].Split('/');
                    var category = pathParts.Length >= 2 ? pathParts[1] : pathParts[0]; // e.g., "weapons"
                    var itemName = parts[1]; // e.g., "iron-longsword"

                    var item = await _itemGenerator.GenerateItemByNameAsync(category, itemName, hydrate: true);

                    if (item != null)
                    {
                        // Override rarity with crafted quality
                        item.Rarity = rarity;

                        _logger.LogInformation("Generated procedural crafted item: {Name} ({Rarity})",
                            item.Name, item.Rarity);
                        return item;
                    }
                }

                _logger.LogWarning("Could not parse item reference {Ref}, falling back to basic item",
                    recipe.OutputItemReference);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating item from reference {Ref}, falling back to basic item",
                    recipe.OutputItemReference);
            }
        }

        // Fallback: Create basic item
        return new Item
        {
            Id = recipe.OutputItemReference,
            Name = recipe.Name,
            Description = $"Crafted {recipe.Name}",
            Type = DetermineItemType(recipe.Category),
            Rarity = rarity,
            Price = recipe.Materials.Sum(m => m.Quantity * 10), // Estimate
            StackSize = 1,
            IsStackable = false
        };
    }

    /// <summary>
    /// Determines item type from recipe category.
    /// </summary>
    private ItemType DetermineItemType(string category)
    {
        return category.ToLowerInvariant() switch
        {
            "weapons" => ItemType.Weapon,
            "armor" => ItemType.Chest,
            "shields" => ItemType.Shield,
            "consumables" => ItemType.Consumable,
            _ => ItemType.Consumable
        };
    }

    /// <summary>
    /// Removes materials from character inventory.
    /// </summary>
    private void RemoveMaterialsFromInventory(Character character, string itemReference, int quantity)
    {
        if (character.Inventory == null) return;

        var itemName = itemReference.Contains(':')
            ? itemReference.Split(':')[1].Split('?')[0]
            : itemReference;

        var removed = 0;
        character.Inventory.RemoveAll(item =>
        {
            if (removed >= quantity) return false;
            if (item.Id == itemName || item.Name == itemName ||
                item.Id == itemReference || item.Name == itemReference)
            {
                removed++;
                return true;
            }
            return false;
        });
    }

    /// <summary>
    /// Checks if a recipe is unlocked for a character based on unlock method.
    /// </summary>
    private bool IsRecipeUnlocked(Character character, Recipe recipe)
    {
        switch (recipe.UnlockMethod)
        {
            case RecipeUnlockMethod.SkillLevel:
                // Recipe is unlocked when character reaches the required skill level
                var craftingSkill = GetCraftingSkillForStation(recipe.RequiredStation);
                var skill = character.Skills?.GetValueOrDefault(craftingSkill);
                var skillLevel = skill?.CurrentRank ?? 0;
                return skillLevel >= recipe.RequiredSkillLevel;

            case RecipeUnlockMethod.Trainer:
                // Recipe must be explicitly learned (check character's learned recipes)
                return character.LearnedRecipes?.Contains(recipe.Id) ?? false;

            case RecipeUnlockMethod.QuestReward:
                // Quest-unlocked recipes require quest completion
                return character.LearnedRecipes?.Contains(recipe.Id) ?? false;

            case RecipeUnlockMethod.Discovery:
                // Discovery recipes must be found through experimentation
                return character.LearnedRecipes?.Contains(recipe.Id) ?? false;

            default:
                return true;
        }
    }

    /// <summary>
    /// Gets the skill name associated with a crafting station.
    /// </summary>
    private string GetCraftingSkillForStation(string station)
    {
        return station.ToLowerInvariant() switch
        {
            "anvil" or "blacksmith_forge" => "Blacksmithing",
            "alchemytable" or "alchemy_table" => "Alchemy",
            "enchantingtable" or "enchanting_altar" => "Enchanting",
            "cookingfire" or "cooking_fire" => "Cooking",
            "workbench" => "Carpentry",
            "loom" => "Tailoring",
            "tanningrack" or "tanning_rack" => "Leatherworking",
            "jewelrybench" or "jewelry_bench" => "Jewelcrafting",
            _ => "Crafting"
        };
    }

    /// <summary>
    /// Counts how many of a specific material the character has in their inventory.
    /// </summary>
    private int CountMaterialInInventory(Character character, string itemReference)
    {
        if (character.Inventory == null)
            return 0;

        // Extract item name from reference (e.g., "@items/materials/ores:iron-ore" → "iron-ore")
        var itemName = itemReference.Contains(':') 
            ? itemReference.Split(':')[1].Split('?')[0]  // Handle optional "?" suffix
            : itemReference;

        // For wildcard references like "@items/materials/organics:*", match any item
        // This is a simplified check - in production, you'd query the catalog
        if (itemName == "*")
        {
            // Match items that could be materials (simple heuristic)
            return character.Inventory.Count(item => 
                !string.IsNullOrEmpty(item.Id) || !string.IsNullOrEmpty(item.Name));
        }

        return character.Inventory
            .Count(item => item.Id == itemName || item.Name == itemName || 
                          item.Id == itemReference || item.Name == itemReference);
    }
}
