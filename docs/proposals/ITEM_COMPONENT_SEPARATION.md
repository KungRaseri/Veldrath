# Item Component Separation Architecture (JSON v4.3)

**Date:** January 18, 2026  
**Status:** ✅ APPROVED - Ready for Implementation  
**Related:** ITEM_ENHANCEMENT_SYSTEM.md, BUDGET_SYSTEM_FIELD_STANDARDIZATION.md

---

## Table of Contents

1. [Executive Summary](#executive-summary) - Overview and major changes
2. [Current State Analysis](#current-state-analysis) - Problems with current structure
3. [JSON v4.3 Architecture Changes](#json-v43-architecture-changes) - New directory structure and files
4. [Proposed Architecture](#proposed-architecture) - Component classes and Item model
5. [Display Name Generation](#display-name-generation) - How names are constructed
6. [Tooltip Data Structure](#tooltip-data-structure-option-b-trait-separation) - Tooltip breakdown by source
7. [Component Count Limits](#component-count-limits) - Rarity-based restrictions
8. [Enchantments](#enchantments-separate-from-name-components) - Separation from prefixes/suffixes
9. [Migration Strategy](#migration-strategy) - 7-phase implementation plan
10. [Migration Script](#migration-script) - Complete Python automation
11. [Resolved Architectural Decisions](#resolved-architectural-decisions) - All 13 decisions documented
12. [Next Steps](#next-steps) - Immediate actions and success criteria
13. [Documentation Updates](#documentation-updates-required) - Files to update/create
14. [Approval Checklist](#approval-checklist) - Final approval status

---

## Quick Reference

**Key Breaking Change:** All material property references must update:
- `@materials/properties/metals:iron` → `@properties/materials/metals:iron`

**Migration Commands:**
```bash
# Create backup
python scripts/migrate-to-v4.3-unified-architecture.py --backup

# Execute all phases
python scripts/migrate-to-v4.3-unified-architecture.py --all

# Or execute phase by phase
python scripts/migrate-to-v4.3-unified-architecture.py --phase 1  # Non-breaking
python scripts/migrate-to-v4.3-unified-architecture.py --phase 2  # BREAKING
python scripts/migrate-to-v4.3-unified-architecture.py --phase 3  # BREAKING
python scripts/migrate-to-v4.3-unified-architecture.py --phase 4  # Cleanup
```

**Estimated Timeline:** 16 days total (4 JSON migration + 3 code updates + 3 testing)

**Files Created:**
- `properties/materials/` (moved from materials/properties/)
- `properties/qualities/catalog.json` (extracted from names.json)
- `configuration/rarity.json`
- `configuration/budget.json`
- `configuration/material-filters.json`
- `configuration/generation-rules.json`
- `configuration/socket-config.json`

**Files Modified:**
- All `names.json` files (remove quality/material, rename value→name)
- ~100+ catalog/recipe files (reference updates)
- `Item.cs`, `ItemGenerator.cs`, service classes

**Files Deleted:**
- `materials/properties/` (moved)
- `general/budget-config.json` (moved)
- `general/socket_config.json` (moved)
- `general/material-pools.json` (replaced)

---

## Executive Summary

Refactor the Item model to provide clear separation between Quality, Material, Prefixes, Suffixes, Enchantments, and Sockets. This enables granular tooltip display, proper trait attribution, and eliminates ambiguity in item name generation.

**MAJOR ARCHITECTURAL CHANGE (JSON v4.3):**
- Move `materials/properties/` → `properties/materials/` (unified properties domain)
- Create `properties/qualities/` (extract from names.json files)
- Create `configuration/` domain (system settings: rarity, budget, filters, rules)
- Update `names.json` files to v4.3 (ONLY prefixes/suffixes, remove quality/materials)
- **BREAKING CHANGE:** All references change from `@materials/properties/` → `@properties/materials/`

**Current Problem:**
```csharp
// Ambiguous structure
List<NameComponent> Prefixes;  // Could be quality, material, prefix, anything
List<NameComponent> Suffixes;  // Could be enchantments, sockets, anything
string Material;                // Duplicate with Prefixes?
Dictionary MaterialTraits;      // Where do these come from?
```

**Proposed Solution:**
```csharp
// Clear, typed structure
ItemQuality? Quality;           // "Fine", "Masterwork" - explicit
ItemMaterial? Material;         // "Iron", "Mithril" - explicit
List<ItemPrefix> Prefixes;      // "Flaming", "Sharp" - multiple allowed
List<ItemSuffix> Suffixes;      // "of Speed", "of the Bear" - multiple allowed
List<Enchantment> Enchantments; // Applied enchantments (separate from name)
Dictionary<SocketType, List<Socket>> Sockets; // Already good
```

---

## Current State Analysis

### Critical Architectural Insight

**PROBLEM DISCOVERED:** Quality and materials are **universal properties** that apply to ALL item types, but they're currently duplicated in every `names.json` file.

**Current Structure (WRONG):**
```
materials/properties/         ← Material PROPERTIES (durability, bonuses)
  metals/catalog.json
  woods/catalog.json
  leathers/catalog.json

items/materials/             ← Material ITEMS (Iron Ingot inventory items)
  metals/catalog.json
  leather/catalog.json

items/weapons/names.json     ← Has quality, material, prefix, suffix (REDUNDANT)
items/armor/names.json       ← Has quality, prefix, suffix (NO MATERIALS! Inconsistent!)
```

**Key Distinction:**
- `materials/properties/` = What items are **MADE FROM** (iron properties, durability, traits)
- `items/materials/` = Inventory **ITEMS** you can **HOLD** (Iron Ingot, Leather Strips)

**Proposed Structure v4.3 (CORRECT):**
```
properties/                  ← UNIVERSAL BUILDING BLOCKS
  materials/                 ← MOVED from materials/properties/
    metals/catalog.json
    woods/catalog.json
    leathers/catalog.json
  qualities/                 ← NEW - extracted from names.json
    catalog.json

configuration/               ← SYSTEM SETTINGS
  rarity.json                ← Rarity tier definitions
  budget.json                ← Budget formulas (moved from general/)
  material-filters.json      ← Item type material pools
  generation-rules.json      ← Component limits, display rules
  socket-config.json         ← Socket system (moved from general/)

items/
  materials/                 ← UNCHANGED - inventory items
  weapons/
    catalog.json
    names.json               ← v4.3: ONLY prefixes/suffixes
  armor/
    catalog.json
    names.json               ← v4.3: ONLY prefixes/suffixes
```

### Existing Item Model Issues

1. **Name Component Ambiguity**
   - `Prefixes` and `Suffixes` are generic `List<NameComponent>`
   - No way to tell if "Iron" is a material or just a descriptive prefix
   - Material stored in BOTH `Material` string AND potentially in `Prefixes`

2. **Trait Attribution Confusion**
   - `BaseTraits` - From catalog (weapon type traits)
   - `MaterialTraits` - From material component
   - `Traits` - Combined or separate? Unclear
   - No way to show "Material Bonuses" vs "Prefix Bonuses" in tooltips

3. **Display Name Generation**
   - Current: Iterate all Prefixes/Suffixes and join them
   - Problem: Can create absurdly long names with 5+ prefixes
   - No clear rule for what gets displayed vs what's tracked internally

4. **Enchantment Overlap**
   - "Flaming" can be a prefix OR an enchantment
   - "of Fire" can be a suffix OR an enchantment
   - Are they the same thing? Different bonuses? Overlapping?

### Example of Current Ambiguity

```csharp
// Generated item
item.Prefixes = [
    { Token: "quality", Value: "Fine" },
    { Token: "material", Value: "Iron" },
    { Token: "prefix", Value: "Flaming" }
];
item.Material = "Iron";  // Duplicate!
item.Suffixes = [
    { Token: "suffix", Value: "of the Bear" }
];

// Display name: "Fine Iron Flaming Longsword of the Bear"
// But where do traits come from? How to show breakdown?
```

---

## JSON v4.3 Architecture Changes

### New Directory Structure

```
RealmEngine.Data/Data/Json/
├── properties/                      ← NEW: Universal components
│   ├── materials/                   ← MOVED from materials/properties/
│   │   ├── metals/
│   │   │   ├── catalog.json        ← Iron, Steel, Mithril
│   │   │   └── .cbconfig.json
│   │   ├── woods/
│   │   │   ├── catalog.json        ← Oak, Ash, Yew
│   │   │   └── .cbconfig.json
│   │   └── leathers/
│   │       ├── catalog.json        ← Hide, Leather, Studded
│   │       └── .cbconfig.json
│   └── qualities/                   ← NEW: Extracted from names.json
│       ├── catalog.json            ← Fine, Superior, Masterwork
│       └── .cbconfig.json
│
├── configuration/                   ← NEW: System settings
│   ├── rarity.json                 ← Rarity tier definitions
│   ├── budget.json                 ← Budget formulas (moved from general/)
│   ├── material-filters.json       ← Item type material pools
│   ├── generation-rules.json       ← Component limits by rarity
│   └── socket-config.json          ← Socket system (moved from general/)
│
├── items/
│   ├── weapons/
│   │   ├── catalog.json            ← UNCHANGED
│   │   └── names.json              ← v4.3: ONLY prefixes/suffixes
│   └── armor/
│       ├── catalog.json            ← UNCHANGED
│       └── names.json              ← v4.3: ONLY prefixes/suffixes
│
└── general/                         ← Keep for other data
    └── material-pools.json         ← DELETE (replaced by material-filters.json)
```

### properties/qualities/catalog.json (NEW)

**Purpose:** Single source of truth for quality tiers, with itemTypeTraits for different item types.

```json
{
  "version": "4.3",
  "type": "quality_catalog",
  "lastUpdated": "2026-01-18",
  "description": "Universal quality tiers for all items",
  "qualities": [
    {
      "name": "Fine",
      "rarityWeight": 40,
      "description": "Well-crafted with attention to detail",
      "itemTypeTraits": {
        "weapon": {
          "damageBonus": { "value": 1, "type": "number" }
        },
        "armor": {
          "armorBonus": { "value": 2, "type": "number" }
        }
      }
    },
    {
      "name": "Superior",
      "rarityWeight": 25,
      "description": "Expertly crafted with superior materials",
      "itemTypeTraits": {
        "weapon": {
          "damageBonus": { "value": 2, "type": "number" },
          "critChanceBonus": { "value": 2, "type": "number" }
        },
        "armor": {
          "armorBonus": { "value": 4, "type": "number" },
          "durabilityBonus": { "value": 10, "type": "number" }
        }
      }
    },
    {
      "name": "Masterwork",
      "rarityWeight": 10,
      "description": "A masterpiece of craftsmanship",
      "itemTypeTraits": {
        "weapon": {
          "damageBonus": { "value": 4, "type": "number" },
          "critChanceBonus": { "value": 5, "type": "number" }
        },
        "armor": {
          "armorBonus": { "value": 6, "type": "number" },
          "durabilityBonus": { "value": 20, "type": "number" }
        }
      }
    },
    {
      "name": "Legendary",
      "rarityWeight": 3,
      "description": "A legendary work of unparalleled quality",
      "itemTypeTraits": {
        "weapon": {
          "damageBonus": { "value": 8, "type": "number" },
          "critChanceBonus": { "value": 10, "type": "number" },
          "critDamageBonus": { "value": 25, "type": "number" }
        },
        "armor": {
          "armorBonus": { "value": 12, "type": "number" },
          "durabilityBonus": { "value": 50, "type": "number" },
          "damageReductionBonus": { "value": 5, "type": "number" }
        }
      }
    }
  ]
}
```

### configuration/material-filters.json (NEW)

**Purpose:** Define which materials can be used for which item types (replaces material-pools.json).

```json
{
  "version": "4.3",
  "type": "material_filter_config",
  "lastUpdated": "2026-01-18",
  "description": "Item type material compatibility",
  "filters": {
    "weapon": {
      "allowedMaterials": [
        "@properties/materials/metals",
        "@properties/materials/woods"
      ],
      "pools": {
        "low_tier": {
          "metals": [
            { "materialRef": "@properties/materials/metals:iron", "rarityWeight": 60 },
            { "materialRef": "@properties/materials/metals:steel", "rarityWeight": 30 }
          ],
          "woods": [
            { "materialRef": "@properties/materials/woods:oak", "rarityWeight": 60 },
            { "materialRef": "@properties/materials/woods:ash", "rarityWeight": 30 }
          ]
        },
        "high_tier": {
          "metals": [
            { "materialRef": "@properties/materials/metals:mithril", "rarityWeight": 20 },
            { "materialRef": "@properties/materials/metals:adamantine", "rarityWeight": 5 }
          ],
          "woods": [
            { "materialRef": "@properties/materials/woods:yew", "rarityWeight": 25 },
            { "materialRef": "@properties/materials/woods:ironwood", "rarityWeight": 10 }
          ]
        }
      }
    },
    "armor": {
      "allowedMaterials": [
        "@properties/materials/metals",
        "@properties/materials/leathers"
      ],
      "pools": {
        "low_tier": {
          "metals": [
            { "materialRef": "@properties/materials/metals:iron", "rarityWeight": 60 },
            { "materialRef": "@properties/materials/metals:steel", "rarityWeight": 30 }
          ],
          "leathers": [
            { "materialRef": "@properties/materials/leathers:hide", "rarityWeight": 60 },
            { "materialRef": "@properties/materials/leathers:leather", "rarityWeight": 40 }
          ]
        }
      }
    }
  }
}
```

### configuration/generation-rules.json (NEW)

**Purpose:** Component limits, display rules, and generation constraints.

```json
{
  "version": "4.3",
  "type": "generation_rules_config",
  "lastUpdated": "2026-01-18",
  "description": "Item generation rules and limits",
  "componentLimits": {
    "quality": { "min": 0, "max": 1 },
    "material": { "min": 0, "max": 1 },
    "prefixes": {
      "byRarity": {
        "common": { "min": 0, "max": 1 },
        "uncommon": { "min": 0, "max": 1 },
        "rare": { "min": 0, "max": 2 },
        "epic": { "min": 0, "max": 2 },
        "legendary": { "min": 0, "max": 3 }
      }
    },
    "suffixes": {
      "byRarity": {
        "common": { "min": 0, "max": 0 },
        "uncommon": { "min": 0, "max": 1 },
        "rare": { "min": 0, "max": 1 },
        "epic": { "min": 0, "max": 2 },
        "legendary": { "min": 0, "max": 3 }
      }
    }
  },
  "displayRules": {
    "nameFormat": "[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]",
    "showAllComponents": false,
    "showFirstPrefixOnly": true,
    "showFirstSuffixOnly": true,
    "hideComponentsAboveRarity": null
  },
  "validationRules": {
    "enforceComponentUniqueness": true,
    "allowDuplicatePrefixes": false,
    "allowDuplicateSuffixes": false,
    "allowDuplicateAcrossCategories": true
  }
}
```

### items/weapons/names.json (v4.3 - UPDATED)

**REMOVED:** `quality` and `material` components (moved to properties/)  
**REMOVED:** `descriptive` component (merged into `prefix`)  
**CHANGED:** `value` → `name` field rename

```json
{
  "version": "4.3",
  "type": "modifier_catalog",
  "lastUpdated": "2026-01-18",
  "description": "Weapon name modifiers (prefixes and suffixes only)",
  "supportsTraits": true,
  "components": {
    "prefix": [
      {
        "name": "Flaming",
        "rarityWeight": 35,
        "description": "Wreathed in magical flames",
        "traits": {
          "fireDamage": { "value": 6, "type": "number" }
        }
      },
      {
        "name": "Sharp",
        "rarityWeight": 50,
        "description": "Honed to a razor edge",
        "traits": {
          "damageBonus": { "value": 4, "type": "number" }
        }
      },
      {
        "name": "Ancient",
        "rarityWeight": 15,
        "description": "Forged in ages past",
        "traits": {
          "damageBonus": { "value": 6, "type": "number" },
          "critChanceBonus": { "value": 3, "type": "number" }
        }
      }
    ],
    "suffix": [
      {
        "name": "of Speed",
        "rarityWeight": 40,
        "description": "Grants swiftness to its wielder",
        "attributeBonuses": {
          "dexterity": 3
        },
        "traits": {
          "attackSpeedBonus": { "value": 10, "type": "number" }
        }
      },
      {
        "name": "of the Bear",
        "rarityWeight": 35,
        "description": "Imbued with ursine strength",
        "attributeBonuses": {
          "strength": 4
        }
      },
      {
        "name": "of Crushing",
        "rarityWeight": 30,
        "description": "Devastating against armor",
        "attributeBonuses": {
          "strength": 3
        },
        "traits": {
          "armorPenetrationBonus": { "value": 15, "type": "number" }
        }
      }
    ]
  }
}
```

### Breaking Changes Summary

| Old Reference | New Reference | Affected Files |
|---------------|---------------|----------------|
| `@materials/properties/metals:iron` | `@properties/materials/metals:iron` | All catalog.json, names.json, recipes |
| `@materials/properties/woods:oak` | `@properties/materials/woods:oak` | All weapon catalogs |
| `@materials/properties/leathers:hide` | `@properties/materials/leathers:hide` | All armor catalogs |
| `general/budget-config.json` | `configuration/budget.json` | BudgetCalculator.cs |
| `general/socket_config.json` | `configuration/socket-config.json` | SocketService.cs |
| `general/material-pools.json` | `configuration/material-filters.json` | MaterialPoolService.cs |

**Estimated Impact:** ~100+ files require reference updates

---

## Proposed Architecture

### New Component Model

```csharp
namespace RealmEngine.Shared.Models;

/// <summary>
/// Common interface for all item components.
/// </summary>
public interface IItemComponent
{
    /// <summary>Display name (e.g., "Fine", "Iron", "Flaming")</summary>
    string Name { get; set; }
    
    /// <summary>Selection weight for generation (higher = more common)</summary>
    int RarityWeight { get; set; }
    
    /// <summary>Trait bonuses provided by this component</summary>
    Dictionary<string, TraitValue> Traits { get; set; }
    
    /// <summary>Reference to JSON definition (e.g., "@properties/materials/metals:iron")</summary>
    string? Reference { get; set; }
}

/// <summary>
/// Represents an item's quality tier (craftsmanship level).
/// Examples: Fine, Superior, Masterwork, Legendary
/// </summary>
public class ItemQuality : IItemComponent
{
    /// <summary>Display name (e.g., "Fine", "Masterwork")</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Selection weight for generation (higher = more common)</summary>
    public int RarityWeight { get; set; }
    
    /// <summary>Trait bonuses provided by this quality tier</summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>Reference to JSON definition (e.g., "@items/weapons/names.json#quality:fine")</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// Represents the material an item is crafted from.
/// Examples: Iron, Steel, Mithril, Oak, Leather
/// </summary>
public class ItemMaterial : IItemComponent
{
    /// <summary>Display name (e.g., "Iron", "Mithril")</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Selection weight for generation (higher = more common)</summary>
    public int RarityWeight { get; set; }
    
    /// <summary>Cost multiplier for refined/exotic variants (defaults to 1.0)</summary>
    public double CostScale { get; set; } = 1.0;
    
    /// <summary>Trait bonuses provided by this material (uses itemTypeTraits for weapon/armor)</summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>Reference to JSON definition (e.g., "@properties/materials/metals:iron")</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// Represents a descriptive prefix modifier.
/// Examples: Flaming, Sharp, Ancient, Divine
/// </summary>
public class ItemPrefix : IItemComponent
{
    /// <summary>Display name (e.g., "Flaming", "Sharp")</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Selection weight for generation (higher = more common)</summary>
    public int RarityWeight { get; set; }
    
    /// <summary>Trait bonuses provided by this prefix</summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>Reference to JSON definition (e.g., "@items/weapons/names.json#prefix:flaming")</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// Represents a descriptive suffix modifier.
/// Examples: of Speed, of the Bear, of Crushing
/// </summary>
public class ItemSuffix : IItemComponent
{
    /// <summary>Display name (e.g., "of Speed", "of the Bear")</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Selection weight for generation (higher = more common)</summary>
    public int RarityWeight { get; set; }
    
    /// <summary>Trait bonuses provided by this suffix</summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>Reference to JSON definition (e.g., "@items/weapons/names.json#suffix:of-speed")</summary>
    public string? Reference { get; set; }
}
```

### Updated Item Model

```csharp
public class Item : ITraitable
{
    // === EXISTING FIELDS (unchanged) ===
    public string Id { get; set; }
    public string Slug { get; set; }
    public string Name { get; set; }  // Full display name
    public string BaseName { get; set; }  // "Longsword", "Plate Mail"
    public ItemType Type { get; set; }
    public ItemRarity Rarity { get; set; }
    // ... other core fields ...

    // === NEW STRUCTURED COMPONENTS ===
    
    /// <summary>
    /// Quality tier of this item (craftsmanship level).
    /// 0-1 allowed: null = Normal/Average quality item, non-null = upgraded quality.
    /// Examples: "Fine", "Masterwork", "Legendary"
    /// </summary>
    public ItemQuality? Quality { get; set; }
    
    /// <summary>
    /// Material this item is crafted from.
    /// 0-1 allowed: null = non-material item (e.g., spell scrolls), non-null = physical material.
    /// Examples: "Iron", "Mithril", "Oak", "Leather"
    /// </summary>
    public ItemMaterial? Material { get; set; }
    
    /// <summary>
    /// Descriptive prefixes that modify the item.
    /// 0-3 allowed: Display uses first prefix, but multiple tracked for trait calculation.
    /// Examples: "Flaming", "Sharp", "Ancient"
    /// </summary>
    public List<ItemPrefix> Prefixes { get; set; } = new();
    
    /// <summary>
    /// Descriptive suffixes that modify the item.
    /// 0-3 allowed: Display uses first suffix, but multiple tracked for trait calculation.
    /// Examples: "of Speed", "of the Bear", "of Crushing"
    /// </summary>
    public List<ItemSuffix> Suffixes { get; set; } = new();
    
    // === EXISTING ENHANCEMENT SYSTEMS (unchanged) ===
    
    /// <summary>
    /// Generation-time enchantments baked into the item.
    /// Separate from name prefixes/suffixes.
    /// </summary>
    public List<Enchantment> Enchantments { get; set; } = new();
    
    /// <summary>
    /// Player-applied enchantments (post-crafting).
    /// </summary>
    public List<Enchantment> PlayerEnchantments { get; set; } = new();
    
    /// <summary>
    /// Socket system (already well-structured).
    /// </summary>
    public Dictionary<SocketType, List<Socket>> Sockets { get; set; } = new();

    // === TRAIT SYSTEMS (updated for separation) ===
    
    /// <summary>
    /// Base traits from item type (catalog.json).
    /// Examples: critChance, range, damageReduction (from catalog)
    /// </summary>
    public Dictionary<string, TraitValue> BaseTraits { get; set; } = new();
    
    /// <summary>
    /// Combined attribute bonuses (computed from all sources).
    /// Kept for backward compatibility, but prefer GetAttributeBreakdown() for display.
    /// </summary>
    public Dictionary<string, int> BaseAttributes { get; set; } = new();

    // === DEPRECATED FIELDS (remove in Phase 3) ===
    
    /// <summary>
    /// ⚠️ DEPRECATED: Use Quality, Material, Prefixes properties instead.
    /// Old generic structure replaced by typed components.
    /// </summary>
    [Obsolete("Use Quality, Material, Prefixes properties")]
    [System.Text.Json.Serialization.JsonIgnore]
    public List<NameComponent> PrefixesOld { get; set; } = new();
    
    /// <summary>
    /// ⚠️ DEPRECATED: Use Suffixes property instead.
    /// </summary>
    [Obsolete("Use Suffixes property")]
    [System.Text.Json.Serialization.JsonIgnore]
    public List<NameComponent> SuffixesOld { get; set; } = new();
}
```

---

## Display Name Generation

### Strategy: First Component Display, All Components Tracked

**Display Format:**
```
[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]
```

**Implementation:**
```csharp
/// <summary>
/// Generate full display name from components.
/// Uses first prefix/suffix for display, but tracks all internally.
/// </summary>
public string GetDisplayName()
{
    var parts = new List<string>();
    
    if (Quality != null)
        parts.Add(Quality.Name);
    
    if (Material != null)
        parts.Add(Material.Name);
    
    if (Prefixes.Any())
        parts.Add(Prefixes[0].Name); // Display first prefix only
    
    parts.Add(BaseName);
    
    if (Suffixes.Any())
        parts.Add(Suffixes[0].Name); // Display first suffix only
    
    return string.Join(" ", parts);
}

/// <summary>
/// Get short name (no quality/material).
/// </summary>
public string GetShortName()
{
    var parts = new List<string>();
    
    if (Prefixes.Any())
        parts.Add(Prefixes[0].Name);
    
    parts.Add(BaseName);
    
    if (Suffixes.Any())
        parts.Add(Suffixes[0].Name);
    
    return string.Join(" ", parts);
}
```

### Examples

| Components | Display Name | Short Name |
|------------|--------------|------------|
| Quality: Fine<br>Material: Iron<br>Prefix: Flaming<br>Base: Longsword<br>Suffix: of Speed | Fine Iron Flaming Longsword of Speed | Flaming Longsword of Speed |
| Material: Mithril<br>Base: Plate Mail | Mithril Plate Mail | Plate Mail |
| Prefix: Sharp<br>Prefix: Ancient (hidden)<br>Base: Dagger<br>Suffix: of the Tiger | Sharp Dagger of the Tiger | Sharp Dagger of the Tiger |
| Base: Health Potion | Health Potion | Health Potion |

---

## Tooltip Data Structure (Option B: Trait Separation)

### Implementation

```csharp
/// <summary>
/// Get structured tooltip data with trait attribution.
/// </summary>
public ItemTooltipData GetTooltipData()
{
    return new ItemTooltipData
    {
        // Basic info
        DisplayName = GetDisplayName(),
        ShortName = GetShortName(),
        BaseName = BaseName,
        Description = Description,
        Rarity = Rarity,
        
        // Component info
        Quality = Quality?.Name,
        Material = Material?.Name,
        Prefixes = Prefixes.Select(p => p.Name).ToList(),
        Suffixes = Suffixes.Select(s => s.Name).ToList(),
        
        // Trait breakdown (separate for display)
        BaseTraits = BaseTraits,
        QualityTraits = Quality?.Traits ?? new(),
        MaterialTraits = Material?.Traits ?? new(),
        PrefixTraits = MergeTraits(Prefixes.SelectMany(p => p.Traits)),
        SuffixTraits = MergeTraits(Suffixes.SelectMany(s => s.Traits)),
        EnchantmentTraits = MergeTraits(Enchantments.SelectMany(e => e.Traits)),
        SocketTraits = GetSocketTraits(),
        
        // Attribute breakdown
        BaseAttributes = GetAttributeBreakdown(),
        
        // Requirements
        Requirements = Requirements,
        
        // Enhancement info
        EnchantmentCount = Enchantments.Count + PlayerEnchantments.Count,
        SocketInfo = GetSocketsInfo(),
        UpgradeLevel = UpgradeLevel
    };
}

/// <summary>
/// Get attribute bonuses broken down by source.
/// </summary>
public AttributeBreakdown GetAttributeBreakdown()
{
    var breakdown = new AttributeBreakdown();
    
    // Quality bonuses
    if (Quality != null)
        breakdown.Quality = ExtractAttributes(Quality.Traits);
    
    // Material bonuses
    if (Material != null)
        breakdown.Material = ExtractAttributes(Material.Traits);
    
    // Prefix bonuses (sum all prefixes)
    breakdown.Prefixes = ExtractAttributes(
        MergeTraits(Prefixes.SelectMany(p => p.Traits))
    );
    
    // Suffix bonuses (sum all suffixes)
    breakdown.Suffixes = ExtractAttributes(
        MergeTraits(Suffixes.SelectMany(s => s.Traits))
    );
    
    // Enchantment bonuses
    breakdown.Enchantments = ExtractAttributes(
        MergeTraits(Enchantments.SelectMany(e => e.Traits))
    );
    
    // Socket bonuses
    breakdown.Sockets = ExtractAttributes(GetSocketTraits());
    
    // Total
    breakdown.Total = SumAttributes(
        breakdown.Quality,
        breakdown.Material,
        breakdown.Prefixes,
        breakdown.Suffixes,
        breakdown.Enchantments,
        breakdown.Sockets
    );
    
    return breakdown;
}

private Dictionary<string, int> ExtractAttributes(Dictionary<string, TraitValue> traits)
{
    return traits
        .Where(t => IsAttributeTrait(t.Key)) // strengthBonus, dexterityBonus, etc.
        .ToDictionary(
            t => StripBonusSuffix(t.Key), // "strengthBonus" -> "strength"
            t => (int)t.Value.AsDouble()
        );
}

private bool IsAttributeTrait(string traitName)
{
    return traitName.EndsWith("Bonus") && 
           (traitName.StartsWith("strength") || 
            traitName.StartsWith("dexterity") || 
            traitName.StartsWith("constitution") ||
            traitName.StartsWith("intelligence") ||
            traitName.StartsWith("wisdom") ||
            traitName.StartsWith("charisma"));
}
```

### Tooltip Display Example (Godot)

```gdscript
# Tooltip rendering in Godot
func render_tooltip(tooltip_data: ItemTooltipData):
    # Header
    add_title(tooltip_data.display_name, get_rarity_color(tooltip_data.rarity))
    add_text(tooltip_data.description)
    
    # Components
    if tooltip_data.quality:
        add_component("Quality", tooltip_data.quality, COLOR_GOLD)
    if tooltip_data.material:
        add_component("Material", tooltip_data.material, COLOR_SILVER)
    if tooltip_data.prefixes.size() > 0:
        add_component("Prefixes", ", ".join(tooltip_data.prefixes), COLOR_ORANGE)
    if tooltip_data.suffixes.size() > 0:
        add_component("Suffixes", ", ".join(tooltip_data.suffixes), COLOR_CYAN)
    
    add_separator()
    
    # Attribute breakdown
    var attr_breakdown = tooltip_data.base_attributes
    
    if attr_breakdown.material:
        add_section("Material Bonuses", COLOR_SILVER)
        for attr in attr_breakdown.material:
            add_stat("+%d %s" % [attr.value, attr.name])
    
    if attr_breakdown.prefixes:
        add_section("Prefix Bonuses", COLOR_ORANGE)
        for attr in attr_breakdown.prefixes:
            add_stat("+%d %s" % [attr.value, attr.name])
    
    if attr_breakdown.suffixes:
        add_section("Suffix Bonuses", COLOR_CYAN)
        for attr in attr_breakdown.suffixes:
            add_stat("+%d %s" % [attr.value, attr.name])
    
    add_separator()
    
    # Combat traits breakdown
    render_trait_section("Base Stats", tooltip_data.base_traits)
    render_trait_section("Material Traits", tooltip_data.material_traits)
    render_trait_section("Enhancement Traits", tooltip_data.prefix_traits)
    
    # Requirements
    if tooltip_data.requirements:
        add_separator()
        add_section("Requirements", COLOR_RED)
        add_stat("Level %d" % tooltip_data.requirements.level)
        for attr in tooltip_data.requirements.attributes:
            add_stat("%s %d" % [attr.name, attr.value])
```

---

## Component Count Limits

### Generation Rules

| Component | Min | Max | Selection Logic |
|-----------|-----|-----|-----------------|
| **Quality** | 0 | 1 | 0 = Normal/Average, 1 = Upgraded quality tier |
| **Material** | 0 | 1 | 0 = Non-physical item (scrolls, potions), 1 = Physical item |
| **Prefixes** | 0 | 3 | Budget-limited, rarity-capped |
| **Suffixes** | 0 | 3 | Budget-limited, rarity-capped |
| **Enchantments** | 0 | MaxEnchantments | Separate budget system |
| **Sockets** | 0 | N | Based on item type and rarity |

### Rarity-Based Caps

```csharp
public int GetMaxPrefixes()
{
    return Rarity switch
    {
        ItemRarity.Common => 1,
        ItemRarity.Uncommon => 1,
        ItemRarity.Rare => 2,
        ItemRarity.Epic => 2,
        ItemRarity.Legendary => 3,
        _ => 0
    };
}

public int GetMaxSuffixes()
{
    return Rarity switch
    {
        ItemRarity.Common => 0,
        ItemRarity.Uncommon => 1,
        ItemRarity.Rare => 1,
        ItemRarity.Epic => 2,
        ItemRarity.Legendary => 3,
        _ => 0
    };
}
```

### Budget Distribution Example

```
Total Budget: 1000

Allocations:
- Base Item (catalog): 200 (20%)
- Material: 300 (30%)
- Quality: 100 (10%)
- Prefix 1: 150 (15%)
- Prefix 2: 100 (10%)
- Suffix 1: 150 (15%)

Remaining: 0
```

---

## Enchantments: Separate From Name Components

### Current Problem

```json
// In names.json - is this a prefix or enchantment?
{
  "value": "Flaming",
  "rarityWeight": 35,
  "traits": {
    "fireDamage": { "value": 6, "type": "number" }
  }
}

// In enchantments/catalog.json - duplicate?
{
  "name": "Flame Weapon",
  "traits": {
    "fireDamage": { "value": 10, "type": "number" }
  }
}
```

### Proposed Solution

**Prefixes/Suffixes** = Descriptive modifiers with small bonuses
- Generated at creation time, baked into name
- Examples: "Flaming" (+6 fire), "Sharp" (+4 damage), "of Speed" (+3 DEX)

**Enchantments** = Powerful magical effects applied separately
- NOT part of the item name
- Displayed as separate section in tooltips
- Examples: "Flame Weapon" (+10 fire), "Fortify Armor" (+5 AC)

### Display Examples

```
// Prefix-based fire bonus
Name: "Flaming Longsword"
Traits: +6 Fire Damage (from Flaming prefix)

// Enchantment-based fire bonus
Name: "Longsword"
Enchantments: [Flame Weapon]
Traits: +10 Fire Damage (from Flame Weapon enchantment)

// Both
Name: "Flaming Longsword"
Enchantments: [Flame Weapon, Fortify Armor]
Traits:
  +6 Fire Damage (Flaming prefix)
  +10 Fire Damage (Flame Weapon enchantment)
  +5 AC (Fortify Armor enchantment)
Total: +16 Fire Damage, +5 AC
```

### Updated names.json Categories

```json
// items/weapons/names.json
{
  "components": {
    "quality": [
      { "value": "Fine", "traits": { ... } }
    ],
    "prefix": [
      { "value": "Flaming", "traits": { "fireDamage": 6 } },  // Small bonus
      { "value": "Sharp", "traits": { "damageBonus": 4 } }
    ],
    "suffix": [
      { "value": "of Speed", "traits": { "dexterityBonus": 3 } },
      { "value": "of the Bear", "traits": { "strengthBonus": 4 } }
    ]
  }
}

// items/enchantments/catalog.json
{
  "enchantments": [
    {
      "name": "Flame Weapon",
      "description": "Wraps the weapon in magical flames",
      "traits": { "fireDamage": 10 }  // Larger bonus
    },
    {
      "name": "Fortify Armor",
      "description": "Magically hardens the armor",
      "traits": { "armorClassBonus": 5 }
    }
  ]
}
```

---

## Migration Strategy

### Phase 1: Create New JSON Structure (Non-Breaking)

**Goal:** Create v4.3 structure without breaking existing code.

**Tasks:**
1. Create new directories: `properties/`, `configuration/`
2. Copy `materials/properties/*` → `properties/materials/*`
3. Extract qualities from `names.json` → `properties/qualities/catalog.json`
4. Create configuration files:
   - `configuration/rarity.json`
   - `configuration/budget.json` (move from `general/budget-config.json`)
   - `configuration/material-filters.json` (transform from `general/material-pools.json`)
   - `configuration/generation-rules.json` (new)
   - `configuration/socket-config.json` (move from `general/socket_config.json`)
5. Create `.cbconfig.json` files for new directories

**Script:** `migrate-to-v4.3-unified-architecture.py` (Phase 1)

**Timeline:** 1 day

**Validation:**
- New structure created
- Old structure still intact (parallel systems)
- All files valid JSON
- No broken references yet

---

### Phase 2: Update All References (BREAKING CHANGE)

**Goal:** Update all material property references to new paths.

**Tasks:**
1. Find all JSON files with material references
2. Update references:
   - `@materials/properties/metals:` → `@properties/materials/metals:`
   - `@materials/properties/woods:` → `@properties/materials/woods:`
   - `@materials/properties/leathers:` → `@properties/materials/leathers:`
3. Update configuration references:
   - `general/budget-config.json` → `configuration/budget.json`
   - `general/socket_config.json` → `configuration/socket-config.json`
4. Update C# code:
   - `BudgetCalculator.cs` (configuration paths)
   - `MaterialPoolService.cs` (material-filters.json)
   - `SocketService.cs` (socket-config.json)

**Script:** `migrate-to-v4.3-unified-architecture.py` (Phase 2)

**Timeline:** 2 days

**Affected Files (estimated):**
- ~50 catalog.json files (items, enemies, NPCs)
- ~20 recipe files
- ~10 ability files
- ~5 C# service classes

**Validation:**
- All references updated
- No broken links (run reference integrity tests)
- C# code compiles
- Tests pass

---

### Phase 3: Migrate names.json Files (Breaking Change)

**Goal:** Update all `names.json` files to v4.3 format.

**Tasks:**
1. Remove `quality` components (moved to `properties/qualities/`)
2. Remove `material` components (use `@properties/materials/` references)
3. Merge `descriptive` components → `prefix` (if exists)
4. Rename `value` field → `name` in all components
5. Update metadata:
   - `version: "4.3"`
   - `type: "modifier_catalog"`
6. Add `attributeBonuses` field where needed (STR, DEX, etc.)

**Files to Update:**
- `items/weapons/names.json`
- `items/armor/names.json`
- `items/jewelry/names.json`
- `items/consumables/names.json`
- Any other `names.json` files

**Script:** `migrate-to-v4.3-unified-architecture.py` (Phase 3)

**Timeline:** 1 day

**Validation:**
- All names.json files v4.3 compliant
- No quality/material components remain
- All components have `name` field (not `value`)
- JSON compliance tests pass

---

### Phase 4: Cleanup Old Structure

**Goal:** Remove deprecated files and directories.

**Tasks:**
1. Delete `materials/properties/` (moved to `properties/materials/`)
2. Delete `general/budget-config.json` (moved to `configuration/`)
3. Delete `general/socket_config.json` (moved to `configuration/`)
4. Delete `general/material-pools.json` (replaced by `material-filters.json`)
5. Update documentation:
   - JSON standards documents
   - ITEM_ENHANCEMENT_SYSTEM.md
   - BUDGET_SYSTEM_FIELD_STANDARDIZATION.md

**Script:** `migrate-to-v4.3-unified-architecture.py` (Phase 4)

**Timeline:** 1 day

**Validation:**
- No duplicate structure remains
- All tests pass
- Documentation updated
- Git commit: "Complete v4.3 migration"

---

### Phase 5: Update Item Model (Code Changes)

**Goal:** Implement new component classes and properties in C#.

**Tasks:**
1. Create component classes:
   - `IItemComponent` interface
   - `ItemQuality` class
   - `ItemMaterial` class
   - `ItemPrefix` class
   - `ItemSuffix` class
2. Add new properties to `Item` model:
   - `ItemQuality? Quality`
   - `ItemMaterial? Material`
   - `List<ItemPrefix> Prefixes`
   - `List<ItemSuffix> Suffixes`
3. Mark old properties `[Obsolete]`
4. Implement new methods:
   - `GetDisplayName()` - Uses first prefix/suffix only
   - `GetShortName()` - No quality/material
   - `GetTooltipData()` - Structured breakdown
   - `GetAttributeBreakdown()` - Bonuses by source
5. Update `ITraitable` aggregation:
   - `GetTotalTraits()` - Aggregate from all components

**Files to Modify:**
- `RealmEngine.Shared/Models/Item.cs`
- `RealmEngine.Shared/Models/ItemQuality.cs` (new)
- `RealmEngine.Shared/Models/ItemMaterial.cs` (new)
- `RealmEngine.Shared/Models/ItemPrefix.cs` (new)
- `RealmEngine.Shared/Models/ItemSuffix.cs` (new)
- `RealmEngine.Shared/Models/IItemComponent.cs` (new)
- `RealmEngine.Shared/Models/ItemTooltipData.cs` (new)

**Timeline:** 3 days

**Validation:**
- All component classes created
- Item model compiles
- Unit tests pass
- Serialization works (JSON round-trip)

---

### Phase 6: Update ItemGenerator (Generator Logic)

**Goal:** Modify ItemGenerator to use new structure and populate new properties.

**Tasks:**
1. Update parser to read from `properties/qualities/`
2. Implement `SelectQuality()`:
   - Load from `properties/qualities/catalog.json`
   - Apply `itemTypeTraits` filtering (weapon vs armor)
   - Budget-based selection
3. Implement `SelectMaterial()`:
   - Use `configuration/material-filters.json` pools
   - Apply item type filters (weapons: metals/woods, armor: metals/leathers)
   - Budget-based selection
4. Update `SelectPrefixes()`:
   - Enforce uniqueness checks (no duplicate prefixes)
   - Respect rarity-based limits (1-3 prefixes)
   - Track all prefixes, use first for display name
5. Update `SelectSuffixes()`:
   - Enforce uniqueness checks (no duplicate suffixes)
   - Respect rarity-based limits (0-3 suffixes)
   - Track all suffixes, use first for display name
6. Update reference resolution:
   - Handle `@properties/materials/` paths
   - Handle `@properties/qualities/` paths
7. Implement tooltip data generation:
   - Separate trait sections (base, quality, material, prefixes, suffixes)
   - Attribute breakdown by source
   - Show all components in tooltip (even if not in name)

**Files to Modify:**
- `RealmEngine.Core/Generators/Modern/ItemGenerator.cs`
- `RealmEngine.Core/Generators/Modern/ComponentSelector.cs` (new)
- `RealmEngine.Core/Services/MaterialPoolService.cs` (update for material-filters.json)

**Timeline:** 5 days

**Validation:**
- Item generation works end-to-end
- All components populated correctly
- Budget respected
- Component limits enforced
- Uniqueness constraints respected
- Tooltip data structured correctly

---

### Phase 7: Testing and Validation

**Goal:** Comprehensive testing of new system.

**Tasks:**
1. Unit tests:
   - Component creation (deserialize from JSON)
   - Display name generation (with/without components)
   - Trait aggregation (sum from all sources)
   - Attribute breakdown (separate by source)
   - Component uniqueness enforcement
2. Integration tests:
   - Generate 1000 items, verify structure
   - Verify budget compliance
   - Verify component limits by rarity
   - Test reference resolution (qualities, materials, prefixes, suffixes)
3. Performance tests:
   - Trait aggregation speed (on-demand vs cached)
   - Memory usage (new structure vs old)
4. Godot integration tests:
   - Serialize items to JSON
   - Godot reads and displays tooltips
   - Verify trait calculations in combat
5. Data validation:
   - Run JSON compliance tests
   - Run reference integrity tests
   - Verify no broken links

**Files to Create:**
- `RealmEngine.Core.Tests/Generators/Modern/ItemGeneratorV43Tests.cs`
- `RealmEngine.Shared.Tests/Models/ItemComponentTests.cs`
- `RealmEngine.Data.Tests/JsonV43ComplianceTests.cs`

**Timeline:** 3 days

**Validation:**
- All tests pass (100% pass rate)
- Performance acceptable (<100ms per item generation)
- Memory impact acceptable (<2KB per item)
- Godot integration works

---

### Migration Timeline Summary

| Phase | Duration | Blocking? | Risk Level |
|-------|----------|-----------|------------|
| Phase 1: Create Structure | 1 day | No | Low |
| Phase 2: Update References | 2 days | **YES** | **HIGH** |
| Phase 3: Migrate names.json | 1 day | **YES** | Medium |
| Phase 4: Cleanup | 1 day | No | Low |
| Phase 5: Update Model | 3 days | No | Medium |
| Phase 6: Update Generator | 5 days | No | Medium |
| Phase 7: Testing | 3 days | No | Low |
| **TOTAL** | **16 days** | | |

**Critical Path:** Phases 1-4 (JSON migration) MUST complete before Phases 5-7 (code updates).

**Rollback Plan:** If Phase 2 breaks too many things, can revert to old structure. After Phase 4 cleanup, rollback becomes expensive.

---

## Migration Script

### migrate-to-v4.3-unified-architecture.py

```python
#!/usr/bin/env python3
"""
Migrate RealmEngine JSON data from v4.0 to v4.3 unified architecture.

Phase 1: Create new structure (non-breaking)
Phase 2: Update all references (BREAKING)
Phase 3: Migrate names.json files (BREAKING)
Phase 4: Cleanup old structure
"""

import json
import os
import shutil
from pathlib import Path
from typing import Dict, List, Any

# Configuration
DATA_ROOT = Path("RealmEngine.Data/Data/Json")
BACKUP_DIR = Path("backups/v4.0-to-v4.3")

def phase1_create_structure():
    """Phase 1: Create new directories and files (non-breaking)"""
    print("=== Phase 1: Creating New Structure ===")
    
    # Create directories
    (DATA_ROOT / "properties/materials").mkdir(parents=True, exist_ok=True)
    (DATA_ROOT / "properties/qualities").mkdir(parents=True, exist_ok=True)
    (DATA_ROOT / "configuration").mkdir(parents=True, exist_ok=True)
    
    # Copy materials/properties/* → properties/materials/*
    src = DATA_ROOT / "materials/properties"
    dst = DATA_ROOT / "properties/materials"
    if src.exists():
        shutil.copytree(src, dst, dirs_exist_ok=True)
        print(f"✓ Copied {src} → {dst}")
    
    # Create properties/qualities/catalog.json
    create_qualities_catalog()
    
    # Create configuration files
    create_material_filters_config()
    create_generation_rules_config()
    create_rarity_config()
    
    # Move general/budget-config.json → configuration/budget.json
    move_file(
        DATA_ROOT / "general/budget-config.json",
        DATA_ROOT / "configuration/budget.json"
    )
    
    # Move general/socket_config.json → configuration/socket-config.json
    move_file(
        DATA_ROOT / "general/socket_config.json",
        DATA_ROOT / "configuration/socket-config.json"
    )
    
    # Create .cbconfig.json files
    create_cbconfig(DATA_ROOT / "properties", "Properties", "Package", 10)
    create_cbconfig(DATA_ROOT / "properties/materials", "Materials", "Build", 20)
    create_cbconfig(DATA_ROOT / "properties/qualities", "Qualities", "Star", 30)
    create_cbconfig(DATA_ROOT / "configuration", "Configuration", "Settings", 15)
    
    print("✓ Phase 1 Complete: New structure created\n")

def create_qualities_catalog():
    """Extract quality components from names.json files and create unified catalog"""
    print("Creating properties/qualities/catalog.json...")
    
    # Collect quality components from all names.json files
    qualities = {}
    names_files = [
        DATA_ROOT / "items/weapons/names.json",
        DATA_ROOT / "items/armor/names.json"
    ]
    
    for names_file in names_files:
        if not names_file.exists():
            continue
        
        with open(names_file, 'r', encoding='utf-8') as f:
            data = json.load(f)
        
        if 'components' not in data or 'quality' not in data['components']:
            continue
        
        for quality in data['components']['quality']:
            name = quality.get('value') or quality.get('name')
            if name not in qualities:
                qualities[name] = {
                    'name': name,
                    'rarityWeight': quality['rarityWeight'],
                    'description': quality.get('description', ''),
                    'itemTypeTraits': {}
                }
            
            # Determine item type from file path
            item_type = 'weapon' if 'weapons' in str(names_file) else 'armor'
            
            # Add traits for this item type
            if 'traits' in quality:
                qualities[name]['itemTypeTraits'][item_type] = quality['traits']
    
    # Create catalog
    catalog = {
        'version': '4.3',
        'type': 'quality_catalog',
        'lastUpdated': '2026-01-18',
        'description': 'Universal quality tiers for all items',
        'qualities': list(qualities.values())
    }
    
    output_path = DATA_ROOT / "properties/qualities/catalog.json"
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(catalog, f, indent=2)
    
    print(f"✓ Created {output_path} with {len(qualities)} qualities")

def create_material_filters_config():
    """Transform material-pools.json → material-filters.json"""
    print("Creating configuration/material-filters.json...")
    
    # Read existing material-pools.json
    pools_path = DATA_ROOT / "general/material-pools.json"
    if not pools_path.exists():
        print(f"⚠ {pools_path} not found, creating default material-filters.json")
        filters = create_default_material_filters()
    else:
        with open(pools_path, 'r', encoding='utf-8') as f:
            pools_data = json.load(f)
        
        # Transform structure
        filters = transform_material_pools(pools_data)
    
    output_path = DATA_ROOT / "configuration/material-filters.json"
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(filters, f, indent=2)
    
    print(f"✓ Created {output_path}")

def transform_material_pools(pools_data: Dict) -> Dict:
    """Transform old material-pools.json to new material-filters.json format"""
    # Update all references: @materials/properties/ → @properties/materials/
    filters = {
        'version': '4.3',
        'type': 'material_filter_config',
        'lastUpdated': '2026-01-18',
        'description': 'Item type material compatibility',
        'filters': {}
    }
    
    # Convert pools structure
    if 'pools' in pools_data:
        for pool_name, pool_data in pools_data['pools'].items():
            # Determine item type from pool name
            item_type = 'weapon' if 'weapon' in pool_name else 'armor'
            
            if item_type not in filters['filters']:
                filters['filters'][item_type] = {
                    'allowedMaterials': [],
                    'pools': {}
                }
            
            # Convert pool data
            tier = 'low_tier' if 'low' in pool_name else 'high_tier'
            filters['filters'][item_type]['pools'][tier] = {}
            
            for material_type, materials in pool_data.items():
                converted_materials = []
                for material in materials:
                    # Update reference
                    old_ref = material['materialRef']
                    new_ref = old_ref.replace('@materials/properties/', '@properties/materials/')
                    
                    converted_materials.append({
                        'materialRef': new_ref,
                        'rarityWeight': material.get('rarityWeight') or material.get('selectionWeight', 50)
                    })
                
                filters['filters'][item_type]['pools'][tier][material_type] = converted_materials
                
                # Add to allowedMaterials
                material_domain = f"@properties/materials/{material_type}"
                if material_domain not in filters['filters'][item_type]['allowedMaterials']:
                    filters['filters'][item_type]['allowedMaterials'].append(material_domain)
    
    return filters

def create_default_material_filters() -> Dict:
    """Create default material-filters.json if material-pools.json doesn't exist"""
    return {
        'version': '4.3',
        'type': 'material_filter_config',
        'lastUpdated': '2026-01-18',
        'description': 'Item type material compatibility',
        'filters': {
            'weapon': {
                'allowedMaterials': [
                    '@properties/materials/metals',
                    '@properties/materials/woods'
                ],
                'pools': {}
            },
            'armor': {
                'allowedMaterials': [
                    '@properties/materials/metals',
                    '@properties/materials/leathers'
                ],
                'pools': {}
            }
        }
    }

def create_generation_rules_config():
    """Create configuration/generation-rules.json"""
    print("Creating configuration/generation-rules.json...")
    
    rules = {
        'version': '4.3',
        'type': 'generation_rules_config',
        'lastUpdated': '2026-01-18',
        'description': 'Item generation rules and limits',
        'componentLimits': {
            'quality': {'min': 0, 'max': 1},
            'material': {'min': 0, 'max': 1},
            'prefixes': {
                'byRarity': {
                    'common': {'min': 0, 'max': 1},
                    'uncommon': {'min': 0, 'max': 1},
                    'rare': {'min': 0, 'max': 2},
                    'epic': {'min': 0, 'max': 2},
                    'legendary': {'min': 0, 'max': 3}
                }
            },
            'suffixes': {
                'byRarity': {
                    'common': {'min': 0, 'max': 0},
                    'uncommon': {'min': 0, 'max': 1},
                    'rare': {'min': 0, 'max': 1},
                    'epic': {'min': 0, 'max': 2},
                    'legendary': {'min': 0, 'max': 3}
                }
            }
        },
        'displayRules': {
            'nameFormat': '[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]',
            'showAllComponents': False,
            'showFirstPrefixOnly': True,
            'showFirstSuffixOnly': True,
            'hideComponentsAboveRarity': None
        },
        'validationRules': {
            'enforceComponentUniqueness': True,
            'allowDuplicatePrefixes': False,
            'allowDuplicateSuffixes': False,
            'allowDuplicateAcrossCategories': True
        }
    }
    
    output_path = DATA_ROOT / "configuration/generation-rules.json"
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(rules, f, indent=2)
    
    print(f"✓ Created {output_path}")

def create_rarity_config():
    """Create configuration/rarity.json"""
    print("Creating configuration/rarity.json...")
    
    rarity = {
        'version': '4.3',
        'type': 'rarity_config',
        'lastUpdated': '2026-01-18',
        'description': 'Rarity tier definitions and colors',
        'tiers': [
            {
                'name': 'Common',
                'rarityWeightRange': {'min': 50, 'max': 100},
                'color': '#FFFFFF',
                'dropChance': 0.5
            },
            {
                'name': 'Uncommon',
                'rarityWeightRange': {'min': 30, 'max': 49},
                'color': '#1EFF00',
                'dropChance': 0.25
            },
            {
                'name': 'Rare',
                'rarityWeightRange': {'min': 15, 'max': 29},
                'color': '#0070DD',
                'dropChance': 0.15
            },
            {
                'name': 'Epic',
                'rarityWeightRange': {'min': 5, 'max': 14},
                'color': '#A335EE',
                'dropChance': 0.08
            },
            {
                'name': 'Legendary',
                'rarityWeightRange': {'min': 1, 'max': 4},
                'color': '#FF8000',
                'dropChance': 0.02
            }
        ]
    }
    
    output_path = DATA_ROOT / "configuration/rarity.json"
    with open(output_path, 'w', encoding='utf-8') as f:
        json.dump(rarity, f, indent=2)
    
    print(f"✓ Created {output_path}")

def phase2_update_references():
    """Phase 2: Update all material property references (BREAKING)"""
    print("=== Phase 2: Updating References (BREAKING CHANGE) ===")
    
    reference_map = {
        '@materials/properties/metals': '@properties/materials/metals',
        '@materials/properties/woods': '@properties/materials/woods',
        '@materials/properties/leathers': '@properties/materials/leathers'
    }
    
    # Find all JSON files
    json_files = list(DATA_ROOT.rglob("*.json"))
    updated_count = 0
    
    for json_file in json_files:
        # Skip backup files
        if 'backup' in str(json_file):
            continue
        
        try:
            with open(json_file, 'r', encoding='utf-8') as f:
                content = f.read()
            
            # Check if any references need updating
            needs_update = any(old_ref in content for old_ref in reference_map.keys())
            
            if needs_update:
                # Update references
                for old_ref, new_ref in reference_map.items():
                    content = content.replace(old_ref, new_ref)
                
                # Write back
                with open(json_file, 'w', encoding='utf-8') as f:
                    f.write(content)
                
                updated_count += 1
                print(f"✓ Updated {json_file.relative_to(DATA_ROOT)}")
        
        except Exception as e:
            print(f"✗ Error updating {json_file}: {e}")
    
    print(f"✓ Phase 2 Complete: Updated {updated_count} files\n")

def phase3_migrate_names_files():
    """Phase 3: Migrate all names.json files to v4.3 format (BREAKING)"""
    print("=== Phase 3: Migrating names.json Files ===")
    
    names_files = list(DATA_ROOT.rglob("names.json"))
    
    for names_file in names_files:
        if 'backup' in str(names_file):
            continue
        
        migrate_names_file(names_file)
    
    print("✓ Phase 3 Complete: All names.json files migrated\n")

def migrate_names_file(file_path: Path):
    """Migrate a single names.json file to v4.3 format"""
    print(f"Migrating {file_path.relative_to(DATA_ROOT)}...")
    
    with open(file_path, 'r', encoding='utf-8') as f:
        data = json.load(f)
    
    # Remove quality and material components
    if 'components' in data:
        if 'quality' in data['components']:
            del data['components']['quality']
            print(f"  ✓ Removed quality components")
        
        if 'material' in data['components']:
            del data['components']['material']
            print(f"  ✓ Removed material components")
        
        # Merge descriptive → prefix if exists
        if 'descriptive' in data['components']:
            if 'prefix' not in data['components']:
                data['components']['prefix'] = []
            
            data['components']['prefix'].extend(data['components']['descriptive'])
            del data['components']['descriptive']
            print(f"  ✓ Merged descriptive → prefix")
        
        # Rename value → name in all components
        for component_type in data['components']:
            for component in data['components'][component_type]:
                if 'value' in component:
                    component['name'] = component.pop('value')
    
    # Update metadata
    data['version'] = '4.3'
    data['type'] = 'modifier_catalog'
    data['lastUpdated'] = '2026-01-18'
    
    # Write back
    with open(file_path, 'w', encoding='utf-8') as f:
        json.dump(data, f, indent=2)
    
    print(f"  ✓ Migrated to v4.3")

def phase4_cleanup():
    """Phase 4: Remove old structure"""
    print("=== Phase 4: Cleanup Old Structure ===")
    
    # Delete materials/properties/ (moved to properties/materials/)
    old_materials = DATA_ROOT / "materials/properties"
    if old_materials.exists():
        shutil.rmtree(old_materials)
        print(f"✓ Deleted {old_materials}")
    
    # Delete general/budget-config.json (moved to configuration/)
    old_budget = DATA_ROOT / "general/budget-config.json"
    if old_budget.exists():
        old_budget.unlink()
        print(f"✓ Deleted {old_budget}")
    
    # Delete general/socket_config.json (moved to configuration/)
    old_socket = DATA_ROOT / "general/socket_config.json"
    if old_socket.exists():
        old_socket.unlink()
        print(f"✓ Deleted {old_socket}")
    
    # Delete general/material-pools.json (replaced by material-filters.json)
    old_pools = DATA_ROOT / "general/material-pools.json"
    if old_pools.exists():
        old_pools.unlink()
        print(f"✓ Deleted {old_pools}")
    
    print("✓ Phase 4 Complete: Cleanup finished\n")

def create_cbconfig(dir_path: Path, display_name: str, icon: str, sort_order: int):
    """Create .cbconfig.json file for ContentBuilder UI"""
    config = {
        'displayName': display_name,
        'icon': icon,
        'sortOrder': sort_order,
        'description': f'{display_name} directory'
    }
    
    config_path = dir_path / ".cbconfig.json"
    with open(config_path, 'w', encoding='utf-8') as f:
        json.dump(config, f, indent=2)

def move_file(src: Path, dst: Path):
    """Move a file and create parent directories if needed"""
    if not src.exists():
        print(f"⚠ {src} not found, skipping")
        return
    
    dst.parent.mkdir(parents=True, exist_ok=True)
    shutil.move(str(src), str(dst))
    print(f"✓ Moved {src.name} → {dst}")

def create_backup():
    """Create backup of current JSON data"""
    print("Creating backup...")
    BACKUP_DIR.mkdir(parents=True, exist_ok=True)
    shutil.copytree(DATA_ROOT, BACKUP_DIR / "Json", dirs_exist_ok=True)
    print(f"✓ Backup created at {BACKUP_DIR}\n")

def main():
    """Run migration script"""
    import argparse
    
    parser = argparse.ArgumentParser(description='Migrate to JSON v4.3 unified architecture')
    parser.add_argument('--phase', type=int, choices=[1, 2, 3, 4], help='Run specific phase only')
    parser.add_argument('--all', action='store_true', help='Run all phases')
    parser.add_argument('--backup', action='store_true', help='Create backup before running')
    
    args = parser.parse_args()
    
    if args.backup:
        create_backup()
    
    if args.phase == 1 or args.all:
        phase1_create_structure()
    
    if args.phase == 2 or args.all:
        phase2_update_references()
    
    if args.phase == 3 or args.all:
        phase3_migrate_names_files()
    
    if args.phase == 4 or args.all:
        phase4_cleanup()
    
    if not args.phase and not args.all:
        print("Usage: python migrate-to-v4.3-unified-architecture.py --phase [1-4] | --all")
        print("\nPhases:")
        print("  1: Create new structure (non-breaking)")
        print("  2: Update references (BREAKING)")
        print("  3: Migrate names.json files (BREAKING)")
        print("  4: Cleanup old structure")

if __name__ == '__main__':
    main()
```

**Note:** The complete migration is split into 7 phases total:
- Phases 1-4: JSON data migration (covered above)
- Phases 5-7: C# code updates (covered below)

---

## Resolved Architectural Decisions

### 1. Component Type Field - **RESOLVED**

**Decision:** Infer from parent key (no explicit `componentType` field needed)

**Rationale:**
- Component type is implicit from JSON structure: `components.prefix[]`, `components.suffix[]`
- Adding explicit field creates redundancy
- Parser can determine type from parent key

**Implementation:**
```csharp
// Parser logic
if (parentKey == "prefix")
    return new ItemPrefix { ... };
else if (parentKey == "suffix")
    return new ItemSuffix { ... };
```

---

### 2. Field Rename - **RESOLVED**

**Decision:** Rename `value` → `name` in all components

**Rationale:**
- More semantic (`name` better describes the field)
- Consistent with other game objects (abilities, items use `name`)
- Clear distinction from `TraitValue.value`

**Migration:** Phase 3 script handles field rename automatically

---

### 3. Material Overrides - **RESOLVED**

**Decision:** No trait overrides when referencing materials (single source of truth)

**Rationale:**
- Materials already use `itemTypeTraits` pattern (weapon vs armor bonuses)
- Overrides create data duplication and maintenance burden
- If special case needed, create a new material definition

**Example:**
```json
// ✓ CORRECT: Use itemTypeTraits in material definition
{
  "name": "Iron",
  "itemTypeTraits": {
    "weapon": { "damageBonus": 2 },
    "armor": { "armorBonus": 5 }
  }
}

// ✗ WRONG: Don't override traits in name component
{
  "name": "Flaming",
  "materialOverride": {
    "iron": { "fireDamage": 10 }  // NO!
  }
}
```

---

### 4. Patterns Array - **RESOLVED**

**Decision:** Remove/simplify patterns array (use fixed order instead)

**Fixed Display Order:** `[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]`

**Rationale:**
- Patterns array adds complexity without significant benefit
- Fixed order is predictable and easy to understand
- Godot can customize display order if needed (not in JSON)

**Implementation:**
```csharp
public string GetDisplayName()
{
    var parts = new List<string>();
    
    // Fixed order
    if (Quality != null) parts.Add(Quality.Name);
    if (Material != null) parts.Add(Material.Name);
    if (Prefixes.Any()) parts.Add(Prefixes[0].Name);
    parts.Add(BaseName);
    if (Suffixes.Any()) parts.Add(Suffixes[0].Name);
    
    return string.Join(" ", parts);
}
```

---

### 5. Material Weights - **RESOLVED**

**Decision:** Material weights are context-only (used for selection within pools)

**Rationale:**
- `rarityWeight` in catalog = base cost calculation
- `rarityWeight` in material-filters.json pools = selection probability within that pool
- Context weights don't override catalog weights (different purposes)

**Example:**
```json
// properties/materials/metals/catalog.json
{
  "name": "Iron",
  "rarityWeight": 60  // Used for: cost = 6000 / 60 = 100 budget
}

// configuration/material-filters.json
{
  "pools": {
    "low_tier": {
      "metals": [
        {
          "materialRef": "@properties/materials/metals:iron",
          "rarityWeight": 80  // Used for: selection probability in this pool
        }
      ]
    }
  }
}
```

---

### 6. NameComponent Approach - **RESOLVED**

**Decision:** Hybrid interface approach (IItemComponent + specific classes)

**Rationale:**
- `IItemComponent` interface provides common contract
- Specific classes (`ItemQuality`, `ItemMaterial`, `ItemPrefix`, `ItemSuffix`) allow type-specific properties
- Best of both worlds: shared behavior + type safety

**Implementation:** See "Proposed Architecture" section above

---

### 7. Material Display - **RESOLVED**

**Decision:** Always show material in name (no hiding for high tiers)

**Rationale:**
- Consistent naming rules (simple to understand)
- Player always knows what item is made from
- Material traits apply regardless of display
- Can add "hide material" option later if needed

**Display Examples:**
```
"Iron Longsword" (Common)
"Mithril Longsword" (Rare)
"Mithril Godslayer Longsword of the Eternal" (Legendary)
```

---

### 8. Multiple Prefixes/Suffixes - **RESOLVED**

**Decision:** Display first only, show all in tooltip

**Display Strategy:**
- Name shows: `[Quality] [Material] [Prefix₁] [BaseName] [Suffix₁]`
- Tooltip shows: All prefixes and suffixes in separate section

**Rationale:**
- Prevents absurdly long names
- All bonuses still apply (nothing hidden from gameplay)
- Clean, readable item names

**Tooltip Example:**
```
=== Flaming Longsword of Speed ===
Fine Iron Longsword

Prefixes:
  • Flaming (+6 Fire Damage)
  • Sharp (+4 Damage)
  • Ancient (+6 Damage, +3 Crit Chance)

Suffixes:
  • of Speed (+3 DEX, +10% Attack Speed)
  • of the Bear (+4 STR)

Total Bonuses:
  +16 Damage, +6 Fire Damage, +3 Crit Chance
  +4 STR, +3 DEX, +10% Attack Speed
```

---

### 9. Trait Aggregation - **RESOLVED**

**Decision:** Option B (Keep separated, aggregate on-demand, no caching initially)

**Rationale:**
- Separated traits enable tooltip breakdown (show bonuses by source)
- On-demand aggregation is simple and always accurate
- Caching can be added later if performance issues arise (premature optimization)

**Implementation:**
```csharp
// Trait sections remain separated
public Dictionary<string, TraitValue> BaseTraits { get; set; }
public Dictionary<string, TraitValue> QualityTraits => Quality?.Traits ?? new();
public Dictionary<string, TraitValue> MaterialTraits => Material?.Traits ?? new();
// ... etc

// Aggregate on-demand
public int GetTotalFireDamage()
{
    return GetTraitValue(BaseTraits, "fireDamage")
        + GetTraitValue(QualityTraits, "fireDamage")
        + GetTraitValue(MaterialTraits, "fireDamage")
        + Prefixes.Sum(p => GetTraitValue(p.Traits, "fireDamage"))
        + Suffixes.Sum(s => GetTraitValue(s.Traits, "fireDamage"));
}
```

---

### 10. Component Uniqueness - **RESOLVED**

**Decision:** Enforce uniqueness (no duplicate prefixes/suffixes)

**Rules:**
- ✓ Unique within category (no two "Flaming" prefixes)
- ✓ Can duplicate across categories (can have "Flaming" prefix AND "of Fire" suffix)

**Rationale:**
- Prevents confusing names ("Flaming Flaming Flaming Sword")
- Encourages diverse bonuses
- Budget efficiency (no wastage)

**Implementation:**
```csharp
// Generator logic
var selectedPrefixes = new List<ItemPrefix>();
while (selectedPrefixes.Count < maxPrefixes && budget > 0)
{
    var candidate = SelectPrefix(budget);
    
    // Enforce uniqueness
    if (selectedPrefixes.Any(p => p.Name == candidate.Name))
        continue;  // Skip duplicate, try again
    
    selectedPrefixes.Add(candidate);
    budget -= candidate.Cost;
}
```

---

### 11. Breaking Change Decision - **RESOLVED**

**Decision:** Accept breaking change, move `materials/properties/` → `properties/materials/`

**Rationale:**
- Unified architecture is worth the migration cost
- DRY principle: Quality defined once, not per item type
- Consistency: Both materials and qualities use same pattern
- Clear separation: properties/ (universal), configuration/ (system), items/ (inventory)

**Migration:** Phase 2 script updates all references automatically

---

### 12. Configuration Scope - **RESOLVED**

**Decision:** Configuration domain includes 5 files:
1. `rarity.json` - Rarity tier definitions (rarityWeight ranges, colors, drop chances)
2. `budget.json` - Budget formulas (moved from general/)
3. `material-filters.json` - Item type material pools (transformed from material-pools.json)
4. `generation-rules.json` - Component limits by rarity, display rules, validation rules
5. `socket-config.json` - Socket system rules (moved from general/)

**Rationale:**
- System-wide settings separate from game data
- Easy to find and modify generation rules
- Consolidates scattered configuration files

---

### 13. general/ Directory - **RESOLVED**

**Decision:** Keep `general/` directory for other data (not deprecated)

**What Stays in general/:**
- Global constants
- Shared utilities
- Other non-configuration data

**What Moves to configuration/:**
- Budget formulas
- Socket config
- Material pools/filters

---

---

## Common Pitfalls and Troubleshooting

### Issue 1: Reference Resolution Failures After Phase 2

**Symptom:** Tests fail with "Reference not found" errors after updating references.

**Cause:** Some JSON files still use old `@materials/properties/` paths.

**Solution:**
```bash
# Search for any remaining old references
grep -r "@materials/properties/" RealmEngine.Data/Data/Json/

# Update manually or re-run Phase 2
python scripts/migrate-to-v4.3-unified-architecture.py --phase 2
```

---

### Issue 2: Quality Duplication in names.json

**Symptom:** Qualities still appearing in weapon/armor names.json after Phase 3.

**Cause:** Migration script didn't remove quality components.

**Solution:**
```bash
# Manually edit files or re-run Phase 3
python scripts/migrate-to-v4.3-unified-architecture.py --phase 3

# Verify no quality components remain
grep -r '"quality":' RealmEngine.Data/Data/Json/items/*/names.json
```

---

### Issue 3: C# Compilation Errors in Phase 5

**Symptom:** `Item.cs` doesn't compile after adding new properties.

**Cause:** Missing `IItemComponent` interface or component classes.

**Solution:**
1. Ensure all component classes created:
   - `IItemComponent.cs`
   - `ItemQuality.cs`
   - `ItemMaterial.cs`
   - `ItemPrefix.cs`
   - `ItemSuffix.cs`
2. Add to `RealmEngine.Shared/Models/` directory
3. Build incrementally: `dotnet build RealmEngine.Shared.csproj` first

---

### Issue 4: ItemGenerator Null Reference Exceptions

**Symptom:** ItemGenerator crashes when loading qualities.

**Cause:** `properties/qualities/catalog.json` not created or invalid.

**Solution:**
```bash
# Verify file exists and is valid JSON
cat RealmEngine.Data/Data/Json/properties/qualities/catalog.json | jq .

# Verify itemTypeTraits structure
jq '.qualities[0].itemTypeTraits' RealmEngine.Data/Data/Json/properties/qualities/catalog.json
```

---

### Issue 5: Material Filters Not Working

**Symptom:** Weapons/armor generated with wrong material types (e.g., leather sword).

**Cause:** `material-filters.json` pools not configured correctly.

**Solution:**
1. Verify filter structure:
```json
{
  "filters": {
    "weapon": {
      "allowedMaterials": [
        "@properties/materials/metals",
        "@properties/materials/woods"
      ]
    }
  }
}
```
2. Check MaterialPoolService uses filters correctly
3. Log material selection for debugging

---

### Issue 6: Display Names Too Long

**Symptom:** Item names like "Fine Iron Flaming Sharp Ancient Longsword of Speed of the Bear".

**Cause:** `GetDisplayName()` showing all prefixes/suffixes instead of first only.

**Solution:**
```csharp
// Correct implementation
if (Prefixes.Any())
    parts.Add(Prefixes[0].Name);  // First only!

// NOT this:
foreach (var prefix in Prefixes)  // ❌ WRONG
    parts.Add(prefix.Name);
```

---

### Issue 7: Trait Aggregation Missing Bonuses

**Symptom:** Items show correct prefixes/suffixes but traits don't add up.

**Cause:** Forgot to include component traits in aggregation.

**Solution:**
```csharp
public int GetTotalFireDamage()
{
    return GetTraitValue(BaseTraits, "fireDamage")
        + GetTraitValue(Quality?.Traits, "fireDamage")  // Don't forget!
        + GetTraitValue(Material?.Traits, "fireDamage")  // Don't forget!
        + Prefixes.Sum(p => GetTraitValue(p.Traits, "fireDamage"))
        + Suffixes.Sum(s => GetTraitValue(s.Traits, "fireDamage"));
}
```

---

### Issue 8: JSON Compliance Tests Failing

**Symptom:** Compliance tests fail with "missing required field" errors.

**Cause:** names.json files still have `value` instead of `name` field.

**Solution:**
```bash
# Re-run Phase 3 to rename fields
python scripts/migrate-to-v4.3-unified-architecture.py --phase 3

# Or manually update
sed -i 's/"value":/"name":/g' RealmEngine.Data/Data/Json/items/*/names.json
```

---

### Issue 9: Performance Degradation

**Symptom:** Item generation takes 200ms+ per item (expected: <100ms).

**Cause:** On-demand trait aggregation called too frequently.

**Solution:**
1. Profile code to find hot paths
2. Add caching if needed:
```csharp
private Dictionary<string, TraitValue>? _cachedTraits;

public Dictionary<string, TraitValue> GetTotalTraits()
{
    if (_cachedTraits == null || _traitsDirty)
    {
        _cachedTraits = ComputeTotalTraits();
        _traitsDirty = false;
    }
    return _cachedTraits;
}
```
3. Invalidate cache when components change

---

### Issue 10: Godot Integration Broken

**Symptom:** Godot can't deserialize new Item structure.

**Cause:** Godot expects old structure with flat Prefixes/Suffixes.

**Solution:**
1. Update Godot C# bindings to match new structure
2. Or create DTO for backward compatibility:
```csharp
public class ItemDTO
{
    public string Name { get; set; }
    public List<string> Prefixes { get; set; }  // Simplified
    public List<string> Suffixes { get; set; }  // Simplified
    public Dictionary<string, int> TotalTraits { get; set; }  // Pre-aggregated
    
    public static ItemDTO FromItem(Item item)
    {
        return new ItemDTO
        {
            Name = item.GetDisplayName(),
            Prefixes = item.Prefixes.Select(p => p.Name).ToList(),
            Suffixes = item.Suffixes.Select(s => s.Name).ToList(),
            TotalTraits = item.GetTotalTraits()
        };
    }
}
```

---

## Validation Checklist

Before considering migration complete, verify:

**Phase 1 (Structure Creation):**
- [ ] `properties/materials/` directory exists with metals/woods/leathers
- [ ] `properties/qualities/catalog.json` exists with 4+ qualities
- [ ] `configuration/` directory exists with 5 config files
- [ ] All new files are valid JSON (run `jq` or JSON validator)
- [ ] `.cbconfig.json` files exist for all new directories

**Phase 2 (Reference Updates):**
- [ ] No JSON files contain `@materials/properties/` (old path)
- [ ] All material references use `@properties/materials/` (new path)
- [ ] Reference integrity tests pass (no broken links)
- [ ] BudgetCalculator.cs compiles (uses new config paths)
- [ ] MaterialPoolService.cs compiles (uses material-filters.json)

**Phase 3 (names.json Migration):**
- [ ] No names.json files contain `quality` components
- [ ] No weapon names.json files contain `material` components
- [ ] No armor names.json files contain `material` components
- [ ] All components use `name` field (not `value`)
- [ ] All names.json files have `version: "4.3"`
- [ ] All names.json files have `type: "modifier_catalog"`

**Phase 4 (Cleanup):**
- [ ] `materials/properties/` directory deleted
- [ ] `general/budget-config.json` deleted
- [ ] `general/socket_config.json` deleted
- [ ] `general/material-pools.json` deleted
- [ ] No duplicate file paths remain

**Phase 5 (Code Updates):**
- [ ] `IItemComponent.cs` exists in RealmEngine.Shared/Models
- [ ] `ItemQuality.cs` exists with all required properties
- [ ] `ItemMaterial.cs` exists with CostScale property
- [ ] `ItemPrefix.cs` and `ItemSuffix.cs` exist
- [ ] `Item.cs` has new component properties
- [ ] Old properties marked `[Obsolete]`
- [ ] All model tests pass

**Phase 6 (Generator Updates):**
- [ ] ItemGenerator loads from `properties/qualities/`
- [ ] ItemGenerator applies itemTypeTraits filtering
- [ ] ItemGenerator enforces component uniqueness
- [ ] ItemGenerator respects rarity-based limits
- [ ] Display name uses first prefix/suffix only
- [ ] Tooltip data includes all components
- [ ] 1000-item generation test passes

**Phase 7 (Testing & Integration):**
- [ ] All unit tests pass (8000+ tests)
- [ ] Integration tests pass (item generation)
- [ ] Performance tests pass (<100ms per item)
- [ ] JSON compliance tests pass (all 164 files)
- [ ] Godot integration works (item display, tooltips, combat)

---

## Next Steps

### Immediate Actions

1. **Create Backup**
   ```bash
   python scripts/migrate-to-v4.3-unified-architecture.py --backup
   ```

2. **Execute Phase 1 (Non-Breaking)**
   ```bash
   python scripts/migrate-to-v4.3-unified-architecture.py --phase 1
   ```
   - Validate: New structure created, old structure intact
   - Run: `dotnet test` (should pass)

3. **Execute Phase 2 (BREAKING)**
   ```bash
   python scripts/migrate-to-v4.3-unified-architecture.py --phase 2
   ```
   - **POINT OF NO RETURN**: All references updated
   - Validate: Run reference integrity tests
   - Fix: Any broken references manually

4. **Execute Phase 3 (Breaking)**
   ```bash
   python scripts/migrate-to-v4.3-unified-architecture.py --phase 3
   ```
   - Validate: All names.json files v4.3 compliant
   - Run: JSON compliance tests

5. **Execute Phase 4 (Cleanup)**
   ```bash
   python scripts/migrate-to-v4.3-unified-architecture.py --phase 4
   ```
   - Validate: No duplicate structure remains
   - Commit: "Complete v4.3 JSON migration"

6. **Begin Code Updates (Phase 5-7)**
   - Create component classes
   - Update Item model
   - Update ItemGenerator
   - Run tests continuously

### Success Criteria

**JSON Migration (Phases 1-4):**
- [ ] New directory structure created
- [ ] All references updated to v4.3 paths
- [ ] All names.json files migrated to v4.3 format
- [ ] Old structure removed
- [ ] All JSON compliance tests pass
- [ ] No broken references

**Code Updates (Phases 5-7):**
- [ ] Component classes created (IItemComponent, ItemQuality, ItemMaterial, ItemPrefix, ItemSuffix)
- [ ] Item model updated with new properties
- [ ] ItemGenerator uses new structure
- [ ] Display name generation works
- [ ] Tooltip data structured correctly
- [ ] All unit tests pass
- [ ] Integration tests pass (1000 item generation)
- [ ] Godot integration works

### Rollback Plan

**Before Phase 2:** Easy rollback (new structure is parallel to old)
```bash
# Delete new directories
rm -rf RealmEngine.Data/Data/Json/properties
rm -rf RealmEngine.Data/Data/Json/configuration
git checkout .
```

**After Phase 2:** Expensive rollback (references updated)
```bash
# Restore from backup
rm -rf RealmEngine.Data/Data/Json
cp -r backups/v4.0-to-v4.3/Json RealmEngine.Data/Data/
git checkout .
```

**After Phase 4:** Point of no return (commit to v4.3)

---

## Documentation Updates Required

### Update These Documents:

1. **JSON Standards**
   - `docs/standards/json/README.md`
   - `docs/standards/json/CATALOG_JSON_STANDARD.md`
   - `docs/standards/json/NAMES_JSON_STANDARD.md`
   - Add new: `docs/standards/json/PROPERTIES_STANDARD.md`
   - Add new: `docs/standards/json/CONFIGURATION_STANDARD.md`

2. **Feature Documentation**
   - `docs/features/ITEM_ENHANCEMENT_SYSTEM.md`
   - Update: Component separation, tooltip structure
   - Update: Reference paths (v4.3)

3. **Proposals**
   - `docs/proposals/BUDGET_SYSTEM_FIELD_STANDARDIZATION.md`
   - Update: Configuration file paths

4. **Main Documentation**
   - `README.md` - Update JSON v4.3 section
   - `.github/copilot-instructions.md` - Update JSON architecture

### New Documents to Create:

1. **JSON v4.3 Migration Guide**
   - `docs/standards/json/V4.3_MIGRATION_GUIDE.md`
   - Detailed migration steps
   - Breaking changes list
   - Troubleshooting common issues

2. **Component System Guide**
   - `docs/features/ITEM_COMPONENT_SYSTEM.md`
   - Component classes documentation
   - Display name generation
   - Tooltip structure
   - Trait aggregation

---

## Approval Checklist

- [x] Open questions resolved (all 13 decisions made)
- [x] Component structure approved (IItemComponent + specific classes)
- [x] Migration strategy agreed upon (4 phases JSON + 3 phases code)
- [x] Performance impact acceptable (<2KB per item, <100ms generation)
- [x] Testing strategy sufficient (unit + integration + Godot)
- [x] Breaking changes accepted (v4.3 reference updates)
- [x] Ready to implement Phase 1

**Status:** ✅ **APPROVED - Ready for Implementation**

**Approved By:** User (January 18, 2026)  
**Target Start Date:** January 18, 2026  
**Estimated Completion:** February 3, 2026 (16 days)
