# Crafting System

**Status**: ✅ **100% COMPLETE** - Full crafting ecosystem with enhancements!  
**Last Updated**: January 11, 2026 03:00 UTC

## Implementation Progress

### ✅ Phase 1-5 Complete (100%)
- **CraftingService** - All validation methods implemented and tested ✅
- **CraftRecipeHandler** - Full execution pipeline with material consumption, item creation, XP awards
- **RecipeDataService** - Loads recipes from the content database
- **Recipe Learning System** - LearnRecipeCommand for trainer/quest rewards
- **Recipe Discovery** - DiscoverRecipeCommand with skill-based chance (5% base + 0.5% per level)
- **Known Recipes Query** - GetKnownRecipesQuery with filtering and material validation
- **Materials System** - Structured material catalog with 10 material families
  - **Material families**: wood, leather, gems, ores, ingots, reagents, organics, essences, scraps, stone
  - **Two-tier crafting**: Raw → Refined → Component (oak logs → planks → handles)
  - **Materials** across all categories with proper traits and rarity weighting
- **Recipe Validation** - Skill checks, material validation, unlock verification
- **Quality Calculation** - Skill-based quality with variance
- **Material Consumption** - Removes required materials from inventory on craft
- **Test Coverage** - ✅ Passing
- **Integration Tests** - Full end-to-end crafting workflow verified

### ✅ Core Features Implemented
- ✅ **Recipe Execution** - CraftRecipeCommand creates items with quality bonuses
- ✅ **Material Consumption** - Removes items from inventory with wildcard matching
- ✅ **Skill Progression** - Awards XP based on recipe difficulty
- ✅ **Recipe Unlocking** - Four unlock methods: SkillLevel (auto), Trainer, Quest, Discovery
- ✅ **Station Validation** - Verifies correct station and tier

### ✅ Enhancement Systems Complete (100%)
- ✅ **[Enchanting System](enchanting-system.md)** — scroll-based magical property application
- ✅ **[Upgrading System](upgrading-system.md)** — essence-based stat amplification (+1 to +10)
- ✅ **[Socketing System](socketing-system.md)** — gem, rune, crystal, and orb slots
- ✅ **Salvaging System** - Recycle unwanted items
  - Skill-based yield: 40% base + (skill * 0.3%), capped at 100%
  - Rarity scaling: Common=3, Legendary=10 base scraps
  - Type-based materials: Weapons→Metal+Wood, Armor→Leather/Metal, Jewelry→Gems+Metal
  - Upgrade bonus: +1 scrap per upgrade level
  - 3:1 refinement ratio (3 scraps → 1 regular material)

## Design Decisions (January 11, 2026)

### ✅ Finalized Architecture

1. **Stat System Consolidation**: Merge legacy stats into Traits system
   - Remove duplicate BonusStrength/Dexterity/etc. fields
   - Use Traits dictionary exclusively: `{ "bonusStrength": 5, "bonusDexterity": 3 }`
   - Item.GetTotalTraits() already implements unified merging

2. **Crafting Quality**: Always succeeds, skill affects output quality
   - Low skill: Common/Uncommon items
   - High skill: Rare/Epic items
   - Critical success (5% chance): +1 quality tier

3. **Material Sources**: All methods implemented
   - Enemy drops (loot tables)
   - Shop purchases (material vendors)
   - Gathering nodes (exploration system)

---

## Overview

The Crafting System allows players to create, modify, and enhance equipment using gathered materials and recipes, providing alternative progression and customization beyond found loot.

## Core Components

### Crafting Stations
- **Blacksmith Forge**: Weapons and armor crafting/upgrading
- **Alchemy Lab**: Potions, elixirs, and consumables
- **Enchanting Altar**: Magical property application
- **Tinker's Workbench**: Utility items and accessories *(Future)*

### Materials System
- **Material Sources**: Gathering, enemy drops, salvaging, vendors, quests
- **Material Types**: Metals, cloth/leather, herbs/reagents, gems/crystals, creature parts
- **Material Quality**: Common to Legendary tiers affecting crafted item quality

### Recipe System
- **Recipe Acquisition**: Starting recipes, discovery, trainers, quests, loot
- **Recipe Types**: Equipment, consumables, enchantments, upgrades
- **Recipe Requirements**: Materials, station tier, level requirements

### Crafting Process
1. Access crafting station
2. Select recipe
3. Verify materials
4. Confirm crafting
5. Consume materials and create item

### Equipment Enhancement
- **[Enchanting System](enchanting-system.md)** — Apply magical properties using crafted scrolls (scroll-based, skill-gated success rates)
- **[Upgrading System](upgrading-system.md)** — Amplify item stats with essences (+1 to +10, hybrid safe/risky zones)
- **[Socketing System](socketing-system.md)** — Gem, rune, crystal, and orb slots with link bonuses
- **Modification System**: Reforge stats *(Future)*

### Crafting Skills *(Future)*
- **Skill Progression**: Separate skills per profession
- **Specialization**: Focus on specific craft types for bonuses
- **Master Crafters**: Higher skill = superior items

### Economic Integration
- **Crafting Value**: Sell crafted items for profit
- **Material Market**: Buy/sell materials with dynamic pricing *(Future)*

## Key Features

- **Build Synergy**: Craft equipment tailored to builds
- **Self-Sufficiency**: Reduce shop reliance
- **Collection Goals**: Gather recipes and rare materials
- **Strategic Depth**: Optimization beyond random drops

## Related Systems

- [Inventory System](inventory-system.md) - Materials and crafted items
- [Exploration System](exploration-system.md) - Resource gathering
- [Quest System](quest-system.md) - Recipe and material rewards
- [Shop System](shop-system-integration.md) - Material purchasing
- [Enchanting System](enchanting-system.md) - Post-craft magical property application
- [Upgrading System](upgrading-system.md) - Essence-based stat amplification
- [Socketing System](socketing-system.md) - Gem, rune, crystal, and orb slots

---

### Backend Architecture

#### Commands & Queries (MediatR)

**Commands:**
```csharp
// RealmEngine.Core/Features/Crafting/Commands/
CraftItemCommand(string RecipeId, string CharacterId)
UpgradeStationCommand(string StationId, string CharacterId)
LearnRecipeCommand(string RecipeId, string CharacterId)
```

**Queries:**
```csharp
// RealmEngine.Core/Features/Crafting/Queries/
GetAvailableRecipesQuery(string StationType, int? PlayerLevel)
GetCraftingStationsQuery(string? LocationId)
GetRecipeDetailsQuery(string RecipeId)
GetKnownRecipesQuery(string CharacterId)
```

**Results:**
```csharp
public record CraftItemResult
{
    public bool Success { get; init; }
    public Item? CraftedItem { get; init; }
    public int ExperienceGained { get; init; }
    public bool SkillLevelUp { get; init; }
    public List<string> MaterialsConsumed { get; init; } = new();
    public string? ErrorMessage { get; init; }
}

public record RecipeInfo
{
    public string Id { get; init; }
    public string Name { get; init; }
    public string Description { get; init; }
    public List<MaterialRequirement> Materials { get; init; } = new();
    public int RequiredLevel { get; init; }
    public string RequiredStation { get; init; }
    public int CraftingTime { get; init; }
    public bool CanCraft { get; init; }
    public string? CannotCraftReason { get; init; }
}

public record MaterialRequirement
{
    public string ItemId { get; init; }
    public string ItemName { get; init; }
    public int Required { get; init; }
    public int Available { get; init; }
    public bool HasEnough { get; init; }
}
```

### Data Models

**Recipe Model** (`RealmEngine.Shared/Models/Recipe.cs`)
```csharp
public class Recipe
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public int RequiredLevel { get; set; }
    public string RequiredStation { get; set; } = string.Empty;
    public int RequiredStationTier { get; set; } = 1;
    public int CraftingTime { get; set; } // seconds
    
    // Materials needed
    public List<RecipeMaterial> Materials { get; set; } = new();
    
    // Output item
    public string OutputItemReference { get; set; } = string.Empty;
    public int OutputQuantity { get; set; } = 1;
    public ItemRarity MinQuality { get; set; } = ItemRarity.Common;
    public ItemRarity MaxQuality { get; set; } = ItemRarity.Uncommon;
    
    // Progression
    public int ExperienceGained { get; set; }
    public string? RequiredSkill { get; set; }
    public int RequiredSkillLevel { get; set; }
    
    // Discovery
    public RecipeUnlockMethod UnlockMethod { get; set; }
    public string? UnlockRequirement { get; set; }
}

public class RecipeMaterial
{
    public required string ItemReference { get; set; }
    public int Quantity { get; set; }
}

public enum RecipeUnlockMethod
{
    Default,        // Known by default
    Trainer,        // Learn from NPC
    Discovery,      // Find as loot
    QuestReward,    // Quest reward
    SkillLevel,     // Unlock at skill level
    Achievement     // Achievement reward
}
```

**CraftingStation Model** (`RealmEngine.Shared/Models/CraftingStation.cs`)
```csharp
public class CraftingStation
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public string Description { get; set; } = string.Empty;
    public int Tier { get; set; } = 1;
    public List<string> Categories { get; set; } = new(); // weapons, armor, consumables
    public string? LocationReference { get; set; }
    public int RequiredLevel { get; set; }
    public Dictionary<int, StationUpgrade> UpgradeRequirements { get; set; } = new();
}

public class StationUpgrade
{
    public int GoldCost { get; set; }
    public List<RecipeMaterial> Materials { get; set; } = new();
}
```

### Character Crafting Data

**Add to Character Model:**
```csharp
public class Character
{
    // ... existing properties ...
    
    // Crafting data
    public List<string> KnownRecipes { get; set; } = new();
    public Dictionary<string, int> CraftingSkills { get; set; } = new();
    public Dictionary<string, int> StationTiers { get; set; } = new();
}
```

### Godot Integration Flow

**1. Browse Available Recipes:**
```csharp
// Player opens crafting station
var query = new GetAvailableRecipesQuery
{
    StationType = "blacksmith_forge",
    PlayerLevel = player.Level
};
var recipes = await mediator.Send(query);

// Godot displays recipes with material requirements
// Shows green checkmark if can craft, red X if cannot
```

**2. Craft Item:**
```csharp
// Player clicks "Craft" button
var command = new CraftItemCommand
{
    RecipeId = "recipe_iron_sword",
    CharacterId = player.Id
};
var result = await mediator.Send(command);

// Godot displays result
if (result.Success)
{
    ShowNotification($"Crafted {result.CraftedItem.Name}!");
    AddItemToInventory(result.CraftedItem);
    
    if (result.SkillLevelUp)
    {
        ShowNotification("Blacksmithing skill increased!");
    }
}
else
{
    ShowError(result.ErrorMessage);
}
```

**3. Station Upgrades:**
```csharp
// Player upgrades station
var command = new UpgradeStationCommand
{
    StationId = "blacksmith_forge",
    CharacterId = player.Id
};
var result = await mediator.Send(command);

if (result.Success)
{
    ShowNotification($"Station upgraded to Tier {result.NewTier}!");
    RefreshAvailableRecipes();
}
```

---

## Design Decisions

### Quality Randomization
**Approach**: Recipe defines min/max quality range (e.g., Common to Uncommon)
- Base chance weighted toward lower quality
- Crafting skill increases chance of higher quality
- Critical success (5% chance) adds +1 quality tier

**Formula**: 
```
baseQuality = RandomWeighted(recipe.MinQuality, recipe.MaxQuality)
skillBonus = (playerSkill - recipe.RequiredSkill) * 0.02 // +2% per skill level
finalChance = Clamp(skillBonus, 0, 0.50) // Max +50%

if (Random() < finalChance)
    quality = Min(quality + 1, ItemRarity.Legendary)
```

### Material Consumption
**Approach**: All materials consumed on craft attempt (success or failure)
- Prevents save-scum abuse
- Makes crafting decisions meaningful
- Higher skill reduces failure chance

**Alternative**: No material loss on failure (easier, less punishing)

### Skill Progression
**Approach**: Separate skill per station type
- Blacksmithing (weapons/armor)
- Alchemy (consumables)
- Enchanting (magical effects)

**Experience Formula**:
```
xp = recipe.BaseXP * (1 + (recipeTier - playerTier) * 0.5)
// Higher tier recipes give more XP
// Crafting at/below level gives reduced XP
```

### Station Tiers
**Approach**: 3 tiers per station
- Tier 1: Basic recipes (levels 1-15)
- Tier 2: Intermediate recipes (levels 16-30)
- Tier 3: Advanced recipes (levels 31-50)

**Upgrade Costs**: Exponential scaling (1000g → 5000g → 15000g)

### Recipe Discovery
**Methods**:
1. **Default**: Starting recipes known by all characters
2. **Trainer**: Purchase from NPC merchants (500-2000g)
3. **Discovery**: Find as loot in dungeons (5% drop chance from bosses)
4. **Quest**: Reward from crafting-related quests
5. **Skill**: Auto-unlock at specific skill levels

---

## Future Enhancements

### Short-term (Post-MVP)
- Salvaging system (break down items → materials)
- Rare/Epic recipe variants
- Crafting dailies/weeklies
- Guild crafting stations

### Long-term
- Player-owned crafting stations
- Crafting specializations (weapon master, potion brewer)
- Legendary recipe quests
- Crafting minigames

