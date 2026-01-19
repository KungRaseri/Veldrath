# Item Component Separation Architecture (JSON v4.3)

**Date:** January 18, 2026  
**Status:** ✅ APPROVED - Ready for Implementation  
**Related:** ITEM_ENHANCEMENT_SYSTEM.md, BUDGET_SYSTEM_FIELD_STANDARDIZATION.md

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

## Proposed Architecture

### New Component Model

```csharp
namespace RealmEngine.Shared.Models;

/// <summary>
/// Represents an item's quality tier (craftsmanship level).
/// Examples: Fine, Superior, Masterwork, Legendary
/// </summary>
public class ItemQuality
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
public class ItemMaterial
{
    /// <summary>Display name (e.g., "Iron", "Mithril")</summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>Selection weight for generation (higher = more common)</summary>
    public int RarityWeight { get; set; }
    
    /// <summary>Cost multiplier for refined/exotic variants (defaults to 1.0)</summary>
    public double CostScale { get; set; } = 1.0;
    
    /// <summary>Trait bonuses provided by this material</summary>
    public Dictionary<string, TraitValue> Traits { get; set; } = new();
    
    /// <summary>Reference to JSON definition (e.g., "@materials/properties/metals:iron")</summary>
    public string? Reference { get; set; }
}

/// <summary>
/// Represents a descriptive prefix modifier.
/// Examples: Flaming, Sharp, Ancient, Divine
/// </summary>
public class ItemPrefix
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
public class ItemSuffix
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

### Phase 1: Add New Structure (Non-Breaking)

**Goal:** Introduce new component classes and properties without breaking existing code.

**Changes:**
1. Create new component classes (ItemQuality, ItemMaterial, ItemPrefix, ItemSuffix)
2. Add new properties to Item model
3. Mark old properties as `[Obsolete]`
4. Keep old properties functional during transition

**Timeline:** 1 week

### Phase 2: Update Generators

**Goal:** Modify ItemGenerator to populate new structure.

**Changes:**
1. Parse names.json components by category (quality, prefix, suffix)
2. Resolve materials to ItemMaterial objects
3. Populate new properties (Quality, Material, Prefixes, Suffixes)
4. Maintain backward compatibility by populating old properties too

**Timeline:** 2 weeks

### Phase 3: Update Consumers

**Goal:** Update all systems to use new structure.

**Changes:**
1. Tooltip generation
2. UI bindings (Godot)
3. Combat calculations
4. Save/load serialization
5. Tests

**Timeline:** 2 weeks

### Phase 4: Remove Old Properties

**Goal:** Clean up obsolete code.

**Changes:**
1. Remove `[Obsolete]` properties
2. Remove backward compatibility shims
3. Update documentation

**Timeline:** 1 week

---

## Open Questions for Discussion

### 1. NameComponent Class - Keep or Replace?

**Option A: Keep NameComponent as Unified Interface**
```csharp
public abstract class NameComponent
{
    public string Name { get; set; }
    public int RarityWeight { get; set; }
    public Dictionary<string, TraitValue> Traits { get; set; }
    public string? Reference { get; set; }
}

public class ItemQuality : NameComponent { }
public class ItemMaterial : NameComponent { public double CostScale { get; set; } }
public class ItemPrefix : NameComponent { }
public class ItemSuffix : NameComponent { }
```

**Pros:**
- Unified serialization
- Shared methods (trait merging, etc.)
- Easier to extend with new component types

**Cons:**
- Less explicit type safety
- Harder to enforce component-specific validation

**Option B: Separate Component Classes (Proposed)**
```csharp
public class ItemQuality { ... }
public class ItemMaterial { ... }
public class ItemPrefix { ... }
public class ItemSuffix { ... }
```

**Pros:**
- Explicit types, better IntelliSense
- Can add component-specific properties (CostScale on Material)
- Clear separation of concerns

**Cons:**
- Slightly more code duplication
- Need separate serialization logic

**Recommendation:** Option B for clarity, but open to discussion.

---

### 2. Material Display - Always Visible or Hide at High Tiers?

**Option A: Always Show Material**
```
"Mithril Longsword" (Common)
"Mithril Longsword" (Legendary)
```

**Pros:**
- Consistent naming
- Player always knows material
- Simpler logic

**Cons:**
- High-tier items feel less epic ("just mithril?")
- Real-world precedent: WoW hides materials for legendary items

**Option B: Hide Material for High Tiers**
```
"Mithril Longsword" (Common)
"Godslayer Longsword of the Eternal" (Legendary)  // No material shown
```

**Pros:**
- Legendary items feel more unique
- Focuses on epic prefixes/suffixes
- Follows MMO conventions

**Cons:**
- Inconsistent naming rules
- Material bonuses still apply (confusing?)
- Tooltip must show material in stats section

**Recommendation:** Start with Option A (always show), can add Option B later as a display setting.

---

### 3. Multiple Prefixes/Suffixes - Display Strategy

**Given:**
- Item has 3 prefixes: ["Flaming", "Sharp", "Ancient"]
- Item has 2 suffixes: ["of Speed", "of the Bear"]

**Option A: Display First Only (Proposed)**
```
Name: "Flaming Longsword of Speed"
Tooltip:
  Additional Prefixes: Sharp, Ancient
  Additional Suffixes: of the Bear
```

**Pros:**
- Clean, readable name
- No visual clutter
- All bonuses still tracked and applied

**Cons:**
- Player doesn't see all modifiers at a glance
- Might feel like "hidden" bonuses

**Option B: Display All (with Ellipsis)**
```
Name: "Flaming Sharp Ancient Longsword of Speed of the Bear"
Short: "Flaming ... Longsword of Speed ..."
```

**Pros:**
- All modifiers visible
- Player can identify item uniqueness

**Cons:**
- Extremely long names
- Readability suffers
- UI layout issues

**Option C: Display All Below Rare**
```
Common: "Flaming Longsword"
Rare: "Flaming Sharp Longsword of Speed"
Legendary: "Flaming ... Longsword of Speed ..." (3+ modifiers)
```

**Pros:**
- Scales with item complexity
- Rare items get full names
- Legendary uses ellipsis for space

**Cons:**
- Inconsistent display rules
- Complex logic

**Recommendation:** Option A (display first only) for simplicity. Show full list in tooltip "Additional Modifiers" section.

---

### 4. Trait Aggregation - When and How?

**Agreed: Option B (Keep Separated, Aggregate On-Demand)**

**Implementation Details to Confirm:**

```csharp
// When calculating combat stats
public int GetTotalFireDamage()
{
    var total = 0;
    
    // Base item traits
    total += GetTraitValue(BaseTraits, "fireDamage");
    
    // Component traits (keep separated)
    if (Quality != null)
        total += GetTraitValue(Quality.Traits, "fireDamage");
    if (Material != null)
        total += GetTraitValue(Material.Traits, "fireDamage");
    foreach (var prefix in Prefixes)
        total += GetTraitValue(prefix.Traits, "fireDamage");
    foreach (var suffix in Suffixes)
        total += GetTraitValue(suffix.Traits, "fireDamage");
    
    // Enchantments
    foreach (var enchantment in Enchantments)
        total += GetTraitValue(enchantment.Traits, "fireDamage");
    
    // Sockets
    total += GetSocketTrait("fireDamage");
    
    return total;
}
```

**Question:** Should we cache aggregated traits or compute on-demand every time?
- **Cache:** Performance benefit, but requires invalidation on component changes
- **On-Demand:** Simpler, always accurate, slight performance cost

**Recommendation:** Compute on-demand during Phase 2-3, add caching in Phase 4 if performance issues arise.

---

### 5. Component Uniqueness - Allow Duplicates?

**Scenario:**
```
Prefixes: ["Flaming", "Flaming", "Flaming"]
Total Fire Damage: +18 (3 × 6)
```

**Option A: Allow Duplicates**
- Pro: RNG can naturally generate duplicates
- Pro: Stacking bonuses can be interesting
- Con: Confusing name ("Flaming Flaming Flaming Sword")
- Con: Budget wastage

**Option B: Enforce Uniqueness**
- Pro: Clean names
- Pro: Encourages diverse bonuses
- Con: Generator needs uniqueness checks
- Con: RNG rejection might be expensive

**Recommendation:** Enforce uniqueness during generation. If RNG selects duplicate, reroll.

---

## Testing Strategy

### Unit Tests Required

1. **Component Creation**
   - Deserialize ItemQuality from JSON
   - Deserialize ItemMaterial from JSON with costScale
   - Deserialize ItemPrefix/Suffix from JSON

2. **Name Generation**
   - GetDisplayName() with all components
   - GetDisplayName() with partial components (no quality, no material)
   - GetShortName() strips quality/material

3. **Trait Breakdown**
   - GetTooltipData() separates traits correctly
   - GetAttributeBreakdown() sums attributes by source
   - Multiple prefixes/suffixes merge traits correctly

4. **Component Limits**
   - Common items capped at 1 prefix, 0 suffixes
   - Legendary items allow 3 prefixes, 3 suffixes
   - Budget respects component costs

5. **Uniqueness**
   - Generator rejects duplicate prefixes
   - Generator rejects duplicate suffixes
   - Unique constraints don't apply across categories (can have "Flaming" prefix and "of Fire" suffix)

### Integration Tests Required

1. **Generator End-to-End**
   - Generate 100 items, verify structure
   - Verify budgets respected
   - Verify component counts within limits

2. **Serialization**
   - Serialize item with new structure
   - Deserialize and verify all components intact
   - Old format backward compatibility

3. **Godot Integration**
   - Tooltip rendering with breakdown
   - UI bindings for component lists
   - Combat calculations use aggregated traits

---

## Performance Considerations

### Memory Impact

**Before:**
```csharp
List<NameComponent> Prefixes;  // 3-5 components × ~200 bytes = 600-1000 bytes
string Material;               // ~50 bytes
Dictionary MaterialTraits;     // ~300 bytes
```

**After:**
```csharp
ItemQuality? Quality;          // ~400 bytes (nullable)
ItemMaterial? Material;        // ~500 bytes (nullable)
List<ItemPrefix> Prefixes;     // 3 components × ~400 bytes = 1200 bytes
List<ItemSuffix> Suffixes;     // 3 components × ~400 bytes = 1200 bytes
```

**Increase:** ~2500 bytes per item (with all components)

**Mitigation:**
- Most items won't have all components (average ~1500 bytes)
- Trait dictionaries can share string keys (intern strings)
- Reference strings can be null for runtime-generated items

### Computation Impact

**Trait Aggregation:**
- On-demand: ~10-20 dictionary lookups per stat query
- With 6 attributes + 10 combat traits = ~160 lookups per full calculation
- Negligible for single-item queries, could matter for 100+ items

**Caching Strategy (if needed):**
```csharp
private Dictionary<string, TraitValue>? _cachedTotalTraits;

public Dictionary<string, TraitValue> GetTotalTraits()
{
    if (_cachedTotalTraits == null)
        _cachedTotalTraits = ComputeTotalTraits();
    return _cachedTotalTraits;
}

public void InvalidateTraitCache()
{
    _cachedTotalTraits = null;
}
```

---

## Next Steps

1. **Review and Discuss**
   - Address open questions (NameComponent, Material display, etc.)
   - Confirm component count limits
   - Decide on caching strategy

2. **Create Component Classes**
   - Implement ItemQuality, ItemMaterial, ItemPrefix, ItemSuffix
   - Add properties to Item model
   - Write unit tests

3. **Update Generator**
   - Modify names.json parsing to recognize component types
   - Implement budget allocation for new structure
   - Add uniqueness checks

4. **Test End-to-End**
   - Generate 1000 items, verify structure
   - Export sample items for Godot testing
   - Performance profiling

5. **Documentation**
   - Update ITEM_ENHANCEMENT_SYSTEM.md
   - Create Godot integration guide
   - Update JSON standards

---

## Related Documents

- [ITEM_ENHANCEMENT_SYSTEM.md](../features/ITEM_ENHANCEMENT_SYSTEM.md) - Current enhancement system
- [BUDGET_SYSTEM_FIELD_STANDARDIZATION.md](BUDGET_SYSTEM_FIELD_STANDARDIZATION.md) - Budget formula standardization
- [JSON_REFERENCE_STANDARDS.md](../standards/json/JSON_REFERENCE_STANDARDS.md) - Reference system v4.1

---

## Approval Checklist

- [ ] Open questions resolved
- [ ] Component structure approved
- [ ] Migration strategy agreed upon
- [ ] Performance impact acceptable
- [ ] Testing strategy sufficient
- [ ] Ready to implement Phase 1

**Approval Required From:** @User  
**Target Implementation Date:** TBD
