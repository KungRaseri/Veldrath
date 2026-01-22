# Material Filter Configuration System

**Date:** January 22, 2026  
**Status:** ✅ IMPLEMENTED  
**Version:** 5.2

---

## Overview

The material filter configuration system replaces hardcoded material logic with a flexible, data-driven approach defined in [configuration/material-filters.json](../RealmEngine.Data/Data/Json/configuration/material-filters.json).

### Problem Solved

**Before (Hardcoded):**
```csharp
// BudgetItemGenerationService.cs - OLD
private static string? GetMaterialTypeForCategory(string category)
{
    return category?.ToLower() switch
    {
        "weapons" => "metals",
        "armor" => "metals",
        "clothing" => "fabrics",
        // ... hardcoded logic
    };
}
```

**After (Configuration-Driven):**
```json
// material-filters.json
{
  "categories": {
    "weapons": {
      "defaultMaterials": ["metals"],
      "types": {
        "bows": { "allowedMaterials": ["woods"] },
        "staves": { "allowedMaterials": ["woods", "metals"] }
      }
    }
  }
}
```

---

## Configuration Structure

### Hierarchy (Priority Order)

1. **Item-Level** (`allowedMaterials` field in item JSON) - **HIGHEST PRIORITY**
2. **Property Matches** (e.g., `armorClass=light` → fabrics)
3. **Type Overrides** (e.g., `heavy-blades` → metals)
4. **Category Defaults** (e.g., `weapons` → metals)
5. **Unknown Default** (fallback for unmapped categories)

---

## Configuration Format

### Categories

```json
{
  "categories": {
    "weapons": {
      "description": "Weapon category - most use metals, some use woods",
      "defaultMaterials": ["metals"],
      "types": {
        "heavy-blades": {
          "description": "Swords, longswords, greatswords",
          "allowedMaterials": ["metals"]
        },
        "bows": {
          "description": "Bows, longbows, crossbows",
          "allowedMaterials": ["woods"]
        }
      }
    }
  }
}
```

### Property Matches

```json
{
  "armor": {
    "propertyMatches": [
      {
        "property": "blockChance",
        "condition": "exists",
        "description": "Shields use woods and metals",
        "allowedMaterials": ["woods", "metals"]
      },
      {
        "property": "armorClass",
        "value": "light",
        "description": "Light armor uses fabrics",
        "allowedMaterials": ["fabrics"]
      }
    ]
  }
}
```

---

## Supported Categories

| Category | Default Materials | Property Matches | Type Overrides |
|----------|------------------|------------------|----------------|
| `weapons` | metals | None | heavy-blades, light-blades, axes, blunt, polearms, bows, staves |
| `armor` | metals | armorClass (light/medium/heavy), blockChance | light-armor, medium-armor, heavy-armor, shields |
| `clothing` | fabrics | None | None |
| `accessories` | gems, metals | None | rings, amulets, trinkets |
| `consumables` | (none) | None | None |
| `potions` | (none) | None | None |
| `scrolls` | (none) | None | None |
| `materials` | (none) | None | None |
| `reagents` | (none) | None | None |

---

## Code Integration

### BudgetItemGenerationService

**Method:** `GetMaterialTypesForItem(JToken baseItem, string category)`

**Logic Flow:**
1. Check item's `allowedMaterials` field → return if present
2. Load `MaterialFilterConfig` from `BudgetConfigFactory`
3. Check property matches (e.g., `armorClass`, `blockChance`)
4. Check type-specific overrides (e.g., `heavy-blades`)
5. Fall back to category default materials
6. Return empty list if no matches (consumables, scrolls, etc.)

**Example:**
```csharp
// Light armor item without allowedMaterials field
var config = _configFactory.GetMaterialFilters();
var armorClass = baseItem["armorClass"]?.Value<string>(); // "light"

// Matches property rule: armorClass=light → ["fabrics"]
return ["fabrics"];
```

---

## Material Types

### Available Material Types

| Type | Usage | Examples |
|------|-------|----------|
| `metals` | Weapons, heavy armor, shields | Iron, Steel, Mithril, Adamantine |
| `woods` | Bows, staves, shields | Oak, Ash, Yew, Ironwood |
| `fabrics` | Light armor, clothing | Linen, Silk, Mageweave, Shadowsilk |
| `leathers` | Medium armor | Rawhide, Leather, Dragonhide |
| `gems` | Accessories | Ruby, Sapphire, Diamond |
| `bones` | Exotic items | Bone, Ivory, Dragonbone |
| `crystals` | Magical items | Quartz, Amethyst, Void Crystal |
| `scales` | Exotic armor | Dragon Scale, Serpent Scale |
| `chitins` | Insect-based items | Beetle Carapace, Spider Chitin |
| `stones` | Blunt weapons | Granite, Obsidian |
| `ceramics` | Crafted items | Clay, Porcelain |
| `corals` | Underwater materials | Red Coral, Fire Coral |
| `glass` | Fragile items | Common Glass, Crystal Glass |
| `ice` | Frozen materials | Glacial Ice, Black Ice |
| `papers` | Scrolls, books | Parchment, Vellum |
| `rubbers` | Flexible items | Latex, Amber |

---

## Benefits

### 1. **Data-Driven Design**
- No code changes needed to add new material types
- Easy to adjust material compatibility
- Centralized configuration for all item types

### 2. **Flexible Property Matching**
- Supports dynamic property checks (`exists`, value matching)
- Handles complex cases (shields with `blockChance`)
- Extensible condition system

### 3. **Multiple Material Support**
- Items can use multiple material types (e.g., clubs: metals OR woods OR stones)
- Material pools try each type in order until one succeeds

### 4. **Fallback Safety**
- Always returns valid materials or empty list
- No null reference exceptions
- Graceful degradation for unknown categories

---

## Adding New Material Types

### Step 1: Create Material Properties

Add to `properties/materials/{type}/catalog.json`:
```json
{
  "materials": [
    {
      "slug": "mythril-glass",
      "name": "Mythril Glass",
      "rarityWeight": 15,
      "traits": { ... }
    }
  ]
}
```

### Step 2: Create Material Items

Add to `items/materials/{type}/catalog.json`:
```json
{
  "items": [
    {
      "slug": "mythril-glass-shard",
      "name": "Mythril Glass Shard",
      "propertyRef": "@properties/materials/glass:mythril-glass"
    }
  ]
}
```

### Step 3: Update Material Filters

Add to `configuration/material-filters.json`:
```json
{
  "categories": {
    "weapons": {
      "types": {
        "glass-blades": {
          "description": "Fragile glass weapons",
          "allowedMaterials": ["glass"]
        }
      }
    }
  }
}
```

### Step 4: No Code Changes Required! ✅

---

## Testing

### Unit Tests

Run material filter tests:
```bash
dotnet test RealmEngine.Core.Tests --filter "BudgetItemGenerationTests"
```

### Integration Tests

Verify material selection with budget generation:
```bash
dotnet test RealmEngine.Core.Tests --filter "GenerateItemAsync"
```

---

## Migration Notes

### From Hardcoded Logic

**Old hardcoded methods (REMOVED):**
- `GetMaterialTypeForCategory()` - Category mapping
- Inline armor class checks in `GetMaterialTypesForItem()`

**New config-based approach:**
- All logic moved to `material-filters.json`
- Property matching system handles special cases
- Type overrides provide granular control

### Breaking Changes

**None** - The system is backward compatible:
- Items with `allowedMaterials` field still work (highest priority)
- Existing items without `allowedMaterials` now use config fallback
- All 110 weapons/armor already have `allowedMaterials` defined

---

## Future Enhancements

### Potential Additions

1. **Condition Operators**: Support `>=`, `<=`, `MATCHES` for property matching
2. **Multiple Properties**: AND/OR logic for complex rules
3. **Exclusions**: `disallowedMaterials` blacklist
4. **Weight Modifiers**: Boost/reduce material selection probability
5. **Biome-Specific**: Location-based material availability
6. **Crafting Filters**: Separate filters for crafted vs dropped items

---

## See Also

- [BUDGET_SYSTEM_FIELD_STANDARDIZATION.md](../docs/proposals/BUDGET_SYSTEM_FIELD_STANDARDIZATION.md) - Budget formulas
- [material-pools.json](../RealmEngine.Data/Data/Json/general/material-pools.json) - Material rarity pools
- [ITEM_ENHANCEMENT_SYSTEM.md](../docs/ITEM_ENHANCEMENT_SYSTEM.md) - Overall item system
