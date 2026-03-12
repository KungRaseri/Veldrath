# Item Enhancement System v1.0

**Version:** 1.0  
**Date:** December 31, 2025  
**Status:** Standard

---

## Overview

The **Hybrid Enhancement System** defines how items are generated with multiple layers of enhancement:
1. **Materials** (baked during generation)
2. **Enchantments** (baked during generation)
3. **Gem Sockets** (player customizable)
4. **Upgrade Level** (player progression)

This system balances **procedural variety** (materials + enchantments) with **player agency** (gems + upgrades).

---

## System Architecture

### Three Enhancement Layers

```
Base Item (Sword)
  ↓ Generation Phase
+ Material (Iron) ← Baked, affects base stats
  ↓
+ Enchantments (Flaming, Strength) ← Baked, locked in
  ↓
+ Gem Sockets (2 empty) ← Created during generation
  ↓ Runtime Phase
+ Gems (Ruby, Sapphire) ← Player fills sockets
  ↓
+ Upgrade Level (+3) ← Player-driven progression
  ↓
= Final Item
```

### Generation vs Runtime

| System | Phase | Modifiable | Stored In |
|--------|-------|------------|-----------|
| Base Item | Generation | ❌ No | Item properties |
| Material | Generation | ❌ No | Item.Material + MaterialTraits |
| Enchantments | Generation | ❌ No | Item.Enchantments[] |
| Gem Sockets | Generation | ❌ Count locked | Item.GemSockets[] |
| Gems | Runtime | ✅ Yes | GemSocket.Gem |
| Upgrade Level | Runtime | ✅ Yes | Item.UpgradeLevel |

---

## Rarity Weight System

### Component Contributions

Each component in item generation contributes to the total `rarityWeight`:

| Component | Contribution | Range |
|-----------|--------------|-------|
| Base Item | `base.rarityWeight` | 10-50 |
| Material | `material.rarityWeight` | 5-150 |
| Pattern | `pattern.rarityWeight` | 10-50 |
| Enchantment Components | Sum of `component.rarityWeight` | 5-150 each |
| Socket Count | `socketCount² × 5` | 5-180 |

### Total Rarity Calculation

```
totalRarityWeight = base.rarityWeight
                  + material.rarityWeight
                  + pattern.rarityWeight
                  + sum(enchantment.components.rarityWeight)
                  + (socketCount² × 5)
```

### ItemRarity Enum Mapping

The calculated `totalRarityWeight` maps to the `ItemRarity` enum for display and filtering:

```csharp
public static ItemRarity CalculateRarity(int totalWeight)
{
    return totalWeight switch
    {
        < 50 => ItemRarity.Common,
        < 100 => ItemRarity.Uncommon,
        < 200 => ItemRarity.Rare,
        < 350 => ItemRarity.Epic,
        _ => ItemRarity.Legendary
    };
}
```

| Range | ItemRarity | Description |
|-------|------------|-------------|
| 0-49 | Common | Basic items, common materials |
| 50-99 | Uncommon | Good quality, standard enchantments |
| 100-199 | Rare | Rare materials, powerful enchantments |
| 200-349 | Epic | Exceptional items, multiple enhancements |
| 350+ | Legendary | Ultimate items, perfect combinations |

---

## Material System

### Purpose
Materials define the physical composition of items and affect base stats contextually based on item type.

### Data Structure

**Location:** `items/materials/catalog.json`

```json
{
  "name": "Steel",
  "rarityWeight": 15,
  "traits": {
    "durability": 120,
    "weight": 1.1,
    "enchantability": 45
  },
  "itemTypeTraits": {
    "weapon": {
      "damage": 4,
      "critChance": 2,
      "value": 30
    },
    "armor": {
      "armorRating": 12,
      "defenseBonus": 5,
      "resistPhysical": 10,
      "value": 50
    }
  }
}
```

**Field Definitions:**

| Field | Type | Description |
|-------|------|-------------|
| `name` | string | Material display name |
| `rarityWeight` | number | Selection probability |
| `traits` | object | Shared traits (apply to all item types) |
| `itemTypeTraits` | object | Context-specific traits per item category |

### Context-Specific Application

Materials apply different bonuses based on item type:

```json
// Steel on a weapon:
{ "damage": 4, "critChance": 2, "durability": 120, "weight": 1.1 }

// Same Steel on armor:
{ "armorRating": 12, "defenseBonus": 5, "resistPhysical": 10, "durability": 120, "weight": 1.1 }
```

### Generation Flow

1. **Pattern Selection** - Pattern has `materialRef` field
2. **Material Resolution** - Reference: `@items/materials/metals:*[itemTypeTraits.weapon EXISTS]`
3. **Material Selection** - Random selection by `rarityWeight` from filtered pool
4. **Trait Application** - Apply `traits` + `itemTypeTraits[itemType]`
5. **Name Substitution** - Replace `{material}` token with material name
6. **Storage** - Store in `Item.Material` and `Item.MaterialTraits`

### Example

```json
// Pattern
{
  "pattern": "{material} {base}",
  "rarityWeight": 50,
  "materialRef": "@items/materials/metals:*[itemTypeTraits.weapon EXISTS]"
}

// Generates:
// 1. Resolve reference → Filters to: Iron, Steel, Mithril
// 2. Select by weight → "Steel" (rarityWeight: 15)
// 3. Apply traits → shared { durability: 120 } + weapon { damage: 4, critChance: 2 }
// 4. Result → "Steel Longsword" with enhanced weapon stats
```

---

## Enchantment System

### Purpose
Enchantments add magical properties to items through pattern-based generation. They are **baked** during generation and cannot be changed.

### Enchantment Types

| Type | Position | Form | Example |
|------|----------|------|---------|
| Prefix | Before base | Adjective | "Flaming Sword" |
| Suffix | After base | Noun phrase | "Sword of Fire" |
| Implicit | Hidden | Baked into base | Not shown in name |

### Data Structure

**Location:** `items/enchantments/names.json`

```json
{
  "components": {
    "element_prefix": [
      {
        "value": "Flaming",
        "rarityWeight": 25,
        "traits": {
          "fireDamage": { "value": 10, "type": "number" },
          "burnChance": { "value": 15, "type": "number" }
        }
      }
    ],
    "element_suffix": [
      {
        "value": "of Fire",
        "rarityWeight": 25,
        "traits": {
          "fireDamage": { "value": 10, "type": "number" }
        }
      }
    ],
    "combat_suffix": [
      {
        "value": "of Strength",
        "rarityWeight": 30,
        "traits": {
          "bonusStrength": { "value": 2, "type": "number" }
        }
      }
    ]
  },
  "patterns": [
    {
      "pattern": "{element_prefix}",
      "rarityWeight": 50,
      "position": "prefix"
    },
    {
      "pattern": "{element_suffix}",
      "rarityWeight": 50,
      "position": "suffix"
    },
    {
      "pattern": "{element_suffix} {combat_suffix}",
      "rarityWeight": 25,
      "position": "suffix"
    }
  ]
}
```

### Pattern Structure in Items

**Location:** Item names.json files (weapons, armor, etc.)

```json
{
  "pattern": "{enchantment_prefix} {material} {base} {enchantment_suffix}",
  "rarityWeight": 10,
  "materialRef": "@items/materials/metals:*",
  "enchantmentSlots": [
    {
      "type": "prefix",
      "ref": "@items/enchantments:*[position=prefix]",
      "rarityWeightMax": 100
    },
    {
      "type": "suffix",
      "ref": "@items/enchantments:*[position=suffix]",
      "rarityWeightMax": 150
    }
  ]
}
```

### Generation Flow

1. **Pattern Selection** - Pattern with `enchantmentSlots[]`
2. **For Each Slot:**
   a. Resolve reference with position filter
   b. Select enchantment pattern by `rarityWeight` (≤ `rarityWeightMax`)
   c. Select components for pattern
   d. Accumulate traits
3. **Trait Application** - Apply all enchantment traits to item
4. **Name Assembly** - Replace `{enchantment_prefix}` and `{enchantment_suffix}` tokens
5. **Storage** - Store in `Item.Enchantments[]` list

### Example

```json
// Item Pattern
{
  "pattern": "{material} {base} {enchantment_suffix}",
  "enchantmentSlots": [
    {
      "type": "suffix",
      "ref": "@items/enchantments:*[position=suffix]",
      "rarityWeightMax": 150
    }
  ]
}

// Enchantment Generated:
// Pattern: "{element_suffix} {combat_suffix}" (rarityWeight: 25)
// Components:
//   - element_suffix: "of Fire" (rarityWeight: 25) → traits: { fireDamage: 10 }
//   - combat_suffix: "of Strength" (rarityWeight: 30) → traits: { bonusStrength: 2 }
// Total: 25 + 30 = 55 rarityWeight

// Result: "Steel Longsword of Fire Strength"
// Traits: { fireDamage: 10, bonusStrength: 2 }
```

---

## Gem Socket System

### Purpose
Gem sockets provide **player customization** after item generation. Players find/craft gems and socket them for stat bonuses.

### Socket Count Rules

Socket count is determined by total `rarityWeight`:

| Rarity | rarityWeight Range | Socket Count |
|--------|-------------------|--------------|
| Common | 0-49 | 0-1 sockets |
| Uncommon | 50-99 | 1-2 sockets |
| Rare | 100-199 | 2-3 sockets |
| Epic | 200-349 | 3-4 sockets |
| Legendary | 350+ | 4-6 sockets |

**Formula:**
```csharp
int CalculateSocketCount(int rarityWeight)
{
    if (rarityWeight < 50) return Random.Next(0, 2);   // 0-1
    if (rarityWeight < 100) return Random.Next(1, 3);  // 1-2
    if (rarityWeight < 200) return Random.Next(2, 4);  // 2-3
    if (rarityWeight < 350) return Random.Next(3, 5);  // 3-4
    return Random.Next(4, 7);                          // 4-6
}
```

**Socket rarityWeight Contribution:**
```
socketBonus = socketCount² × 5

// Examples:
1 socket: 1² × 5 = 5
2 sockets: 2² × 5 = 20
3 sockets: 3² × 5 = 45
4 sockets: 4² × 5 = 80
5 sockets: 5² × 5 = 125
6 sockets: 6² × 5 = 180
```

### Socket Colors

Socket colors determine which gems can be inserted:

| Color | Associated Stats | Item Types |
|-------|-----------------|------------|
| **Red** | Strength, Physical damage | Weapons (melee) |
| **Blue** | Intelligence, Mana, Magic damage | Weapons (magic), Armor |
| **Green** | Dexterity, Evasion, Speed | Armor, Weapons (ranged) |
| **Yellow** | Hybrid stats (Str+Int, Dex+Wis) | Any |
| **White** | Universal (accepts any gem) | Any |
| **Prismatic** | Rainbow (counts as all colors) | Legendary only |

**Socket Color Assignment:**
```csharp
GemColor DetermineSocketColor(ItemType itemType, int socketIndex)
{
    if (itemType == ItemType.Weapon)
    {
        return socketIndex switch
        {
            0 => GemColor.Red,      // First socket: Strength
            1 => GemColor.Blue,     // Second socket: Magic
            _ => GemColor.White     // Additional: Universal
        };
    }
    
    if (itemType == ItemType.Armor)
    {
        return socketIndex switch
        {
            0 => GemColor.Green,    // First socket: Dexterity
            1 => GemColor.Yellow,   // Second socket: Hybrid
            _ => GemColor.White     // Additional: Universal
        };
    }
    
    return GemColor.White;
}
```

### Data Structure

```csharp
public class GemSocket
{
    public GemColor Color { get; set; }
    public Gem? Gem { get; set; }        // null = empty socket
    public bool IsLocked { get; set; }    // Can this be modified?
}

public class Gem
{
    public string Name { get; set; }      // "Flawless Ruby"
    public GemColor Color { get; set; }
    public int Level { get; set; }        // 1-5 (quality)
    public Dictionary<string, TraitValue> Traits { get; set; }
}
```

### Runtime Operations

**Socketing a Gem:**
```csharp
bool SocketGem(GemSocket socket, Gem gem)
{
    // Check color compatibility
    if (socket.Color != GemColor.White && 
        socket.Color != gem.Color && 
        gem.Color != GemColor.Prismatic)
    {
        return false; // Color mismatch
    }
    
    // Check if locked
    if (socket.IsLocked) return false;
    
    // Socket the gem
    socket.Gem = gem;
    return true;
}
```

**Removing a Gem:**
```csharp
Gem? RemoveGem(GemSocket socket, int goldCost)
{
    if (socket.IsLocked) return null;
    if (socket.Gem == null) return null;
    
    // Pay jeweler fee
    if (!Player.SpendGold(goldCost)) return null;
    
    Gem removed = socket.Gem;
    socket.Gem = null;
    return removed;
}
```

**Adding Sockets (Expensive):**
```csharp
bool AddSocket(Item item, GemColor color)
{
    // Check max sockets for rarity
    int maxSockets = item.Rarity switch
    {
        ItemRarity.Common => 2,
        ItemRarity.Uncommon => 3,
        ItemRarity.Rare => 4,
        ItemRarity.Epic => 5,
        ItemRarity.Legendary => 6,
        _ => 2
    };
    
    if (item.GemSockets.Length >= maxSockets) return false;
    
    // Cost increases exponentially
    int cost = 100 * (int)Math.Pow(3, item.GemSockets.Length);
    if (!Player.SpendGold(cost)) return false;
    
    // Add socket
    Array.Resize(ref item.GemSockets, item.GemSockets.Length + 1);
    item.GemSockets[^1] = new GemSocket { Color = color, Gem = null };
    
    return true;
}
```

---

## Upgrade System

### Purpose
Upgrade levels provide linear stat progression through player investment (blacksmith services).

### Upgrade Mechanics

**Levels:** 0 to +10 (max)

**Cost Formula:**
```csharp
int CalculateUpgradeCost(int currentLevel)
{
    return 100 * (int)Math.Pow(2, currentLevel);
}

// Examples:
0 → +1: 100 gold
+1 → +2: 200 gold
+2 → +3: 400 gold
+3 → +4: 800 gold
+10: 102,400 gold total
```

**Stat Bonuses Per Level:**
- **Attribute Bonuses:** +2 to all (Strength, Dexterity, etc.)
- **Damage/Defense:** +5% multiplicative

```csharp
void ApplyUpgradeMultiplier(Dictionary<string, TraitValue> traits, int level)
{
    // +2 to attribute bonuses
    string[] attributes = { "bonusStrength", "bonusDexterity", "bonusConstitution",
                           "bonusIntelligence", "bonusWisdom", "bonusCharisma" };
    
    foreach (var attr in attributes)
    {
        if (traits.ContainsKey(attr))
        {
            int current = (int)traits[attr].Value;
            traits[attr].Value = current + (level * 2);
        }
    }
    
    // +5% to damage/defense
    string[] multipliers = { "damageMultiplier", "defenseMultiplier" };
    
    foreach (var mult in multipliers)
    {
        if (traits.ContainsKey(mult))
        {
            double current = (double)traits[mult].Value;
            traits[mult].Value = current * (1.0 + (level * 0.05));
        }
    }
}
```

---

## Trait Merging Rules

When combining traits from multiple sources (base + material + enchantments + gems + upgrades):

### Number Traits
**Rule:** Take the **HIGHEST** value (not sum)

```csharp
// material: { damage: 4 }
// enchantment: { damage: 10 }
// Result: { damage: 10 }
```

**Rationale:** Prevents exponential scaling, maintains balance

### Multiplier Traits
**Rule:** **MULTIPLY** together

```csharp
// material: { damageMultiplier: 1.2 }
// enchantment: { damageMultiplier: 1.5 }
// Result: { damageMultiplier: 1.8 } // 1.2 × 1.5
```

**Rationale:** Multiplicative stacking for percentage bonuses

### String Traits
**Rule:** Take the **LAST** defined value (override)

```csharp
// material: { element: "physical" }
// enchantment: { element: "fire" }
// Result: { element: "fire" }
```

**Rationale:** Single value properties (element type, appearance)

### Boolean Traits
**Rule:** Use **OR** logic (any true = true)

```csharp
// material: { glowing: false }
// enchantment: { glowing: true }
// Result: { glowing: true }
```

**Rationale:** Additive boolean flags

---

## Complete Generation Flow

### Step-by-Step Example

```
INPUT: Generate weapon, rarityWeight budget ~150

STEP 1: Select Base Item
-----------------------
catalog: "weapons/catalog.json"
selected: "Longsword" (rarityWeight: 25)
stats: { baseDamage: 8, weight: 3.0, twoHanded: false }
Running total: 25

STEP 2: Select Pattern
-----------------------
patterns: "weapons/names.json"
selected: "{material} {base} {enchantment_suffix}" (rarityWeight: 25)
Running total: 25 + 25 = 50

STEP 3: Apply Material
-----------------------
materialRef: "@items/materials/metals:*[itemTypeTraits.weapon EXISTS]"
resolved: [Iron, Steel, Mithril, ...]
selected: "Steel" (rarityWeight: 15)
traits applied:
  shared: { durability: 120, enchantability: 45 }
  weapon: { damage: 4, critChance: 2 }
merged stats: { baseDamage: 12, critChance: 2, durability: 120 }
Running total: 50 + 15 = 65

STEP 4: Generate Enchantment
----------------------------
enchantmentSlots[0].ref: "@items/enchantments:*[position=suffix]"
enchantment pattern: "{element_suffix} {combat_suffix}" (rarityWeight: 25)
components:
  - element_suffix: "of Fire" (rarityWeight: 25) → { fireDamage: 10 }
  - combat_suffix: "of Strength" (rarityWeight: 30) → { bonusStrength: 2 }
enchantment traits: { fireDamage: 10, bonusStrength: 2 }
enchantment rarityWeight: 25 + 30 = 55
Running total: 65 + 55 = 120

STEP 5: Create Gem Sockets
---------------------------
totalRarityWeight: 120 → RARE tier
socketCount: random(2, 4) = 3
socketBonus: 3² × 5 = 45
socket colors: [Red, Blue, White] (weapon type)
sockets created: [ {Red, null}, {Blue, null}, {White, null} ]
Running total: 120 + 45 = 165

STEP 6: Finalize Item
---------------------
Final rarityWeight: 165 → RARE
ItemRarity enum: RARE
Name assembled: "Steel Longsword of Fire Strength"
Traits merged:
  baseDamage: 12 (highest: base 8 + material 4)
  fireDamage: 10 (from enchantment)
  critChance: 2 (from material)
  bonusStrength: 2 (from enchantment)
  durability: 120 (from material)

OUTPUT:
-------
Item {
  Name: "Steel Longsword of Fire Strength",
  BaseName: "Longsword",
  Material: "Steel",
  Rarity: RARE,
  TotalRarityWeight: 165,
  Traits: { baseDamage: 12, fireDamage: 10, critChance: 2, 
            bonusStrength: 2, durability: 120 },
  Enchantments: [
    { Name: "of Fire Strength", Position: Suffix, 
      Traits: { fireDamage: 10, bonusStrength: 2 } }
  ],
  GemSockets: [
    { Color: Red, Gem: null },
    { Color: Blue, Gem: null },
    { Color: White, Gem: null }
  ],
  UpgradeLevel: 0
}
```

---

## Examples by Rarity Tier

### Common Item (rarityWeight: 45)
```
Name: "Iron Sword"
Material: "Iron" (rW: 5)
Pattern: "{material} {base}" (rW: 50, no enchantments)
Base: "Sword" (rW: 20)
Sockets: 0
Total: 5 + 50 + 20 = 75 → Actually UNCOMMON
// Note: Even simple patterns can reach Uncommon tier
```

### Uncommon Item (rarityWeight: 85)
```
Name: "Iron Sword"
Material: "Iron" (rW: 5)
Pattern: "{material} {base}" (rW: 50)
Base: "Sword" (rW: 20)
Sockets: 1 (rW: 5)
Total: 5 + 50 + 20 + 5 = 80 → UNCOMMON
Traits: { damage: 10, durability: 80 }
```

### Rare Item (rarityWeight: 140)
```
Name: "Steel Longsword of Fire Strength"
Material: "Steel" (rW: 15)
Pattern: "{material} {base} {enchantment_suffix}" (rW: 25)
Base: "Longsword" (rW: 25)
Enchantment: "of Fire Strength" (Fire: 25 + Strength: 30 = 55)
Sockets: 2 (rW: 20)
Total: 15 + 25 + 25 + 55 + 20 = 140 → RARE
Traits: { damage: 12, fireDamage: 10, bonusStrength: 2, critChance: 2 }
```

### Epic Item (rarityWeight: 285)
```
Name: "Flaming Mithril Greatsword of the Titan"
Material: "Mithril" (rW: 80)
Pattern: "{enchantment_prefix} {material} {base} {enchantment_suffix}" (rW: 10)
Base: "Greatsword" (rW: 40)
Prefix: "Flaming" (rW: 25)
Suffix: "of the Titan" (rW: 80)
Sockets: 3 (rW: 45)
Total: 80 + 10 + 40 + 25 + 80 + 45 = 280 → EPIC
Traits: { damage: 20, fireDamage: 15, bonusStrength: 5, bonusConstitution: 3 }
```

### Legendary Item (rarityWeight: 475)
```
Name: "Perfect Flaming Adamantine Greatsword of the Dragon King"
Material: "Adamantine" (rW: 120)
Pattern: "{enchantment_prefix} {material} {base} {enchantment_suffix}" (rW: 10)
Base: "Greatsword" (rW: 40)
Prefix: "Perfect Flaming" (Perfect: 100 + Flaming: 25 = 125)
Suffix: "of the Dragon King" (Dragon: 80 + King: 60 = 140)
Sockets: 5 (rW: 125)
Total: 120 + 10 + 40 + 125 + 140 + 125 = 560 → LEGENDARY
Traits: { damage: 30, fireDamage: 30, bonusStrength: 10, bonusConstitution: 8,
          bonusCharisma: 5, indestructible: true }
```

---

## Related Standards

- [NAMES_JSON_STANDARD.md](../json/NAMES_JSON_STANDARD.md) - Pattern generation with enhancement fields
- [CATALOG_JSON_STANDARD.md](../json/CATALOG_JSON_STANDARD.md) - Material and item catalog structures
- [JSON_REFERENCE_STANDARDS.md](../json/JSON_REFERENCE_STANDARDS.md) - Material and enchantment references

---

## Implementation Notes

### Performance Considerations
- Material trait lookup: O(1) dictionary access
- Enchantment generation: O(n) where n = component count
- Trait merging: O(m) where m = total trait count
- Socket calculation: O(1) formula

### Future Enhancements
- **Gem Crafting** - Combine 3 lower gems → 1 higher gem
- **Enchantment Extraction** - Remove enchantments for crafting materials
- **Material Refinement** - Upgrade material quality
- **Set Bonuses** - Items from same set grant extra bonuses
- **Cursed Items** - Negative enchantments with powerful bonuses

---

## Version History

| Version | Date | Changes |
|---------|------|---------|
| 1.0 | 2025-12-31 | Initial hybrid enhancement system documentation |
