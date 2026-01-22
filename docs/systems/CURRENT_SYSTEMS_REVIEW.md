# Current Systems Review - RealmEngine Backend
**Date:** January 22, 2026  
**Purpose:** Pre-implementation review before expanding harvesting, loot, and crafting systems

---

## 1. Loot & Material Drop Systems ✅ **COMPLETE**

### A. Loot Tables (`general/loot-tables.json`)
**Status:** Fully implemented with wildcard support

**Structure:**
- **Sources**: enemies, chests, merchants
- **Lookup Order**: specific → category wildcard → domain wildcard → default
- **Pool References**: Points to `material-pools.json` entries

**Example Mappings:**
```json
"@enemies/goblinoids:goblin": {
  "pools": [
    "@material_pools/metals/common",
    "@material_pools/leathers/common"
  ]
}
"@enemies/dragons/*": {
  "pools": [
    "@material_pools/metals/legendary",
    "@material_pools/gems/legendary"
  ]
}
```

**Coverage:**
- ✅ Goblins, Orcs (common/uncommon metals + leathers)
- ✅ Beasts (leathers + woods)
- ✅ Dragons (legendary materials)
- ✅ Undead, Demons (specialized pools)
- ✅ Chests (5 rarity tiers: common → legendary)
- ✅ Merchants (profession-specific: blacksmith, tailor, leatherworker, jeweler)

**Missing:**
- ❌ Harvesting sources (ore veins, herb nodes, trees, fishing spots)
- ❌ Boss-specific loot tables
- ❌ Event/quest reward loot tables

### B. Material Pools (`general/material-pools.json`)
**Status:** Fully implemented for 16 material types

**Structure:** Organized as `material_type → rarity → items`

**Supported Material Types:**
- ✅ **Metals** (5 rarity tiers: copper/tin → godforged)
- ✅ **Fabrics** (5 tiers: burlap/linen → starweave)
- ✅ **Leathers** (5 tiers: scraps/rawhide → dragon-leather)
- ✅ **Woods** (5 tiers: pine/birch → ironwood)
- ✅ **Gems** (5 tiers: quartz/topaz → diamond)

**NEW Material Types (Just Added):**
- ✅ **Bones** (5 items: beast-bone → ancient-bone)
- ✅ **Crystals** (6 items: quartz-crystal → arcane-crystal)
- ✅ **Scales** (5 items: snake-scale → dragon-scale)
- ✅ **Chitins** (4 items: spider-chitin → mantis-carapace)
- ✅ **Stones** (8 items: granite → jade)
- ✅ **Ceramics** (5 items: clay-pot → mystic-ceramic)
- ✅ **Corals** (5 items: red-coral → deep-sea-coral)
- ✅ **Glass** (5 items: glass-shard → ethereal-glass)
- ✅ **Ice** (6 items: frozen-water → prismatic-ice)
- ✅ **Papers** (4 items: parchment → mystic-papyrus)
- ✅ **Rubbers** (5 items: tree-resin → living-resin)

**Architecture:**
- **Material Properties** (`properties/materials/TYPE/catalog.json`): Define stat bonuses, traits
- **Material Items** (`items/materials/TYPE/catalog.json`): Have `propertyRef` linking to properties
- **Reference System**: Items drop as loot, propertyRef resolves stats during crafting

### C. Enemy Loot System (`CombatService.GenerateLootDrops`)
**Status:** Fully functional budget-based generation

**Flow:**
1. Enemy dies → determine drop count (boss: 3, elite: 2, normal: 0-1)
2. Get loot chance based on difficulty (boss: 100%, elite: 80%, normal: 50%)
3. Determine item category from enemy type (weapons, armor, materials)
4. Create `BudgetItemRequest` with enemy type/level/budget
5. Generate item via `BudgetItemGenerationService`
6. Return loot list

**Budget Calculation:**
- **Base**: `100 × (Level²)` 
- **Multiplier**: Enemy type specific (goblin: 1.0x, dragon: 2.5x)
- **Material %**: Varies by type (undead: 20%, dragon: 40%)

**Integration:**
- ✅ Works with budget item generation
- ✅ Uses enemy-type-specific material pools
- ✅ Budget scales with level/difficulty

---

## 2. Crafting System ✅ **COMPLETE**

### A. Recipe System (`recipes/catalog.json`)
**Status:** 30 recipes implemented across 6 categories

**Recipe Categories:**
1. **Blacksmithing Refining** (4 recipes): Iron/Steel/Bronze/Mithril Ingots
2. **Blacksmithing Weapons** (2 recipes): Battleaxe, Legendary Sword
3. **Alchemy Potions** (5 recipes): Health/Mana/Stamina/Strength Elixir/Quenching Oil
4. **Enchanting Scrolls** (3 recipes): Agility/Intellect/Jeweled Scrolls
5. **Enchanting Runes** (5 recipes): Defensive/Offensive/Utility Runes + Orbs
6. **Salvaging** (7 recipes): Scrap → Materials refinement

**Recipe Structure:**
```json
{
  "name": "Iron Ingot",
  "requiredSkill": "Blacksmithing",
  "requiredSkillLevel": 1,
  "requiredStation": "Anvil",
  "requiredStationTier": 1,
  "craftingTime": 10,
  "experienceGained": 2,
  "unlockMethod": "SkillLevel",
  "producedItem": {
    "itemReference": "@items/materials/ingot:iron-ingot",
    "quantity": 1,
    "minQuality": "Common",
    "maxQuality": "Common"
  },
  "components": [
    {
      "itemReference": "@items/materials/ore:iron-ore",
      "quantity": 2
    }
  ]
}
```

**Unlock Methods:**
- `SkillLevel`: Auto-unlock at skill rank
- `Trainer`: Learn from NPC
- `Quest`: Quest reward
- `Discovery`: 5% base + 0.5% per skill level

### B. Crafting Stations
**Implemented Stations:**
- ✅ Anvil (Blacksmithing Tier 1-3)
- ✅ Alchemy Table (Alchemy)
- ✅ Enchanting Altar (Enchanting)
- ✅ Runeforge (Runecrafting)
- ✅ Forge (Salvaging)

### C. Crafting Services
**Core Components:**
- ✅ **CraftingService**: Validation, material checks, quality calculation
- ✅ **RecipeCatalogLoader**: Loads recipes from JSON
- ✅ **CraftRecipeHandler**: Executes crafting (consumes materials, creates item, awards XP)
- ✅ **LearnRecipeCommand**: Adds recipe to character
- ✅ **DiscoverRecipeCommand**: Skill-based discovery

**Quality System:**
- **Formula**: `(skillLevel × 2) + random(-10, +10) + stationTierBonus`
- **Station Tier Bonus**: Tier 1: +0, Tier 2: +10, Tier 3: +20
- **Critical Success**: 5% chance → +20 quality
- **Rarity Mapping**: Quality 0-39: Common, 40-59: Uncommon, 60-74: Rare, 75-89: Epic, 90+: Legendary

### D. Material Requirements
**Current Support:**
- ✅ Exact material references (`@items/materials/ore:iron-ore`)
- ✅ Wildcard materials (`@items/materials/wood:*` accepts any wood)
- ✅ Material categories (metals, fabrics, leathers, woods, gems)
- ✅ Quantity requirements
- ✅ Reference resolution via `ReferenceResolverService`

**Missing:**
- ❌ Material quality requirements (e.g., "Uncommon Iron Ore or better")
- ❌ Alternative material lists (e.g., "Iron OR Steel Ingot")
- ❌ Tool requirements (hammer, saw, needle)

---

## 3. Harvesting System ❌ **NOT IMPLEMENTED**

**Current State:** No harvesting code exists

**What's Missing:**
- ❌ Resource nodes (ore veins, herb plants, trees, fishing spots)
- ❌ Harvesting commands/handlers
- ❌ Harvesting skills integration
- ❌ Tool requirements (pickaxe, axe, knife, fishing rod)
- ❌ Yield calculations
- ❌ Node depletion/respawn
- ❌ Skill-based success rates
- ❌ Critical harvests (double yield)

**Data Exists:**
- ✅ Material items that COULD be harvested (ores, herbs, wood, leather)
- ✅ Material properties with rarityWeight (common → legendary)

**Architecture Needed:**
- Resource node catalog (`resources/nodes/catalog.json`)
- Harvesting skills in skill catalog (Mining, Herbalism, Woodcutting, Skinning, Fishing)
- Harvesting service with yield calculation
- Node state management (depleted, respawning)
- Location-based node spawning

---

## 4. Skills System ✅ **PARTIALLY COMPLETE**

### A. Skill Catalog (`skills/catalog.json`)
**Status:** 54 skills defined, code integration complete

**Skill Categories:**
- ✅ **Attribute Skills (24)**: Strength, Dexterity, Constitution, Intelligence, Wisdom, Charisma (4 tiers each)
- ✅ **Weapon Skills (10)**: Swords, Axes, Bows, Polearms, etc.
- ✅ **Armor Skills (4)**: Light/Medium/Heavy/Shield
- ✅ **Magic Skills (16)**: Arcane, Divine, Occult, Primal traditions + specializations
- ✅ **Profession Skills (12)**: Blacksmithing, Alchemy, Mining, Herbalism, etc.

**Profession Skills (Relevant to Harvesting):**
- ✅ **Blacksmithing**: Forging weapons/armor (+quality, +repair)
- ✅ **Leatherworking**: Creating leather goods (+stats, +durability)
- ✅ **Tailoring**: Sewing cloth armor (+stats, +enchantability)
- ✅ **Woodworking**: Crafting bows/staves (+weapon stats)
- ✅ **Jewelcrafting**: Creating jewelry (+socketing, +stats)
- ✅ **Alchemy**: Brewing potions (+strength, +duration)
- ✅ **Enchanting**: Imbuing magic (+power, +slots)
- ✅ **Runecrafting**: Carving runes (+effects, +combos)
- ✅ **Mining**: Extracting ore/gems (+yield, +rare find)
- ✅ **Herbalism**: Harvesting plants (+yield, +rare herb)
- ✅ **Cooking**: Preparing food buffs (+buff strength)
- ✅ **Fishing**: Catching fish (+catch rate, +rare fish)

### B. Skill Progression System
**Status:** Fully implemented

**Core Services:**
- ✅ **SkillCatalogService**: Loads skill definitions from JSON
- ✅ **SkillProgressionService**: XP awards, rank-ups, effect calculations
- ✅ **AwardSkillXPCommand**: Awards XP and processes rank-ups

**XP Formula:** `baseXPCost × (1 + currentRank × costMultiplier)`

**Skill Effects:**
- ✅ Mining: +1% ore yield, +0.5% rare find per rank
- ✅ Herbalism: +1% herb yield, +0.5% rare herb per rank
- ✅ Blacksmithing: +0.5% item quality per rank
- ✅ Alchemy: +1% potion strength per rank

**Integration:**
- ✅ Crafting awards skill XP
- ✅ Combat awards weapon/magic skill XP
- ❌ Harvesting does NOT award skill XP yet (no harvesting system)

---

## 5. Budget Item Generation System ✅ **COMPLETE**

### A. BudgetItemGenerationService
**Status:** Fully functional

**Flow:**
1. Receive `BudgetItemRequest` (enemy type, level, category, budget)
2. Select base item from category (weapons, armor, etc.)
3. Allocate budget: materials (25-40%), enchantments, sockets, quality
4. Select material from enemy-type-specific pool via `MaterialPoolService`
5. Apply material stats from properties via `propertyRef`
6. Generate name from pattern system
7. Return generated item

**Budget Allocation:**
- **Materials**: 25-40% (varies by enemy type)
- **Enchantments**: 15-30%
- **Sockets**: 10-20%
- **Quality**: 10-15%
- **Remaining**: Pattern components

### B. MaterialPoolService
**Status:** Fully functional

**Purpose:** Selects materials from enemy-specific pools within budget

**Methods:**
- `SelectMaterialAsync(materialType, budget)`: Returns affordable material
- `GetMaterialPercentage(enemyType)`: Returns material budget %
- `GetBudgetMultiplier(enemyType)`: Returns enemy loot quality multiplier

**Material Resolution:**
1. Get enemy type config from `enemy-types.json`
2. Look up material pool from `material-pools.json`
3. Filter materials within budget
4. Weighted random selection (rarityWeight)
5. Resolve propertyRef to get material stats
6. Return material JToken

---

## 6. Reference System ✅ **COMPLETE**

### A. JSON Reference System v4.1
**Syntax:** `@domain/path/category:item-name[filters]?.property.nested`

**Features:**
- ✅ Direct references: `@items/weapons/swords:iron-longsword`
- ✅ Wildcards: `@items/weapons/swords:*` (random sword)
- ✅ Property access: `@materials/metals:iron.durability`
- ✅ Optional references: `@items/consumables:health-potion?` (null if missing)
- ✅ Filtering: `[rarity>uncommon]`, `[level<=10]`

### B. ReferenceResolverService
**Status:** Fully implemented

**Validation:**
- ✅ 2,074 reference tests passing
- ✅ Validates all enemy loot tables
- ✅ Validates all recipe materials
- ✅ Validates all material propertyRef links

---

## 7. Salvaging System ✅ **COMPLETE**

### A. SalvageItemCommand
**Purpose:** Break down items into scrap materials

**Yield Calculation:**
- **Base Yield**: 40%
- **Skill Bonus**: +0.3% per rank (max 100%)
- **Scrap Types**: Metal, Wood, Leather, Cloth, Gems

**Material Mapping:**
- Weapons → Scrap Metal (primary) + type-specific (secondary)
- Armor → Scrap Metal + Scrap Leather
- Jewelry → Gemstone Fragments + Scrap Metal

**Refinement Chain:**
- Salvage item → scrap (3:1 ratio)
- Refine scrap → materials (3:1 ratio)
- 9 items → 3 scraps → 1 material

---

## Summary: What Exists vs. What's Needed

### ✅ **Complete Systems:**
1. **Loot Tables**: Fully mapped for enemies, chests, merchants
2. **Material Pools**: 16 material types with rarity tiers
3. **Budget Item Generation**: Functional with enemy-specific materials
4. **Crafting**: 30 recipes, 5 stations, quality system
5. **Skills**: 54 skills defined, progression working
6. **Reference System**: JSON v4.1 with 2,074 validated references
7. **Salvaging**: Scrap generation with skill-based yields

### ❌ **Missing Systems:**
1. **Harvesting**: No code exists
   - Need resource nodes
   - Need harvesting commands
   - Need yield calculations
   - Need skill integration
   - Need tool requirements

### 🔨 **Needs Expansion:**
1. **Loot Tables**: Add harvesting sources (ore veins, herb nodes, trees, fishing spots)
2. **Material Pools**: Already supports all 16 types, ready for harvesting integration
3. **Recipes**: Add more recipes for new materials (ceramics, corals, glass, ice, papers, rubbers)
4. **Skills**: Mining/Herbalism/Fishing exist in catalog, need harvesting integration

---

## Recommended Implementation Order

### Phase 1: Harvesting Foundation (Week 1)
1. **Resource Node Model** (`ResourceNode.cs`)
   - NodeId, Type (OreVein, HerbPlant, Tree, FishingSpot)
   - RequiredSkill, RequiredSkillLevel, RequiredTool
   - YieldTable (possible materials with weights)
   - RespawnTime, CurrentState (Available, Depleted, Respawning)

2. **Harvesting Commands** (MediatR)
   - `HarvestNodeCommand` (player, node, tool)
   - `HarvestNodeResult` (success, materials, XP, messages)

3. **Harvesting Service**
   - `CalculateYieldAsync(skill, node, tool)` → List<Item>
   - `CheckToolRequirement(player, node)` → bool
   - `AwardHarvestingXP(player, skill, node)` → XP amount
   - `HandleCriticalHarvest(baseYield)` → doubled yield (5% base + skill%)

4. **Resource Node Catalog** (`resources/nodes/catalog.json`)
```json
{
  "node_types": {
    "ore_veins": {
      "items": [
        {
          "name": "Iron Vein",
          "requiredSkill": "Mining",
          "requiredSkillLevel": 1,
          "requiredTool": "Pickaxe",
          "respawnTime": 300,
          "yieldTable": [
            { "item": "@items/materials/ore:iron-ore", "weight": 70, "quantity": "1-3" },
            { "item": "@items/materials/ore:copper-ore", "weight": 30, "quantity": "1-2" }
          ]
        }
      ]
    }
  }
}
```

### Phase 2: Location Integration (Week 1-2)
5. **Location Spawning**
   - Add `resourceNodes` array to Location model
   - LocationGenerator spawns nodes based on biome/danger
   - Node depletion tracking per location

6. **Tool System**
   - Add `Tool` item subtype with durability
   - EquipmentService validates tool in inventory
   - Tool quality affects yield bonus

### Phase 3: Loot Table Expansion (Week 2)
7. **Harvesting Loot Tables**
   - Add "resources" section to `loot-tables.json`
```json
"resources": {
  "@resources/nodes/ore_veins:iron-vein": {
    "pools": [
      "@material_pools/metals/common",
      "@material_pools/gems/common"
    ]
  }
}
```

8. **Material Pool Integration**
   - Harvesting uses existing material pools
   - No code changes needed (already supports 16 types)

### Phase 4: Recipe Expansion (Week 2-3)
9. **New Material Recipes**
   - Ceramics: Pottery, Decorative Armor Pieces
   - Corals: Jewelry, Underwater Gear
   - Glass: Vials, Ornaments, Sharp Weapons
   - Ice: Temporary Weapons (decay mechanic)
   - Papers: Scroll Bases, Light Armor
   - Rubbers: Elastic Gear, Padding

10. **Harvesting-Specific Recipes**
   - Refine raw ore → ingots (already exists)
   - Process herbs → reagents
   - Cure leather → hardened leather
   - Split logs → planks

---

## Next Steps: Discussion Points

1. **Harvesting Mechanics:**
   - Should nodes respawn or be permanently depleted?
   - Time-based depletion vs. quantity-based?
   - Should tools have durability or unlimited uses?
   - Critical harvest mechanics (double yield)?

2. **Skill Gating:**
   - Hard requirements (can't harvest without skill) or soft bonuses (yield/quality)?
   - Tool requirements strict or optional?
   - Rare material skill thresholds (legendary materials need rank 80+)?

3. **Loot Table Priorities:**
   - Which material types drop from which sources?
   - Should harvesting be the PRIMARY source for raw materials?
   - Should enemies drop processed materials (ingots) or raw (ore)?

4. **Recipe Additions:**
   - Priority material types for new recipes?
   - Should all 16 material types have craftable items?
   - Harvesting-crafting integration (ore → ingot → sword)?

5. **Node Spawning:**
   - Static nodes in locations or procedural spawning?
   - Biome-specific nodes (forests: herbs/wood, mountains: ore)?
   - Danger-based rarity (safe zones: common, dungeons: rare)?

---

**Ready for Discussion!** 🎯
