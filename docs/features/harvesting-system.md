# Harvesting System

**Status**: ✅ **Core Implementation Complete** - Engine and logic layers implemented; node content catalog pending  
**Last Updated**: January 22, 2026

---

## Implementation Progress

### Phase 1: Foundation ✅
- [x] **Resource Node Models** - `HarvestableNode`, `NodeType`, `NodeState` (`RealmEngine.Shared.Models.Harvesting`)
- [x] **Node Health System** - Depletion tracking and threshold calculations (`HarvestCalculatorService`, `NodeHealthConfig`)
- [x] **Tool Requirement System** - Tool validation and tier matching (`ToolValidationService`)
- [x] **Harvest Command** - `HarvestNodeCommand` with validation pipeline (`HarvestNodeCommandHandler`)
- [x] **Yield Calculation** - Skill + tool quality + critical harvest logic (`HarvestCalculatorService`, `CriticalHarvestService`)
- [x] **Node Spawning** - `NodeSpawnerService` for biome-based placement

### Phase 2: Content & Data 🚧 In Progress
- [ ] **Resource Node Catalog** - Static catalog data (JSON) for 30+ harvestable nodes not yet seeded; nodes created at runtime via `NodeSpawnerService`
- [ ] **Node Distribution Rules** - Biome-specific spawn rules (runtime only, no static config yet)
- [ ] **Tool Catalog Expansion** - Pickaxes, axes, sickles, fishing rods (no dedicated tool catalog yet)
- [x] **Harvesting Loot Tables** - Integrated with existing `LootTableService`
- [x] **Node Respawn Configuration** - `HarvestingConfig` / `NodeHealthConfig` (configurable per-node)

### Phase 3: Integration ✅
- [x] **Skill Integration** - `SkillProgressionService` called on harvest for skill XP awards
- [x] **Location Integration** - `NodeSpawnerService` registered and wired into the exploration pipeline
- [x] **Critical Harvest System** - `CriticalHarvestService` applied on successful harvests
- [x] **Commands** - `HarvestNodeCommand`, `GetNearbyNodesQuery`, `InspectNodeQuery` all implemented
- *Note: UI and sound effects are out of scope for the game engine layer*

### Phase 4: Testing ✅
- [x] **Integration Tests** - `HarvestingIntegrationTests.cs` (5 tests covering core harvest workflows)
- [x] **Repository Tests** - `InMemoryNodeRepositoryTests.cs`
- [ ] **Expanded test coverage** - Additional cases for edge conditions and balance tuning pending

---

## Overview

The Harvesting System allows players to gather raw materials from resource nodes in the world using specialized tools. It provides the **primary method** for acquiring crafting materials, rewarding exploration, skill investment, and proper tool usage.

### Design Philosophy

**"Active Gathering Over Passive Drops"**: While enemies and chests drop some materials, harvesting provides superior quantity and quality. Players who invest in gathering skills and tools gain significant advantages.

**"Meaningful Tool Progression"**: Better tools = better yields. A bronze pickaxe struggles with iron ore; a steel pickaxe extracts more with less waste.

**"Skill-Based Mastery"**: Higher skill ranks increase base yield, reduce tool durability loss, and unlock critical harvest chances. New players can gather, but masters excel.

**"Node Health Depletion"**: Nodes aren't unlimited. Each harvest depletes the node's "health pool" based on tool quality and skill. When depleted, the node becomes unusable until respawn.

**"Biome-Based Distribution"**: Ore veins spawn in mountains/caves, herb patches in forests/plains, fishing spots near water. Exploration is rewarded.

---

## Core Systems

### 1. Resource Nodes

**Node Types:**
- **Ore Veins** (Mining skill)
  - Copper, Tin, Iron, Silver, Gold, Mithril, Adamantine veins
  - Found in: Mountains, caves, cliff faces
  - Tools: Pickaxes (bronze → steel → mithril → adamantine)
  
- **Trees** (Woodcutting skill)
  - Oak, Ash, Pine, Yew, Ironwood, Ancient trees
  - Found in: Forests, groves, wilderness
  - Tools: Axes (bronze → steel → enchanted)
  
- **Herb Patches** (Herbalism skill)
  - Medicinal herbs, alchemical reagents, magical plants
  - Found in: Plains, forests, swamps, magical groves
  - Tools: Sickles (optional, increases yield)
  
- **Fishing Spots** (Fishing skill)
  - Freshwater, saltwater, magical pools
  - Found in: Rivers, lakes, oceans, enchanted springs
  - Tools: Fishing rods (bamboo → quality → master → legendary)
  
- **Crystal Formations** (Mining skill, high-tier)
  - Arcane crystals, gems, essence stones
  - Found in: Deep caves, magical zones, ancient ruins
  - Tools: Crystal picks (specialized, rare)
  
- **Stone Quarries** (Mining skill)
  - Granite, marble, obsidian, volcanic rock
  - Found in: Rocky areas, volcanic regions
  - Tools: Stone hammers, chisels

### 2. Node Health System

**Health Pool**: Each node has a health value (100-500 HP depending on tier)

**Depletion Calculation**:
```
depletionAmount = baseDepletion × (toolTierModifier / skillDamageReduction)

baseDepletion = 20 (standard harvest action)
toolTierModifier = 1.0 (tier 1) to 0.6 (tier 5) // Better tools = less damage
skillDamageReduction = 1.0 - (skillRank × 0.002) // Capped at 0.5 (50% reduction at rank 250)
```

**Example**:
- **Novice** (Rank 0) with Bronze Pickaxe (Tier 1): 20 × 1.0 / 1.0 = **20 damage/harvest**
- **Expert** (Rank 100) with Steel Pickaxe (Tier 3): 20 × 0.8 / 0.8 = **20 damage/harvest** (balanced)
- **Master** (Rank 200) with Mithril Pickaxe (Tier 4): 20 × 0.7 / 0.6 = **23 damage/harvest** (slightly more efficient)

**Node States**:
- **Healthy** (80-100% HP): Full yield, no penalties
- **Depleted** (40-79% HP): Normal yield
- **Exhausted** (10-39% HP): 50% yield reduction, warning message
- **Empty** (0-9% HP): Cannot harvest, respawn required

**Respawn Mechanics**:
- **Time-Based**: Node regenerates 1 HP per minute (default)
- **Quantity-Based**: After X harvests, node despawns and respawns elsewhere
- **Global Respawn**: All nodes in location reset on daily cycle
- **Configuration**: Adjustable per node type in `harvesting-config.json`

### 3. Tool System

**Tool Requirements**:

| Material Tier | Minimum Tool Tier | Recommended Tool |
|---------------|-------------------|------------------|
| **Common** (Copper, Oak) | None (hands work) | Tier 1 (Bronze/Basic) |
| **Uncommon** (Iron, Ash) | Tier 1 | Tier 2 (Iron) |
| **Rare** (Silver, Yew) | Tier 2 | Tier 3 (Steel) |
| **Epic** (Mithril, Ironwood) | Tier 3 | Tier 4 (Mithril) |
| **Legendary** (Adamantine, Ancient) | Tier 4 | Tier 5 (Adamantine) |

**Tool Quality Bonuses**:
- **Base Yield**: +10% per tool tier above minimum (Tier 3 tool on Tier 1 node = +20% yield)
- **Durability**: Higher tier tools have more uses before breaking
- **Speed**: Better tools reduce harvest time (not implemented initially)
- **Critical Chance**: Quality tools boost crit harvest chance (+1% per tier)

**Tool Durability**:
```
durabilityLoss = baseLoss × (nodeResistance / toolHardness)

baseLoss = 1 durability point per harvest
nodeResistance = 1.0 (common) to 2.0 (legendary)
toolHardness = 0.5 (tier 1) to 2.0 (tier 5)
```

**No Tool Penalties** (Hands/Improvised):
- -50% base yield
- -25% critical harvest chance
- Cannot harvest Rare+ materials
- 2x node health depletion (wasteful)

### 4. Skill-Based Yield

**Yield Formula**:
```
finalYield = baseYield × yieldMultiplier × criticalMultiplier

baseYield = node default (1-3 materials)
yieldMultiplier = 1.0 + (skillRank × 0.003) + toolTierBonus // Linear scaling, 1.0 → 1.75 at rank 250
criticalMultiplier = 1.0 (normal) or 2.0 (critical harvest)
toolTierBonus = 0.10 × (toolTier - minRequiredTier) // Capped at +0.30
```

**Skill Milestones**:
| Rank | Bonus | Unlock |
|------|-------|--------|
| 0 | +0% yield | Basic gathering |
| 25 | +7.5% yield | Tier 2 materials |
| 50 | +15% yield | Critical harvests enabled |
| 100 | +30% yield | Tier 3 materials, rare drops |
| 150 | +45% yield | Tier 4 materials |
| 200 | +60% yield | Tier 5 materials, legendary drops |
| 250 | +75% yield | Max efficiency, double crit chance |

**Skill XP Awards**:
```
xpGain = baseXP × (nodeTier + 1) × (1 + qualityBonus)

baseXP = 10 per harvest
nodeTier = 0 (common) to 4 (legendary)
qualityBonus = 0.0 (no tool) to 0.5 (tier 5 tool)
```

**Example**: Harvesting Epic Ironwood (Tier 3) with Mithril Axe (Tier 4)
- XP = 10 × (3+1) × (1 + 0.4) = **56 XP per harvest**

### 5. Critical Harvest System

**Critical Chance**:
```
critChance = baseCrit + (skillRank × 0.05%) + toolQualityBonus + locationBonus

baseCrit = 5% (universal)
skillRank scaling = 0% (rank 0) to 12.5% (rank 250)
toolQualityBonus = +1% per tool tier
locationBonus = +5% in "rich" nodes (rare spawns)
```

**Critical Effects**:
- **Double Yield**: 2x base materials (rolled separately per material)
- **Bonus Material**: 25% chance for +1 higher tier material (Iron node → Steel Ingot)
- **Rare Drop**: 10% chance for special material (gems from ore, rare wood from trees)
- **Durability Savings**: 50% reduced tool durability loss
- **XP Bonus**: +50% skill XP

**Critical Animation/Feedback**:
- Golden sparkle effect
- "Critical Harvest!" message
- Sound effect (chime/bell)
- Bonus material highlighted in loot notification

### 6. Biome & Location Integration

**Node Spawning Rules** (`harvesting-config.json`):
```json
{
  "biomes": {
    "forest": {
      "nodes": [
        { "type": "oak_tree", "density": "high", "chance": 0.6 },
        { "type": "herb_patch", "density": "medium", "chance": 0.3 },
        { "type": "iron_vein", "density": "low", "chance": 0.1 }
      ]
    },
    "mountains": {
      "nodes": [
        { "type": "iron_vein", "density": "high", "chance": 0.5 },
        { "type": "silver_vein", "density": "medium", "chance": 0.3 },
        { "type": "mithril_vein", "density": "rare", "chance": 0.1 }
      ]
    }
  }
}
```

**Density Tiers**:
- **Abundant**: 8-12 nodes per location
- **High**: 5-7 nodes per location
- **Medium**: 3-4 nodes per location
- **Low**: 1-2 nodes per location
- **Rare**: 1 node per 3-5 locations (random spawn)

**Danger-Based Scaling**:
```
nodeTier = baseLocationTier + dangerBonus

dangerBonus = 0 (safe areas) to +2 (deadly zones)
```

**Example**: Iron Vein in Forest (Tier 1 location, safe) = Tier 1 node
**Example**: Iron Vein in Cursed Forest (Tier 1 location, deadly) = Tier 3 node (higher yield)

### 7. Loot Tables Integration

**Material Drop Pools** (extends existing `material-pools.json`):
```json
{
  "sources": {
    "resources": {
      "ore_veins": {
        "copper_vein": {
          "pool": "metals",
          "tier": "common",
          "baseYield": 2,
          "materials": [
            { "ref": "@items/materials/ore:copper-ore", "weight": 80 },
            { "ref": "@items/materials/ore:tin-ore", "weight": 15 },
            { "ref": "@items/materials/gems:rough-amber", "weight": 5 }
          ]
        }
      },
      "trees": {
        "oak_tree": {
          "pool": "woods",
          "tier": "common",
          "baseYield": 3,
          "materials": [
            { "ref": "@items/materials/wood:oak-logs", "weight": 85 },
            { "ref": "@items/materials/wood:oak-bark", "weight": 10 },
            { "ref": "@items/materials/reagent:tree-sap", "weight": 5 }
          ]
        }
      }
    }
  }
}
```

**Bonus Drop System**:
- **Common Nodes**: 5% chance for uncommon material
- **Uncommon Nodes**: 10% chance for rare material
- **Rare Nodes**: 15% chance for epic material
- **Epic Nodes**: 20% chance for legendary material
- **Legendary Nodes**: 25% chance for unique/mythic material

---

## Harvesting Workflow

### Player Perspective

1. **Exploration**: Player discovers Oak Tree in forest location
2. **Inspection**: Examine node (shows required tool, material type, node health)
3. **Tool Check**: Equip Woodcutting Axe (Tier 2 Steel Axe)
4. **Harvest Action**: Execute `/harvest` command
5. **Calculation**:
   - Skill Check: Woodcutting Rank 75 (+22.5% yield)
   - Tool Bonus: Tier 2 on Tier 1 node (+10% yield)
   - Critical Roll: 12% chance (5% base + 3.75% skill + 2% tool + 1% location)
   - Result: **CRITICAL!** 6 Oak Logs + 1 Oak Bark + 1 Tree Sap (bonus rare drop)
6. **Node Depletion**: Tree health drops 18 HP (20 base × 0.9 tool efficiency)
7. **XP Gain**: +18 Woodcutting XP (10 × 1.0 tier × 1.2 tool × 1.5 crit)
8. **Loot Notification**: Materials added to inventory with golden sparkle effect

### System Flow

```
Player → HarvestNodeCommand
  ↓
Validate Tool Requirements
  ↓
Calculate Yield (Skill + Tool + Crit)
  ↓
Roll Material Drops (Loot Table)
  ↓
Apply Node Depletion
  ↓
Award Skill XP
  ↓
Update Inventory
  ↓
Check Node State (Respawn Timer)
```

---

## Profession Skills

### Mining (INT-based)
**Governs**: Ore veins, crystal formations, stone quarries

**Skill Benefits**:
- +0.3% yield per rank
- +0.05% critical chance per rank
- -0.2% tool durability loss per rank
- Unlock higher tier materials at milestones

**Related Tools**: Pickaxes (bronze, iron, steel, mithril, adamantine, crystal)

**Synergies**:
- **Investigation** (INT): Faster node discovery in locations
- **Nature** (INT): Increased yield from natural ore formations
- **Blacksmithing** (Crafting): Can repair mining tools at reduced cost

### Woodcutting (STR-based)
**Governs**: Trees, magical groves

**Skill Benefits**:
- +0.3% yield per rank
- +0.05% critical chance per rank
- -0.2% axe durability loss per rank
- Faster chopping speed at high ranks

**Related Tools**: Axes (bronze, iron, steel, enchanted, ancient)

**Synergies**:
- **Athletics** (STR): Faster tree chopping speed
- **Survival** (WIS): Better wood quality from wilderness trees
- **Carpentry** (Crafting): Can craft superior axes

### Herbalism (WIS-based)
**Governs**: Herb patches, mushroom groves, alchemical plants

**Skill Benefits**:
- +0.3% yield per rank
- +0.05% critical chance per rank
- +1% rare herb drop chance per 10 ranks
- Can identify magical plant properties

**Related Tools**: Sickles (basic, quality, master, enchanted) - *Optional*

**Synergies**:
- **Nature** (INT): Identify herb uses without consumption
- **Medicine** (WIS): Better potion crafting with gathered herbs
- **Survival** (WIS): Find herb patches faster

### Fishing (DEX-based)
**Governs**: Fishing spots (freshwater, saltwater, magical)

**Skill Benefits**:
- +0.3% catch rate per rank
- +0.05% critical chance (rare fish) per rank
- Reduced wait time between catches
- Can catch higher tier fish

**Related Tools**: Fishing rods (bamboo, quality, master, legendary, enchanted)

**Synergies**:
- **Perception** (WIS): Detect fish movement/bites faster
- **Cooking** (Profession): Better meal bonuses from caught fish
- **Swimming** (STR): Access deep water fishing spots

---

## Configuration Files

### harvesting-config.json
```json
{
  "metadata": {
    "version": "1.0",
    "type": "harvesting_config",
    "lastUpdated": "2026-01-22"
  },
  "nodeHealth": {
    "baseDepletion": 20,
    "healthThresholds": {
      "healthy": 0.8,
      "depleted": 0.4,
      "exhausted": 0.1
    },
    "respawnRate": 1,
    "respawnUnit": "minutes"
  },
  "yieldCalculation": {
    "skillScaling": 0.003,
    "toolBonusPerTier": 0.10,
    "maxToolBonus": 0.30,
    "criticalMultiplier": 2.0
  },
  "criticalHarvest": {
    "baseChance": 0.05,
    "skillScaling": 0.0005,
    "toolBonusPerTier": 0.01,
    "richNodeBonus": 0.05,
    "bonusMaterialChance": 0.25,
    "rareDropChance": 0.10,
    "durabilityReduction": 0.5,
    "xpBonus": 1.5
  },
  "toolRequirements": {
    "enforceMinimum": true,
    "noToolPenalty": 0.5,
    "noToolDepletionMultiplier": 2.0
  },
  "skillXP": {
    "baseXP": 10,
    "tierMultiplier": "nodeTier + 1",
    "qualityBonus": "0.1 per tool tier"
  }
}
```

### resource-nodes.json
```json
{
  "metadata": {
    "version": "1.0",
    "type": "resource_node_catalog",
    "lastUpdated": "2026-01-22",
    "totalNodes": 36
  },
  "node_types": {
    "ore_veins": {
      "copper_vein": {
        "name": "Copper Vein",
        "tier": "common",
        "skill": "@skills/profession:mining",
        "minToolTier": 0,
        "health": 100,
        "baseYield": 2,
        "lootTable": "ore_veins_copper",
        "biomes": ["mountains", "caves", "hills"],
        "rarityWeight": 60
      },
      "iron_vein": {
        "name": "Iron Vein",
        "tier": "uncommon",
        "skill": "@skills/profession:mining",
        "minToolTier": 1,
        "health": 150,
        "baseYield": 2,
        "lootTable": "ore_veins_iron",
        "biomes": ["mountains", "deep_caves"],
        "rarityWeight": 40
      }
    },
    "trees": {
      "oak_tree": {
        "name": "Oak Tree",
        "tier": "common",
        "skill": "@skills/profession:woodcutting",
        "minToolTier": 0,
        "health": 120,
        "baseYield": 3,
        "lootTable": "trees_oak",
        "biomes": ["forest", "plains"],
        "rarityWeight": 70
      }
    }
  }
}
```

---

## Economic Balance

### Material Value Hierarchy

**Primary Source Comparison**:
- **Harvesting**: 100% base yield, skill/tool scaled (BEST for quantity)
- **Enemy Drops**: 30% of harvesting yield, no skill scaling
- **Chest Loot**: 50% of harvesting yield, random quality
- **Vendor Purchase**: 200% market price, unlimited stock

**Harvesting Advantages**:
- ✅ **Highest Yield**: 2-4x more materials than other sources
- ✅ **Quality Control**: Critical harvests guarantee better materials
- ✅ **Cost-Free**: Only tool durability cost (1 tool = 500+ harvests)
- ✅ **Skill Progression**: Improves over time with practice
- ✅ **Renewable**: Nodes respawn, infinite materials

**Harvesting Disadvantages**:
- ❌ **Time Investment**: Must travel to nodes, harvest manually
- ❌ **Tool Dependency**: Rare materials require expensive tools
- ❌ **Skill Gating**: Low ranks struggle with high-tier nodes
- ❌ **Location Requirement**: Must discover/access specific biomes

### Crafting Integration

**Material Sources Priority**:
1. **Harvesting** - Primary for raw materials (ore, wood, herbs, fish)
2. **Crafting** - Refine raw → processed (ore → ingots, logs → planks)
3. **Enemy Drops** - Exotic materials (scales, bones, essences)
4. **Vendors** - Emergency/rare materials when needed

**Example Crafting Chain**:
```
Harvest Oak Logs (Woodcutting) 
  → Refine to Oak Planks (Carpentry recipe)
  → Craft Oak Staff (Staffcrafting station)
  → Enchant with Fire Enchantment (Enchanting Altar)
```

---

## UI/UX Considerations (Godot Integration)

### Commands
- `/harvest [target]` - Harvest targeted node
- `/inspect [target]` - Show node info (health, materials, requirements)
- `/nodes` - List nearby harvestable nodes
- `/repair [tool]` - Repair equipped tool (requires materials)

### Visual Feedback
- **Node Indicators**: Shimmering aura around harvestable nodes
- **Tool Icon**: Display required tool icon when targeting node
- **Health Bar**: Show node health % when inspecting
- **Yield Preview**: Estimated material count based on skill/tool
- **Critical Animation**: Gold sparkles + particle effects

### Node Interaction
- **Mouse Hover**: Highlight node, show name + tier
- **Right Click**: Open harvest context menu
- **Keyboard**: Target nearest node with hotkey, harvest with action key
- **Mobile**: Tap to target, tap again to harvest

---

## Progression Path Example

### Novice Harvester (Rank 0-25)
- **Tools**: Bronze/Basic tier 1 tools
- **Nodes**: Common materials (Copper, Oak, basic herbs)
- **Yield**: 2-3 materials per harvest
- **Income**: ~100 gold/hour from selling materials

### Apprentice Harvester (Rank 25-75)
- **Tools**: Iron/Quality tier 2 tools
- **Nodes**: Uncommon materials (Iron, Ash, medicinal herbs)
- **Yield**: 3-4 materials per harvest
- **Income**: ~300 gold/hour from selling materials

### Expert Harvester (Rank 75-150)
- **Tools**: Steel/Master tier 3 tools
- **Nodes**: Rare materials (Silver, Yew, alchemical reagents)
- **Yield**: 4-5 materials per harvest, occasional crits
- **Income**: ~600 gold/hour from selling materials

### Master Harvester (Rank 150-250)
- **Tools**: Mithril/Legendary tier 4-5 tools
- **Nodes**: Epic/Legendary materials (Mithril, Ironwood, rare gems)
- **Yield**: 5-7 materials per harvest, frequent crits
- **Income**: ~1500 gold/hour from selling materials

---

## Related Systems

- [Skills System](skills-system.md) - Mining, Woodcutting, Herbalism, Fishing progression
- [Crafting System](crafting-system.md) - Raw material processing and item creation
- [Inventory System](inventory-system.md) - Material storage and organization
- [Exploration System](exploration-system.md) - Node discovery and biome navigation
- [Shop System](shop-system-integration.md) - Material vendors and tool purchases
- [Progression System](progression-system.md) - Skill XP awards and milestones

---

## Future Enhancements

### Advanced Features (Post-Launch)
- **Resource Caravans**: Automated material gathering with NPC workers
- **Node Claiming**: Personal/guild-owned nodes with exclusive access
- **Seasonal Variations**: Weather/time affecting node spawn rates
- **Legendary Nodes**: Ultra-rare one-time harvest with unique materials
- **Harvesting Quests**: "Gather 50 Oak Logs" daily quests
- **Material Transmutation**: Convert excess materials to needed types
- **Gathering Pets**: Companions that boost yield or find hidden nodes

### Quality of Life
- **Auto-Harvest**: Hold key to continuously harvest until node depleted
- **Skill Notifications**: Alert when skill rank increases
- **Node Markers**: Map pins for discovered nodes
- **Material Tracking**: "X/Y materials needed for recipe" quest tracker
- **Batch Processing**: Harvest multiple nodes in sequence
- **Tool Swap**: Auto-equip correct tool when targeting node

---

## Technical Implementation Notes

### Models
```csharp
public class HarvestableNode
{
    public string NodeId { get; set; }
    public string NodeType { get; set; } // "ore_vein", "tree", "herb_patch"
    public string MaterialTier { get; set; } // "common", "legendary"
    public int CurrentHealth { get; set; }
    public int MaxHealth { get; set; }
    public DateTime LastHarvestedAt { get; set; }
    public int TimesHarvested { get; set; }
    public string LocationId { get; set; }
    public string BiomeType { get; set; }
}

public class HarvestResult
{
    public bool Success { get; set; }
    public List<ItemDrop> MaterialsGained { get; set; }
    public int SkillXPGained { get; set; }
    public bool WasCritical { get; set; }
    public int NodeHealthRemaining { get; set; }
    public int ToolDurabilityLost { get; set; }
    public string Message { get; set; }
}
```

### Services
- **NodeSpawnerService**: Generate nodes in locations based on biome rules
- **HarvestCalculatorService**: Compute yield, depletion, XP
- **ToolValidationService**: Check tool requirements, calculate bonuses
- **CriticalHarvestService**: Roll and apply critical harvest effects

### Commands
- **HarvestNodeCommand**: Main harvesting command
- **InspectNodeCommand**: Query node details
- **GetNearbyNodesQuery**: List harvestable nodes in location

---

## Testing Strategy

### Unit Tests
- Yield calculation with various skill/tool combinations
- Node health depletion formulas
- Critical harvest probability
- Tool requirement validation
- XP award calculations

### Integration Tests
- Full harvest workflow (command → yield → inventory → XP)
- Node respawn cycles
- Multiple harvests on same node
- Tool durability tracking across harvests

### Balance Tests
- Gold/hour rates at different skill tiers
- Material supply vs crafting demand
- Node spawn density vs player population
- Tool cost vs harvesting profit

---

**Next Steps**: 
1. Review and approve design decisions
2. Create `resource-nodes.json` catalog with 30+ nodes
3. Implement `HarvestNodeCommand` and calculator services
4. Add harvesting loot tables to existing `loot-tables.json`
5. Integrate with exploration system for node spawning
6. Test and balance yield rates
