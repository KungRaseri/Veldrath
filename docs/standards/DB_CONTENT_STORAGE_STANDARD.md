# DB Content Storage Standard

**Version:** 2.0  
**Last Updated:** 2026-03-13  
**Status:** Active — supersedes v1.0 (single `content_items` JSONB table, now dropped)

## Overview

All game content is stored in **typed, domain-specific tables** in PostgreSQL. Every content entity has strongly-typed columns for fields queried or filtered at the DB level, and EF Core `ToJson()` owned entities for variable fields (stats, traits) that are consumed entirely in-engine.

JSON files under `RealmEngine.Data/Data/Json/` remain the **authoring format only**. Content is imported via the seed pipeline and never read from the filesystem at runtime.

The v1.0 single `content_items` JSONB table is **dropped** and replaced by this schema.

---

## Design Rules

### When a field is a typed column

- It is used in a `WHERE` clause at the DB level (level range, rarity filter, zone lookup)
- It is a FK target from another table
- It is displayed as a first-class field in RealmForge

### When a field belongs in an owned JSON entity (`ToJson()`)

- It is always loaded with the parent entity — never queried in isolation
- Its schema may evolve without a DB migration (new nullable properties on the C# class = zero migration)
- Examples: `Stats`, `Traits`, `Properties` sections of a catalog item

### Never

- Raw `string Data` columns storing serialised JSON blobs
- EAV `(entity_id, key, value)` tables — they are JSONB with worse SQL
- Nullable columns for every possible trait key

---

## Shared Base Properties

Every content entity table has these columns:

| Column | Type | Notes |
|---|---|---|
| `Id` | `uuid` PK | |
| `Slug` | `varchar(128)` | Unique within `TypeKey`. URL-safe, lowercase, hyphenated. |
| `TypeKey` | `varchar(64)` | Subcategory within the domain — e.g. `"wolves"`, `"active"` |
| `DisplayName` | `varchar(256)` | Human-readable label for RealmForge and logs |
| `RarityWeight` | `int` | Higher = more common. Range 1–100. |
| `IsActive` | `bool` | False = excluded from generation and reference resolution |
| `Version` | `int` | Incremented by the import pipeline on each upsert |
| `UpdatedAt` | `timestamptz` | Set by the import pipeline on each upsert |

Unique index on `(TypeKey, Slug)` on every content table.

---

## Reference Resolution

### The old `@domain/path:slug` string syntax

The `@reference` syntax was required because JSON files have no FK relationships. **At the DB layer, references become real relationships:**

| Relationship type | Mechanism |
|---|---|
| One entity always references the same target table | Typed `Guid` FK column + EF navigation property |
| One entity references one of several target tables (polymorphic) | `(Domain, Slug)` value pair resolved via `content_registry` |

### `content_registry` table

Every content entity row registers itself here on upsert. It is the routing table for polymorphic reference resolution (e.g. a loot table entry that could be a weapon, armor, or consumable):

```sql
CREATE TABLE content_registry (
    id          UUID         NOT NULL,   -- PK of the entity in its own table
    table_name  VARCHAR(64)  NOT NULL,   -- e.g. 'weapons', 'armor', 'items'
    domain      VARCHAR(64)  NOT NULL,   -- e.g. 'items/weapons'
    type_key    VARCHAR(64)  NOT NULL,
    slug        VARCHAR(128) NOT NULL,
    CONSTRAINT pk_content_registry PRIMARY KEY (domain, type_key, slug)
);
CREATE INDEX ON content_registry (id);
```

Resolution of a cross-domain reference: `SELECT id, table_name FROM content_registry WHERE domain=? AND type_key=? AND slug=?` → then query the correct typed table.

### `@reference` strings in import JSON

The import pipeline translates `@abilities/active:basic-attack` into a Guid FK by looking up the target slug in the relevant typed table before inserting the FK column. The `@reference` string syntax exists only in the authoring JSON — it never appears in the DB.

---

## Content Tables

### Abilities

Source: `abilities/active/`, `abilities/passive/`, `abilities/reactive/`, `abilities/ultimate/`

```
abilities
  Id, Slug, TypeKey (= ability sub-path, e.g. "active/offensive"),
  DisplayName, RarityWeight, IsActive, Version, UpdatedAt
  AbilityType     varchar(32)   NOT NULL   -- 'active' | 'passive' | 'reactive' | 'ultimate'
  Stats           jsonb                    -- cooldown, manaCost, castTime, range, etc.
  Effects         jsonb                    -- damage, heal, buffs, conditions applied
  Traits          jsonb                    -- hasCooldown, requiresTarget, isAoE, etc.
```

Abilities are shared across multiple entity types via junction tables (see Shared Abilities below).

---

### Enemies

Source: `enemies/{type_key}/catalog.json`

```
enemies
  Id, Slug, TypeKey (= family, e.g. "wolves"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  MinLevel        int           NOT NULL
  MaxLevel        int           NOT NULL
  LootTableId     uuid          FK → loot_tables.Id (nullable)
  Stats           jsonb         -- health, mana, strength, dex, int, xpReward, etc.
  Traits          jsonb         -- aggressive, packHunter, fireImmune, etc.
  Properties      jsonb         -- movementSpeed, attackRange, detectRadius, etc.
```

Enemy abilities:
```
enemy_ability_pool
  EnemyId         uuid  FK → enemies.Id
  AbilityId       uuid  FK → abilities.Id
  UseChance       float NOT NULL   -- 0.0–1.0
  PRIMARY KEY (EnemyId, AbilityId)
```

---

### Weapons

Source: `items/weapons/catalog.json`

```
weapons
  Id, Slug, TypeKey (= weapon category, e.g. "swords"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  WeaponType      varchar(32)   NOT NULL   -- 'sword' | 'axe' | 'bow' | etc.
  DamageType      varchar(32)   NOT NULL   -- 'physical' | 'fire' | etc.
  HandsRequired   int           NOT NULL   -- 1 or 2
  Stats           jsonb         -- damageMin, damageMax, attackSpeed, critChance, range, weight, durability
  Traits          jsonb         -- twoHanded, throwable, silvered, magical, etc.
```

---

### Armor

Source: `items/armor/catalog.json`

```
armor
  Id, Slug, TypeKey (= armor slot, e.g. "chest"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  ArmorType       varchar(32)   NOT NULL   -- 'light' | 'medium' | 'heavy' | 'shield'
  EquipSlot       varchar(32)   NOT NULL   -- 'head' | 'chest' | 'legs' | 'feet' | 'hands' | 'offhand'
  Stats           jsonb         -- armorRating, magicResist, weight, durability, movementPenalty
  Traits          jsonb         -- stealth_penalty, fireResist, coldResist, etc.
```

---

### Items (general)

Source: `items/consumables/`, `items/crystals/`, `items/essences/`, `items/gems/`, `items/orbs/`, `items/runes/`

```
items
  Id, Slug, TypeKey (= item category), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  ItemType        varchar(32)   NOT NULL   -- 'consumable' | 'crystal' | 'gem' | 'rune' | etc.
  Stats           jsonb         -- weight, stackSize, value, effectPower, duration, etc.
  Traits          jsonb         -- stackable, questItem, unique, soulbound, etc.
```

---

### Materials

Source: `items/materials/{material_type}/catalog.json`

```
materials
  Id, Slug, TypeKey (= material family, e.g. "ingot"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  MaterialFamily  varchar(32)   NOT NULL   -- 'metal' | 'wood' | 'leather' | 'gem' | etc.
  CostScale       float         NOT NULL   -- multiplier in budget formula: cost = (6000/rarityWeight) × costScale
  Stats           jsonb         -- hardness, weight, conductivity, magicAffinity, etc.
  Traits          jsonb         -- fireResist, flexible, brittle, enchantable, etc.
```

---

### Enchantments

Source: `items/enchantments/catalog.json`

```
enchantments
  Id, Slug, TypeKey, DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  TargetSlot      varchar(32)   -- 'weapon' | 'armor' | 'any' (nullable = any)
  Stats           jsonb         -- bonusDamage, bonusArmor, manaCostReduction, etc.
  Traits          jsonb         -- stackable, exclusive, requiresMagicItem, etc.
```

---

### Skills

Source: `skills/catalog.json`

```
skills
  Id, Slug, TypeKey, DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  MaxRank         int           NOT NULL
  Stats           jsonb         -- xpPerRank, bonusPerRank, etc.
  Traits          jsonb         -- passive, combat, crafting, social, etc.
```

---

### Spells

Source: `spells/catalog.json`

```
spells
  Id, Slug, TypeKey (= school, e.g. "fire"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  School          varchar(32)   NOT NULL
  Stats           jsonb         -- manaCost, castTime, cooldown, range, damage, etc.
  Traits          jsonb         -- requiresStaff, aoe, channeled, etc.
```

---

### Classes

Source: `classes/catalog.json`

```
classes
  Id, Slug, TypeKey, DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  HitDie          int           NOT NULL
  PrimaryStat     varchar(32)   NOT NULL
  Stats           jsonb         -- baseHealth, baseMana, statGrowth, etc.
  Traits          jsonb         -- canDualWield, canWearHeavy, spellcaster, etc.
```

Class ability unlocks:
```
class_ability_unlocks
  ClassId         uuid  FK → classes.Id
  AbilityId       uuid  FK → abilities.Id
  LevelRequired   int   NOT NULL
  Rank            int   NOT NULL DEFAULT 1
  PRIMARY KEY (ClassId, AbilityId)
```

---

### Backgrounds

Source: `backgrounds/catalog.json`

```
backgrounds
  Id, Slug, TypeKey, DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  Stats           jsonb   -- startingSkillBonuses, startingGold, statBonuses, etc.
  Traits          jsonb   -- regional, noble, criminal, etc.
```

---

### NPCs

Source: `npcs/{category}/catalog.json`

```
npcs
  Id, Slug, TypeKey (= npc category, e.g. "merchants"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  Faction         varchar(64)   -- nullable, soft ref
  Stats           jsonb         -- health, disposition, tradeSkill, etc.
  Traits          jsonb         -- hostile, shopkeeper, questGiver, etc.
  Schedule        jsonb         -- times and locations throughout the day
```

NPC abilities:
```
npc_abilities
  NpcId           uuid  FK → npcs.Id
  AbilityId       uuid  FK → abilities.Id
  PRIMARY KEY (NpcId, AbilityId)
```

---

### Quests

Source: `quests/catalog.json`

```
quests
  Id, Slug, TypeKey, DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  MinLevel        int           NOT NULL
  Stats           jsonb         -- xpReward, goldReward, reputationReward, etc.
  Traits          jsonb         -- repeatable, mainStory, timed, etc.
  Objectives      jsonb         -- list of objective definitions
  Rewards         jsonb         -- list of reward definitions (item slugs, xp, gold)
```

---

### Recipes

Source: `recipes/catalog.json`

```
recipes
  Id, Slug, TypeKey (= craft type, e.g. "blacksmithing"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  OutputItemDomain  varchar(64)   -- resolved via content_registry
  OutputItemSlug    varchar(128)
  OutputQuantity    int           NOT NULL DEFAULT 1
  CraftingSkill     varchar(64)   NOT NULL
  CraftingLevel     int           NOT NULL
  Traits            jsonb         -- discoverable, requiresStation, etc.

recipe_ingredients
  RecipeId          uuid  FK → recipes.Id
  ItemDomain        varchar(64)   NOT NULL   -- resolved via content_registry
  ItemSlug          varchar(128)  NOT NULL
  Quantity          int           NOT NULL
  IsOptional        bool          NOT NULL DEFAULT false
  PRIMARY KEY (RecipeId, ItemDomain, ItemSlug)
```

---

### Loot Tables

Source: `loot-tables/{type}/catalog.json`

```
loot_tables
  Id, Slug, TypeKey (= loot context, e.g. "enemies", "chests", "harvesting"),
  DisplayName, RarityWeight, IsActive, Version, UpdatedAt
  Traits          jsonb   -- boss, elite, common, etc.

loot_table_entries
  Id              uuid  PK
  LootTableId     uuid  FK → loot_tables.Id
  ItemDomain      varchar(64)   NOT NULL   -- resolved via content_registry
  ItemSlug        varchar(128)  NOT NULL
  DropWeight      int           NOT NULL   -- relative weight within this table
  QuantityMin     int           NOT NULL DEFAULT 1
  QuantityMax     int           NOT NULL DEFAULT 1
  IsGuaranteed    bool          NOT NULL DEFAULT false
```

---

### Organizations

Source: `organizations/{type}/catalog.json`

```
organizations
  Id, Slug, TypeKey (= org type: "factions", "guilds", "businesses", "shops"),
  DisplayName, RarityWeight, IsActive, Version, UpdatedAt
  OrgType         varchar(32)   NOT NULL
  Stats           jsonb         -- reputation thresholds, member count, wealth, etc.
  Traits          jsonb         -- hostile, joinable, hasShop, questGiver, etc.
```

---

### Material Properties

Source: `properties/materials/{family}/catalog.json`

```
material_properties
  Id, Slug, TypeKey (= material family, e.g. "metals"), DisplayName,
  RarityWeight, IsActive, Version, UpdatedAt
  MaterialFamily  varchar(32)   NOT NULL
  CostScale       float         NOT NULL
  Stats           jsonb         -- all material-specific numeric properties
  Traits          jsonb         -- conducting, brittle, magical, etc.
```

---

### World Locations

Source: `world/environments/`, `world/locations/`, `world/regions/`

```
world_locations
  Id, Slug, TypeKey (= location type: "environments", "locations", "regions"),
  DisplayName, RarityWeight, IsActive, Version, UpdatedAt
  LocationType    varchar(32)   NOT NULL
  Stats           jsonb         -- size, dangerLevel, population, etc.
  Traits          jsonb         -- isIndoor, hasMerchant, pvpEnabled, etc.
```

---

### Dialogue

Source: `social/dialogue/{farewells,greetings,responses,styles}/catalog.json`

```
dialogue
  Id, Slug, TypeKey (= dialogue type: "greetings", "farewells", "responses", "styles"),
  DisplayName, RarityWeight, IsActive, Version, UpdatedAt
  Speaker         varchar(64)   -- nullable, NPC type or 'player'
  Stats           jsonb         -- tone, formality, etc.
  Traits          jsonb         -- hostile, friendly, merchant, etc.
  Lines           jsonb         -- list of dialogue line strings
```

---

## Shared Abilities — Summary

Abilities live once in the `abilities` table. Each entity type that can have abilities has its own junction table with entity-specific metadata. This supports both directions: "what can this entity do?" and "which entities use ability X?".

| Junction table | Entity FK | Extra columns |
|---|---|---|
| `class_ability_unlocks` | `ClassId` | `LevelRequired`, `Rank` |
| `enemy_ability_pool` | `EnemyId` | `UseChance` |
| `npc_abilities` | `NpcId` | — |

---

## Name Patterns — Relational Tables

Source: `*/names.json`

Fully relational — no JSONB. Patterns and components are individually addressable rows for RealmForge editing.

```
name_pattern_sets
  Id              uuid  PK
  EntityPath      varchar(128)  NOT NULL UNIQUE   -- e.g. 'enemies/wolves', 'items/weapons'
  DisplayName     varchar(256)
  SupportsTraits  bool          NOT NULL DEFAULT false
  Version         int           NOT NULL DEFAULT 1
  UpdatedAt       timestamptz   NOT NULL

name_patterns
  Id              uuid  PK
  SetId           uuid  FK → name_pattern_sets.Id
  Template        varchar(256)  NOT NULL   -- e.g. '{prefix} {base}', '{base} the {suffix}'
  RarityWeight    int           NOT NULL

name_components
  Id              uuid  PK
  SetId           uuid  FK → name_pattern_sets.Id
  ComponentKey    varchar(64)   NOT NULL   -- e.g. 'prefix', 'base', 'suffix'
  Value           varchar(128)  NOT NULL   -- the actual word/token
  SortOrder       int           NOT NULL DEFAULT 0
  UNIQUE (SetId, ComponentKey, Value)
```

---

## Configuration — JSONB Table

Source: `configuration/*.json`

Config files have completely unique schemas per key — JSONB is the correct call here. No `ToJson()` owned types because config is never queried field-by-field at runtime.

```
game_config
  ConfigKey       varchar(64)   PRIMARY KEY   -- 'experience' | 'rarity' | 'budget' | etc.
  Data            jsonb         NOT NULL
  Version         int           NOT NULL DEFAULT 1
  UpdatedAt       timestamptz   NOT NULL
```

---

## Trait Definitions — Vocabulary Table

Defines the known trait keys across the game. Does **not** store values — values live in each entity's `Traits` jsonb column. This is the single source of truth for what a trait means and which entity types it applies to.

```
trait_definitions
  Key             varchar(64)   PRIMARY KEY   -- e.g. 'aggressive', 'fireResist', 'stackable'
  ValueType       varchar(16)   NOT NULL       -- 'bool' | 'int' | 'float' | 'string'
  Description     varchar(256)
  AppliesTo       text[]                       -- ['enemies', 'weapons', 'armor', '*']
```

Adding a new trait = one INSERT into `trait_definitions`. No migration. No entity table changes.

---

## What Stays in JSON

| File | Reason |
|---|---|
| `.cbconfig.json` | RealmForge UI metadata — not game runtime data |
| `backups/` | Archive directory |

---

## Import Pipeline

```bash
dotnet run --project RealmUnbound.Server -- --import
```

For each catalog item:
1. Upsert into the typed entity table (matched on `TypeKey + Slug`)
2. Register in `content_registry`
3. Resolve any `@reference` strings into Guid FKs before insert
4. Increment `Version`, set `UpdatedAt`

For `names.json` files: upsert `name_pattern_sets`, then upsert `name_patterns` and `name_components`.

For config files: upsert `game_config` by `ConfigKey`.

Rows are never deleted by the import pipeline — set `IsActive = false` to disable.

---

## C# Entity Location

All entity classes: `RealmUnbound.Server/Data/Entities/`  
`ApplicationDbContext` exposes a `DbSet<T>` for every table.  
Repository interfaces: `RealmEngine.Shared/Abstractions/` (one per domain).  
Repository implementations: `RealmEngine.Data/Repositories/` (backed by EF Core).

---

## Migration Plan

| Step | Action |
|---|---|
| 1 | Drop `ContentItems` table (migration: `DropContentItems`) |
| 2 | Add all typed content tables + `content_registry` + `trait_definitions` (migration: `AddContentSchema`) |
| 3 | Add name pattern tables (migration: `AddNamePatterns`) |
| 4 | Add `game_config` table (migration: `AddGameConfig`) |
| 5 | Build import pipeline |
| 6 | Swap repositories from JSON/GameDataCache to EF Core, one domain at a time |
| 7 | Remove `GameDataCache` from server DI once all repositories are swapped |

- **Catalog items** — enemies, weapons, armor, abilities, skills, spells, classes, backgrounds, npcs, quests, recipes, items, loot tables, social/dialogue, world locations, organizations, properties
- **Name-generation patterns** — procedural name patterns and component pools (`names.json` equivalents)
- **System configuration** — experience curves, rarity tiers, budget formulas, generation rules, socket config, etc.

JSON files remain the **authoring format** only. Content is imported into the DB via the seed/import pipeline and is not loaded from the filesystem at runtime.

---

## The `content_items` Table

```sql
CREATE TABLE content_items (
    id            UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    domain        VARCHAR(64)   NOT NULL,
    type_key      VARCHAR(64)   NOT NULL,
    slug          VARCHAR(128)  NOT NULL,
    display_name  VARCHAR(256),
    rarity_weight INT           NOT NULL DEFAULT 50,
    is_active     BOOLEAN       NOT NULL DEFAULT true,
    version       INT           NOT NULL DEFAULT 1,
    data          JSONB         NOT NULL DEFAULT '{}',
    updated_at    TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE UNIQUE INDEX ON content_items (domain, type_key, slug);
CREATE INDEX ON content_items (domain);
CREATE INDEX ON content_items USING GIN (data);
```

### Columns

| Column | Type | Purpose |
|---|---|---|
| `id` | `uuid` | Surrogate PK |
| `domain` | `varchar(64)` | Content namespace (see Domain Namespaces below) |
| `type_key` | `varchar(64)` | Subcategory within domain |
| `slug` | `varchar(128)` | Unique item identifier within domain+type_key |
| `display_name` | `varchar(256)` | Human-readable name for RealmForge UI |
| `rarity_weight` | `int` | Selection weight — higher = more common. 1 for non-catalog rows |
| `is_active` | `bool` | False = excluded from generation and reference resolution |
| `version` | `int` | Incremented each time the import pipeline upserts this row |
| `data` | `jsonb` | Full item payload — schema depends on domain (see below) |
| `updated_at` | `timestamptz` | Updated by import pipeline on every upsert |

---

## Domain Namespaces

The `domain` column partitions content into three categories. The `domain/type_key:slug` address mirrors the `@reference` syntax.

### 1. Catalog Content

Stores individual game items. Each item in a `catalog.json` becomes one row.

| Field | Value |
|---|---|
| `domain` | Entity category path — e.g. `"enemies"`, `"items/weapons/swords"`, `"abilities/active/offensive"` |
| `type_key` | Subcategory key — e.g. `"wolves"`, `"iron-longsword-types"`, `"basic-attacks"` |
| `slug` | Item slug — e.g. `"grey-wolf"`, `"iron-longsword"`, `"basic-attack"` |
| `display_name` | Item display name |
| `rarity_weight` | Item rarity weight (higher = more common, 1–100 typical range) |

**`data` JSONB shape:**
```json
{
  "attributes": { "health": 120, "strength": 14 },
  "stats": { "attackSpeed": 1.2, "movementSpeed": 3.0 },
  "traits": { "aggressive": true, "packHunter": true },
  "properties": { "level": 5, "xpReward": 80 }
}
```

Section names and field names follow the v5.1 catalog standard. The sections present depend on the domain (enemies have all four; abilities may only have `attributes` + `traits`).

**Reference resolution:** `@enemies/wolves:grey-wolf` → `SELECT data FROM content_items WHERE domain='enemies' AND type_key='wolves' AND slug='grey-wolf'`

---

### 2. Name-Generation Patterns

Stores procedural name-generation data (patterns and component pools). One row per entity type that supports procedural naming.

| Field | Value |
|---|---|
| `domain` | `"name-patterns"` |
| `type_key` | Entity path being named — e.g. `"enemies/wolves"`, `"items/weapons"`, `"npcs/merchants"` |
| `slug` | Always `"default"` (one pattern set per entity type) |
| `display_name` | Optional — e.g. `"Wolf Names"` |
| `rarity_weight` | `1` (not used for pattern rows) |

**`data` JSONB shape:**
```json
{
  "version": "4.0",
  "type": "pattern_generation",
  "supportsTraits": false,
  "patterns": [
    { "pattern": "{prefix} {base}", "rarityWeight": 50 },
    { "pattern": "{base} the {suffix}", "rarityWeight": 20 }
  ],
  "components": {
    "prefix": ["Dark", "Shadow", "Frost"],
    "base": ["Wolf", "Fang", "Howler"],
    "suffix": ["Stalker", "Hunter", "Pack"]
  }
}
```

**Pattern tokens:** `{prefix}`, `{base}`, `{suffix}`, `{quality}`, `{title}` — any component key defined in `components`.

**External references in components:** Use `@reference` syntax instead of inline strings where the component values live in another domain (e.g. material names referencing `@properties/materials`).

**Query:** `SELECT data FROM content_items WHERE domain='name-patterns' AND type_key='enemies/wolves' AND slug='default'`

---

### 3. System Configuration

Stores game configuration data. One row per configuration file.

| Field | Value |
|---|---|
| `domain` | `"config"` |
| `type_key` | Config key — e.g. `"experience"`, `"rarity"`, `"budget"`, `"generation-rules"`, `"socket-config"`, `"growth-stats"`, `"harvesting-config"`, `"material-filters"` |
| `slug` | Always `"default"` (one config per type_key) |
| `display_name` | Optional — e.g. `"Experience & Leveling"` |
| `rarity_weight` | `1` (not used for config rows) |

**`data` JSONB shape:** The full JSON object from the source config file, preserved as-is.

Example (`type_key = "experience"`):
```json
{
  "levelCurve": { "formula": "exponential", "baseXP": 100, "exponent": 1.5, "maxLevel": 100 },
  "xpSources": { "combat": { "baseXP": 50 } }
}
```

Example (`type_key = "rarity"`):
```json
{
  "tiers": [
    { "name": "Common", "rarityWeightRange": { "min": 50, "max": 100 }, "color": "#FFFFFF" }
  ]
}
```

**Query:** `SELECT data FROM content_items WHERE domain='config' AND type_key='experience' AND slug='default'`

---

## Import Pipeline

JSON files under `RealmEngine.Data/Data/Json/` are the **authoring format**. They are imported into the DB via:

```bash
dotnet run --project RealmUnbound.Server -- --import
```

The import pipeline:
1. Reads each `catalog.json` → upserts one row per item (matched on `domain + type_key + slug`)
2. Reads each `names.json` → upserts one row (domain=`"name-patterns"`, type_key=entity path, slug=`"default"`)
3. Reads each config file → upserts one row (domain=`"config"`, type_key=config key, slug=`"default"`)
4. Increments `version` and sets `updated_at = NOW()` on every upserted row

Rows are never deleted by the import pipeline — set `is_active = false` to disable an item.

---

## What Stays in JSON

The following files **do not move to the DB** and remain as static JSON read at startup:

| File | Reason |
|---|---|
| `.cbconfig.json` | RealmForge UI metadata — not game runtime data |
| `backups/` | Archive directory, not loaded at runtime |

---

## C# Entity

`RealmUnbound.Server/Data/Entities/ContentItem.cs`

`DbSet<ContentItem>` is exposed on `ApplicationDbContext` as `ContentItems`.

The repository layer (`IContentItemRepository`) is the abstraction point for all content reads. Domain-specific repositories (`IEnemyRepository`, `IAbilityRepository`, etc.) are implemented on top of `IContentItemRepository`.

---

## GIN Index on `data`

The JSONB column has a GIN index:

```sql
CREATE INDEX ON content_items USING GIN (data);
```

This enables efficient `@>` containment queries across the payload, useful for filtering by trait, level range, or any other nested property without denormalising the schema.

Example: all enemies with `aggressive` trait:
```sql
SELECT * FROM content_items
WHERE domain = 'enemies'
  AND data @> '{"traits": {"aggressive": true}}';
```
