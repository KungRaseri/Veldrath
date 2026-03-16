using MediatR;
using RealmEngine.Core.Services;
using RealmEngine.Shared.Models;

namespace RealmEngine.Core.Features.Salvaging.Commands;

/// <summary>
/// Handler for salvaging items into scrap materials.
/// Implements type-based material mapping and skill-based yield rates.
/// </summary>
public class SalvageItemHandler : IRequestHandler<SalvageItemCommand, SalvageItemResult>
{
    private readonly RecipeDataService? _recipeCatalogLoader;

    /// <summary>
    /// Initializes a new instance of the SalvageItemHandler class.
    /// </summary>
    /// <param name="recipeCatalogLoader">Optional recipe catalog loader for recipe-based salvaging.</param>
    public SalvageItemHandler(RecipeDataService? recipeCatalogLoader = null)
    {
        _recipeCatalogLoader = recipeCatalogLoader;
    }

    /// <summary>
    /// Handles the salvage item request.
    /// </summary>
    public Task<SalvageItemResult> Handle(SalvageItemCommand request, CancellationToken cancellationToken)
    {
        var character = request.Character;
        var item = request.Item;
        var stationId = request.StationId;
        
        // Validate item type (can't salvage consumables or quest items)
        if (item.Type == ItemType.Consumable || item.Type == ItemType.QuestItem || item.Type == ItemType.Material)
        {
            return Task.FromResult(new SalvageItemResult
            {
                Success = false,
                Message = $"Cannot salvage {item.Type} items.",
                YieldRate = 0,
                ItemDestroyed = false
            });
        }
        
        // Determine crafting skill based on item type
        var skillName = GetCraftingSkillForItemType(item.Type);
        var skillLevel = character.Skills.TryGetValue(skillName, out var skill) ? skill.CurrentRank : 0;
        
        // Calculate yield rate: 40% base + (skill * 0.3%), max 100%
        var baseYield = 40.0;
        var skillBonus = skillLevel * 0.3;
        var yieldRate = Math.Min(baseYield + skillBonus, 100.0);
        
        // Determine scrap materials
        var scrapMaterials = GetScrapMaterials(item, yieldRate);
        
        return Task.FromResult(new SalvageItemResult
        {
            Success = true,
            Message = $"Salvaged {item.Name} at {yieldRate:F1}% yield rate. Recovered {string.Join(", ", scrapMaterials.Select(kvp => $"{kvp.Value}x {kvp.Key}"))}.",
            ScrapMaterials = scrapMaterials,
            SkillUsed = skillName,
            YieldRate = yieldRate,
            ItemDestroyed = true
        });
    }
    
    /// <summary>
    /// Get crafting skill name based on item type.
    /// </summary>
    private string GetCraftingSkillForItemType(ItemType itemType) => itemType switch
    {
        ItemType.Weapon => "Blacksmithing",
        ItemType.Shield => "Blacksmithing",
        ItemType.OffHand => "Blacksmithing",
        ItemType.Helmet => "Blacksmithing",
        ItemType.Shoulders => "Leatherworking",
        ItemType.Chest => "Blacksmithing",
        ItemType.Bracers => "Leatherworking",
        ItemType.Gloves => "Leatherworking",
        ItemType.Belt => "Leatherworking",
        ItemType.Legs => "Blacksmithing",
        ItemType.Boots => "Leatherworking",
        ItemType.Necklace => "Jewelcrafting",
        ItemType.Ring => "Jewelcrafting",
        _ => "Salvaging"
    };
    
    /// <summary>
    /// Get scrap materials from an item based on type.
    /// Implements two-tier system: salvage → scrap → refine (3:1) → materials.
    /// </summary>
    private Dictionary<string, int> GetScrapMaterials(Item item, double yieldRate)
    {
        var scrapMaterials = new Dictionary<string, int>();
        
        // Determine base scrap type based on item type
        var (primaryScrap, secondaryScrap) = GetScrapTypesForItem(item.Type);
        
        // Calculate base scrap yield
        // Weapons/armor typically return 3-5 scraps at 100% yield
        var baseYield = item.Rarity switch
        {
            ItemRarity.Common => 3,
            ItemRarity.Uncommon => 4,
            ItemRarity.Rare => 5,
            ItemRarity.Epic => 7,
            ItemRarity.Legendary => 10,
            _ => 3
        };
        
        // Apply upgrade level bonus (bonus scraps for upgraded items)
        var upgradeBonus = item.UpgradeLevel;
        
        // Calculate actual yield with skill-based rate
        var totalYield = (int)Math.Ceiling((baseYield + upgradeBonus) * (yieldRate / 100.0));
        
        // Ensure minimum of 1 scrap if yield rate allows
        if (totalYield < 1 && yieldRate >= 10)
        {
            totalYield = 1;
        }
        
        // Distribute between primary and secondary scraps
        // Always give at least 1 secondary if there's a secondary type
        var primaryAmount = !string.IsNullOrEmpty(secondaryScrap) 
            ? Math.Max(1, (int)Math.Ceiling(totalYield * 0.7))
            : totalYield;
        var secondaryAmount = !string.IsNullOrEmpty(secondaryScrap) 
            ? Math.Max(1, totalYield - primaryAmount)
            : 0;
        
        if (primaryAmount > 0)
        {
            scrapMaterials[primaryScrap] = primaryAmount;
        }
        
        if (secondaryAmount > 0 && !string.IsNullOrEmpty(secondaryScrap))
        {
            scrapMaterials[secondaryScrap] = secondaryAmount;
        }
        
        return scrapMaterials;
    }
    
    /// <summary>
    /// Get primary and secondary scrap types for an item.
    /// Returns tuple of (primaryScrap, secondaryScrap).
    /// </summary>
    private (string primary, string secondary) GetScrapTypesForItem(ItemType itemType) => itemType switch
    {
        // Metal items
        ItemType.Weapon => ("Scrap Metal", "Scrap Wood"),
        ItemType.Shield => ("Scrap Metal", "Scrap Wood"),
        ItemType.Helmet => ("Scrap Metal", ""),
        ItemType.Chest => ("Scrap Metal", "Scrap Leather"),
        ItemType.Legs => ("Scrap Metal", ""),
        
        // Leather items
        ItemType.Shoulders => ("Scrap Leather", "Scrap Cloth"),
        ItemType.Bracers => ("Scrap Leather", ""),
        ItemType.Gloves => ("Scrap Leather", ""),
        ItemType.Belt => ("Scrap Leather", ""),
        ItemType.Boots => ("Scrap Leather", ""),
        
        // Jewelry
        ItemType.Necklace => ("Gemstone Fragments", "Scrap Metal"),
        ItemType.Ring => ("Gemstone Fragments", "Scrap Metal"),
        
        // Other
        ItemType.OffHand => ("Scrap Wood", "Scrap Cloth"),
        
        _ => ("Scrap Metal", "")
    };
}
