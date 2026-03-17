using MediatR;
using RealmEngine.Core.Features.Crafting.Services;
using RealmEngine.Shared.Models;
using Microsoft.Extensions.Logging;

namespace RealmEngine.Core.Features.Crafting.Commands;

/// <summary>
/// Handles crafting an item from a recipe.
/// Validates materials, consumes them, creates the item, and awards skill XP.
/// </summary>
public class CraftRecipeHandler : IRequestHandler<CraftRecipeCommand, CraftRecipeResult>
{
    private readonly CraftingService _craftingService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CraftRecipeHandler"/> class.
    /// </summary>
    /// <param name="craftingService">The crafting service.</param>
    public CraftRecipeHandler(CraftingService craftingService)
    {
        _craftingService = craftingService ?? throw new ArgumentNullException(nameof(craftingService));
    }

    /// <summary>
    /// Handles the craft recipe command and returns the result.
    /// </summary>
    /// <param name="request">The craft recipe command.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation, containing the crafting result.</returns>
    public async Task<CraftRecipeResult> Handle(CraftRecipeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Validate that the character can craft this recipe
            if (!_craftingService.CanCraftRecipe(request.Character, request.Recipe, out var failureReason))
            {
                _logger.LogWarning("Crafting failed for {CharacterName}: {Reason}", 
                    request.Character.Name, failureReason);
                
                return new CraftRecipeResult
                {
                    Success = false,
                    Message = failureReason
                };
            }

            // Validate station matches recipe requirements (case-insensitive)
            if (!request.Station.Id.Equals(request.Recipe.RequiredStation, StringComparison.OrdinalIgnoreCase) && 
                !request.Station.Slug.Equals(request.Recipe.RequiredStation, StringComparison.OrdinalIgnoreCase) &&
                !request.Station.Name.Equals(request.Recipe.RequiredStation, StringComparison.OrdinalIgnoreCase))
            {
                return new CraftRecipeResult
                {
                    Success = false,
                    Message = $"Recipe requires {request.Recipe.RequiredStation}, but using {request.Station.Name}"
                };
            }

            // Calculate item quality
            var quality = _craftingService.CalculateQuality(request.Character, request.Recipe);
            
            _logger.LogInformation("Crafting {RecipeName} for {CharacterName} with quality {Quality}", 
                request.Recipe.Name, request.Character.Name, quality);

            // Consume materials
            var materialsConsumed = ConsumeMaterials(request.Character, request.Recipe);

            // Create the crafted item
            var craftedItem = CreateCraftedItem(request.Recipe, quality);

            // Add the item to the character's inventory
            request.Character.Inventory ??= new List<Item>();
            request.Character.Inventory.Add(craftedItem);

            // Award skill XP
            var skillName = GetCraftingSkillForStation(request.Recipe.RequiredStation);
            var xpGained = AwardSkillXp(request.Character, skillName, request.Recipe.ExperienceGained);

            _logger.LogInformation("Successfully crafted {ItemName} (quality {Quality}) for {CharacterName}. {SkillName} +{XpGained} XP",
                craftedItem.Name, quality, request.Character.Name, skillName, xpGained);

            return await Task.FromResult(new CraftRecipeResult
            {
                Success = true,
                Message = $"Successfully crafted {craftedItem.Name} (quality {quality}%)",
                CraftedItem = craftedItem,
                Quality = quality,
                SkillXpGained = xpGained,
                MaterialsConsumed = materialsConsumed
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error crafting recipe {RecipeName} for {CharacterName}", 
                request.Recipe.Name, request.Character.Name);
            
            return new CraftRecipeResult
            {
                Success = false,
                Message = $"Failed to craft item: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Consumes materials from the character's inventory.
    /// </summary>
    private List<string> ConsumeMaterials(Character character, Recipe recipe)
    {
        var consumed = new List<string>();

        // Process specific materials first, then wildcards
        // This prevents wildcards from consuming items needed for specific requirements
        var specificMaterials = recipe.Materials.Where(m => !m.ItemReference.EndsWith(":*")).ToList();
        var wildcardMaterials = recipe.Materials.Where(m => m.ItemReference.EndsWith(":*")).ToList();
        var allMaterials = specificMaterials.Concat(wildcardMaterials);

        foreach (var material in allMaterials)
        {
            // Extract item name from reference (e.g., "@items/materials/ores:iron-ore" → "iron-ore")
            var itemName = material.ItemReference.Contains(':') 
                ? material.ItemReference.Split(':')[1].Split('?')[0]  // Handle optional "?" suffix
                : material.ItemReference;
            
            var remaining = material.Quantity;
            
            // Find and remove materials from inventory
            for (int i = character.Inventory!.Count - 1; i >= 0 && remaining > 0; i--)
            {
                var item = character.Inventory[i];
                
                // Match against extracted item name OR full reference OR wildcard
                if (item.Id == itemName || item.Name == itemName || 
                    item.Id == material.ItemReference || item.Name == material.ItemReference ||
                    itemName == "*")  // Wildcard match
                {
                    // Remove entire item (assume 1 item = 1 unit for now)
                    character.Inventory.RemoveAt(i);
                    remaining--;
                    consumed.Add($"1x {item.Name}");
                }
            }
        }

        return consumed;
    }

    /// <summary>
    /// Creates a new item instance from the recipe with the specified quality.
    /// </summary>
    private Item CreateCraftedItem(Recipe recipe, int quality)
    {
        // Note: Recipe.OutputItemReference contains the item reference (e.g., @items/weapons/swords:iron-sword)
        // ItemGenerator.GenerateFromReference() can be used for full template loading
        // Current implementation creates basic items with recipe-defined properties
        
        var item = new Item
        {
            Id = Guid.NewGuid().ToString(),
            Name = recipe.Name.Replace("Recipe: ", ""),
            Description = $"A crafted item of {quality}% quality",
            Type = DetermineItemType(recipe),
            Rarity = DetermineRarity(quality),
            Traits = new Dictionary<string, TraitValue>()
        };

        // Apply quality-based stat bonuses
        ApplyQualityBonuses(item, quality);

        return item;
    }

    /// <summary>
    /// Determines the item type based on the recipe's output and required station.
    /// </summary>
    private ItemType DetermineItemType(Recipe recipe)
    {
        return recipe.RequiredStation.ToLowerInvariant() switch
        {
            "anvil" or "blacksmith_forge" => ItemType.Weapon,
            "alchemytable" or "alchemy_table" => ItemType.Consumable,
            "enchantingtable" or "enchanting_altar" => ItemType.EnchantmentScroll,
            "cookingfire" or "cooking_fire" => ItemType.Consumable,
            "workbench" => ItemType.Material,
            "loom" => ItemType.Material,
            "tanningrack" or "tanning_rack" => ItemType.Material,
            "jewelrybench" or "jewelry_bench" => ItemType.Material,
            _ => ItemType.Material
        };
    }

    /// <summary>
    /// Determines the item rarity based on quality (0-100).
    /// </summary>
    private ItemRarity DetermineRarity(int quality)
    {
        return quality switch
        {
            >= 90 => ItemRarity.Legendary,
            >= 75 => ItemRarity.Epic,
            >= 60 => ItemRarity.Rare,
            >= 40 => ItemRarity.Uncommon,
            _ => ItemRarity.Common
        };
    }

    /// <summary>
    /// Applies stat bonuses to the item based on quality.
    /// Higher quality items get better stats.
    /// </summary>
    private void ApplyQualityBonuses(Item item, int quality)
    {
        // Quality affects base stats
        var qualityMultiplier = quality / 100.0;
        
        // Add bonuses based on item type
        switch (item.Type)
        {
            case ItemType.Weapon:
                item.Traits["Strength"] = new TraitValue((int)(5 * qualityMultiplier), TraitType.Number);
                item.Traits["Dexterity"] = new TraitValue((int)(3 * qualityMultiplier), TraitType.Number);
                break;
            
            case ItemType.Material:
                // Materials don't have stat bonuses
                break;
        }
    }

    /// <summary>
    /// Awards skill XP to the character and handles level-ups.
    /// </summary>
    private int AwardSkillXp(Character character, string skillName, int baseXp)
    {
        character.Skills ??= new Dictionary<string, CharacterSkill>();

        if (!character.Skills.ContainsKey(skillName))
        {
            character.Skills[skillName] = new CharacterSkill
            {
                SkillId = skillName,
                CurrentRank = 0,
                CurrentXP = 0
            };
        }

        var skill = character.Skills[skillName];
        skill.CurrentXP += baseXp;

        // Check for level-ups (simplified: 100 XP per level)
        var xpPerLevel = 100;
        var levelUps = 0;
        
        while (skill.CurrentXP >= xpPerLevel && skill.CurrentRank < 100)
        {
            skill.CurrentXP -= xpPerLevel;
            skill.CurrentRank++;
            levelUps++;
        }

        if (levelUps > 0)
        {
            _logger.LogInformation("{CharacterName} gained {LevelUps} {SkillName} level(s)! Now level {NewLevel}",
                character.Name, levelUps, skillName, skill.CurrentRank);
        }

        return baseXp;
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
}
