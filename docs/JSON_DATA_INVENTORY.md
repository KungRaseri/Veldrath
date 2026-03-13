# RealmEngine JSON Game Data — Complete Inventory & Analysis

> Generated from `RealmEngine.Data/Data/Json/`  
> Covers 18 top-level domains, ~100+ files, ~1,600+ named game data entries

---

## Table of Contents

1. [Complete Folder/File Tree](#1-complete-folderfile-tree)
2. [Per-File Summaries by Domain](#2-per-file-summaries-by-domain)
3. [Entry Count Reference Table](#3-entry-count-reference-table)
4. [JSON File Type Schemas](#4-json-file-type-schemas)
5. [Cross-Domain Reference Map](#5-cross-domain-reference-map)
6. [Issues and Inconsistencies](#6-issues-and-inconsistencies)
7. [Canonical Entry Templates](#7-canonical-entry-templates)

---

## 1. Complete Folder/File Tree

```
RealmEngine.Data/Data/Json/
├── abilities/
│   ├── .cbconfig.json                     [config]  icon:Sparkles, sortOrder:40
│   ├── active/
│   │   ├── .cbconfig.json
│   │   └── catalog.json                   ⭐ 177 entries — 5 subtypes
│   ├── passive/
│   │   ├── .cbconfig.json
│   │   └── catalog.json                   ⭐ 148 entries — 3 subtypes
│   ├── reactive/
│   │   ├── .cbconfig.json
│   │   └── catalog.json                   ⭐ 36 entries — triggered-condition
│   └── ultimate/
│       ├── .cbconfig.json
│       └── catalog.json                   ⭐ 39 entries — tier-5 signature
│
├── backgrounds/
│   ├── .cbconfig.json
│   └── catalog.json                       ⭐ 12 entries — 6 attribute groups
│
├── backups/                               [archive — old/superseded data]
│
├── classes/
│   ├── .cbconfig.json
│   ├── catalog.json                       ⭐ hierarchical — warriors/mages/rogues/clerics/hybrids
│   └── names.json                         [names] v4.3 — 7 patterns, prefix/suffix/title
│
├── configuration/
│   ├── .cbconfig.json                     [config]  icon:Settings, sortOrder:15
│   ├── experience.json                    [config] v4.0 — XP curve, sources, level caps
│   ├── generation-rules.json              [config] v5.1 — component limits by rarity
│   ├── growth-stats.json                  [config] v4.0 — derived stat formulas
│   ├── harvesting-config.json             [config] v5.1 — yield formulas, node health
│   ├── material-filters.json              [config] v5.1 — item↔material compatibility
│   ├── rarity.json                        [config] v5.1 — 5 tiers with weight ranges
│   ├── resource-nodes.json                [config] — harvesting node definitions
│   └── socket-config.json                 [config] v5.1 — socket count/type probabilities
│
├── enemies/
│   ├── .cbconfig.json
│   ├── enemy-types.json                   [config] — budget multipliers for 20+ enemy types
│   ├── beasts/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ ~15 entries (wolves×3, bears×3, cats, raptors)
│   │   └── names.json                     [names]
│   ├── demons/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 17 entries — 6 subtypes incl. Dark Lord final boss
│   │   └── names.json                     [names]
│   ├── dragons/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 13 entries — chromatic, metallic, special
│   │   ├── colors.json
│   │   └── names.json                     [names]
│   ├── elementals/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 12 entries — fire, ice, earth, air
│   │   └── names.json                     [names]
│   ├── goblinoids/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 12 entries — goblins, hobgoblins, bugbears
│   │   └── names.json                     [names]
│   ├── humanoids/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ ~15 entries — bandits, knights, bosses
│   │   └── names.json                     [names]
│   ├── insects/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 14 entries — spiders, beetles, flying, hive
│   │   └── names.json                     [names]
│   ├── orcs/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 11 entries — common, elite, special
│   │   └── names.json                     [names]
│   ├── plants/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 13 entries — aggressive, defensive, mobile, ancient
│   │   └── names.json                     [names]
│   ├── reptilians/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 13 entries — kobolds, lizardfolk, yuan-ti, special
│   │   └── names.json                     [names]
│   ├── trolls/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 10 entries — common, elemental, special
│   │   └── names.json                     [names]
│   ├── undead/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 14 entries — zombies, skeletons, spirits, liches
│   │   └── names.json                     [names]
│   ├── vampires/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 11 entries — lesser, true, ancient
│   │   └── names.json                     [names]
│   └── wolves/                            ⚠️ DUPLICATE — see Issues §6.2
│       └── catalog.json                   ⭐ 5 entries — standalone v5.1 reference format
│
├── general/
│   ├── .cbconfig.json
│   ├── adjectives.json                    [vocabulary] — positive/negative/size/appearance/condition lists
│   ├── budget-config.json                 [config] ⚠️ — budget formulas, allocation splits ← see Issues §6.1
│   ├── colors.json                        [vocabulary]
│   ├── material-pools.json                [config] — metal/fabric/leather/gem/wood pools by tier
│   ├── smells.json                        [vocabulary]
│   ├── sounds.json                        [vocabulary]
│   ├── textures.json                      [vocabulary]
│   ├── time_of_day.json                   [vocabulary]
│   ├── verbs.json                         [vocabulary]
│   └── weather.json                       [vocabulary] — precipitation, wind, temperature components
│
├── items/
│   ├── .cbconfig.json
│   ├── armor/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 45 entries — light/medium/heavy-armor
│   │   └── names.json                     [names] v4.3 — 4 patterns, prefix/suffix
│   ├── consumables/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ 50 entries — potions, elixirs, tonics, food, misc
│   │   └── names.json                     [names] v4.3 — 3 patterns, effect/suffix
│   ├── crystals/
│   │   ├── .cbconfig.json
│   │   ├── life/
│   │   │   ├── .cbconfig.json
│   │   │   └── catalog.json               ⭐ ~6 entries — life/vitality/rejuvenation crystals
│   │   └── mana/
│   │       ├── .cbconfig.json
│   │       └── catalog.json               ⭐ [not fully read]
│   ├── enchantments/
│   │   ├── .cbconfig.json
│   │   ├── catalog.json                   ⭐ ~8 entries — enchantment scrolls
│   │   ├── names.json                     [names] v4.3 — 4 patterns, element_prefix/combat_suffix
│   │   └── names.json.broken              ⚠️ ARCHIVED v4.2 — should be deleted, see Issues §6.3
│   ├── essences/
│   │   ├── .cbconfig.json
│   │   ├── fire/
│   │   │   ├── .cbconfig.json
│   │   │   └── catalog.json               ⭐ 3 entries — flame/inferno/ember essence
│   │   └── shadow/
│   │       ├── .cbconfig.json
│   │       └── catalog.json               ⭐ [not fully read]
│   ├── gems/
│   │   ├── .cbconfig.json
│   │   ├── blue/
│   │   │   ├── .cbconfig.json
│   │   │   └── catalog.json               ⭐ ~6 entries — sapphire, aquamarine, lapis lazuli
│   │   ├── red/
│   │   │   ├── .cbconfig.json
│   │   │   └── catalog.json               ⭐ [not fully read]
│   │   └── special/
│   │       └── catalog.json               ⭐ [not fully read] ⚠️ missing .cbconfig.json
│   ├── materials/
│   │   ├── .cbconfig.json
│   │   ├── names.json                     ⚠️ MISNOMER — quality modifiers (Damaged→Masterwork), see Issues §6.4
│   │   ├── bone/catalog.json              ⭐ [not fully read]
│   │   ├── ceramic/catalog.json           ⭐ [not fully read]
│   │   ├── chitin/catalog.json            ⭐ [not fully read]
│   │   ├── coral/catalog.json             ⭐ [not fully read]
│   │   ├── crystal/catalog.json           ⭐ [not fully read]
│   │   ├── essence/catalog.json           ⭐ [not fully read]
│   │   ├── fabric/
│   │   │   └── catalog.json               ⭐ 15 entries — burlap→arcane-weave cloth
│   │   ├── gem/
│   │   │   └── catalog.json               ⭐ 14 entries — quartz→dragon tear gems
│   │   ├── glass/catalog.json             ⭐ [not fully read]
│   │   ├── ice/catalog.json               ⭐ [not fully read]
│   │   ├── ingot/
│   │   │   └── catalog.json               ⭐ 16 entries — copper→void-crystal ingots
│   │   ├── leather/
│   │   │   └── catalog.json               ⭐ 21 entries — scraps→dragon leather (v5.2)
│   │   ├── ore/
│   │   │   └── catalog.json               ⭐ 5 entries — iron/copper/tin/mithril ores
│   │   ├── organic/catalog.json           ⭐ [not fully read]
│   │   ├── paper/catalog.json             ⭐ [not fully read]
│   │   ├── reagent/catalog.json           ⭐ [not fully read]
│   │   ├── rubber/catalog.json            ⭐ [not fully read]
│   │   ├── scale/catalog.json             ⭐ [not fully read]
│   │   ├── scrap/catalog.json             ⭐ [not fully read]
│   │   ├── stone/catalog.json             ⭐ [not fully read]
│   │   └── wood/
│   │       └── catalog.json               ⭐ 15 entries — pine→legendary wood (v5.2)
│   ├── orbs/
│   │   ├── .cbconfig.json
│   │   ├── combat/
│   │   │   ├── .cbconfig.json
│   │   │   └── catalog.json               ⭐ ~8 entries — strike/berserk/precision orbs  [type: orb_catalog]
│   │   └── magic/
│   │       ├── .cbconfig.json
│   │       └── catalog.json               ⭐ [not fully read]
│   ├── runes/
│   │   ├── .cbconfig.json
│   │   ├── defensive/
│   │   │   ├── .cbconfig.json
│   │   │   ├── catalog.json               ⭐ [not fully read]  [type: rune_catalog]
│   │   │   └── names.json                 [names]
│   │   └── offensive/
│   │       ├── .cbconfig.json
│   │       ├── catalog.json               ⭐ ~8 entries — fury/piercing/devastation runes
│   │       └── names.json                 [names]
│   └── weapons/
│       ├── .cbconfig.json
│       ├── catalog.json                   ⭐ 74 entries — 7 weapon types
│       └── names.json                     [names] v4.3 — 4 patterns, prefix/suffix
│
├── loot-tables/
│   ├── chests/
│   │   └── catalog.json                   ⭐ 5 chest tiers — common→legendary
│   ├── enemies/
│   │   └── catalog.json                   ⭐ ~10 enemy loot groups with inheritance
│   └── harvesting/
│       └── catalog.json                   ⭐ ~8 node types (woods, ores, plants, fish)
│
├── npcs/
│   ├── .cbconfig.json
│   ├── names.json                         [names] v4.3 — 16 patterns, full name components
│   ├── relationships.json                 [config] v4.0 — faction memberships, ally/enemy tables
│   ├── schedules.json                     [config] — daily schedule templates per role
│   ├── traits.json                        [config] — 40 personality traits + 35 quirks
│   ├── reorganize-npcs.ps1                [script — not game data]
│   ├── common/catalog.json                ⭐ ~5 entries — farmers, laborers, troubled-past types
│   ├── craftsmen/catalog.json             ⭐ ~6 entries — Craftsman, SmithApprentice, Blacksmith, etc.
│   ├── criminal/catalog.json              ⭐ ~4 entries — FormerCriminal, active criminals
│   ├── magical/catalog.json               ⭐ ~5 entries — WizardApprentice, Mage, etc.
│   ├── merchants/catalog.json             ⭐ ~6 entries — GeneralMerchant, Jeweler, specialists
│   ├── military/catalog.json              ⭐ ~5 entries — FormerSoldier, GuardDuty, active
│   ├── noble/catalog.json                 ⭐ ~5 entries — NobleBorn, KnightErrant, aristocracy
│   ├── professionals/catalog.json         ⭐ [not fully read]
│   ├── religious/catalog.json             ⭐ ~5 entries — AcolyteOfFaith, clergy types
│   ├── service/catalog.json               ⭐ [not fully read]
│   └── dialogue/                          [subdirs: farewells/, greetings/, responses/, styles/]
│
├── organizations/
│   ├── .cbconfig.json
│   ├── businesses/catalog.json            ⭐ ~5 entries — inns, taverns (inn_basic/quality/tavern)
│   ├── factions/catalog.json              ⭐ ~8 entries v5.0 — guilds, political, military, religious
│   ├── guilds/catalog.json                ⭐ ~3 entries — fighters_guild (5 ranks), mages_guild
│   └── shops/catalog.json                 ⭐ ~6 entries — blacksmith, weapon-master, merchant shops
│
├── properties/
│   ├── .cbconfig.json
│   ├── materials/
│   │   ├── .cbconfig.json
│   │   ├── bones/catalog.json             ⭐ [not fully read]
│   │   ├── ceramics/catalog.json          ⭐ [not fully read]
│   │   ├── chitins/catalog.json           ⭐ [not fully read]
│   │   ├── corals/catalog.json            ⭐ [not fully read]
│   │   ├── crystals/catalog.json          ⭐ [not fully read]
│   │   ├── fabrics/catalog.json           ⭐ 13 entries — burlap→arcane-weave properties
│   │   ├── gemstones/catalog.json         ⭐ [not fully read]
│   │   ├── glass/catalog.json             ⭐ [not fully read]
│   │   ├── ice/catalog.json               ⭐ [not fully read]
│   │   ├── leathers/catalog.json          ⭐ 11 entries — rawhide→exotic leather properties
│   │   ├── metals/catalog.json            ⭐ 18 entries — copper→godforged metal properties
│   │   ├── papers/catalog.json            ⭐ [not fully read]
│   │   ├── rubbers/catalog.json           ⭐ [not fully read]
│   │   ├── scales/catalog.json            ⭐ [not fully read]
│   │   ├── stones/catalog.json            ⭐ [not fully read]
│   │   └── woods/catalog.json             ⭐ 11 entries — pine→worldtree wood properties
│   └── qualities/catalog.json             ⭐ 4 entries — Fine/Superior/Exceptional/Masterwork
│
├── quests/
│   ├── .cbconfig.json
│   ├── catalog.json                       ⭐ 27 template entries — 7+ quest types
│   ├── objectives.json                    ⭐ 51 objectives (20 primary, 17 secondary, 14 hidden)
│   ├── rewards.json                       ⭐ 38 reward entries (20 items, 9 gold tiers, 9 XP tiers)
│   ├── objectives/catalog.json            ⭐ [separate structured catalog]
│   ├── rewards/catalog.json               ⭐ [separate structured catalog]
│   └── templates/catalog.json             ⭐ [separate structured catalog]
│
├── recipes/
│   ├── .cbconfig.json
│   └── catalog.json                       ⭐ — smithing/alchemy/crafting recipes
│
├── skills/
│   ├── .cbconfig.json
│   └── catalog.json                       ⭐ 55 entries — attribute-grouped professions
│
├── social/
│   ├── .cbconfig.json
│   └── dialogue/
│       ├── .cbconfig.json
│       ├── farewells/   [merchant/, noble/, simple/]
│       ├── greetings/   [merchant/, military/, noble/]
│       ├── responses/
│       └── styles/      [casual/, formal/]
│
├── spells/
│   ├── .cbconfig.json
│   └── catalog.json                       ⭐ 144 entries — arcane/divine/natural/shadow traditions
│
└── world/
    ├── .cbconfig.json
    ├── environments/catalog.json          ⭐ ~8 biomes — temperate-forest, grassland, desert, etc.
    ├── locations/
    │   ├── .cbconfig.json
    │   ├── dungeons/catalog.json          ⭐ ~9 entries — easy/medium/hard tiers
    │   ├── towns/catalog.json             ⭐ ~12 entries — outpost→capital
    │   └── wilderness/catalog.json        ⭐ ~9 entries — low/medium/high danger zones
    └── regions/catalog.json               ⭐ ~6 kingdoms — political relations, factions
```

---

## 2. Per-File Summaries by Domain

### 2.1 Abilities

| File | Type | Entries | Key Fields | Notable References |
|------|------|---------|-----------|-------------------|
| `active/catalog.json` | `abilities_catalog` v4.2 | 177 | slug, name, cooldown, manaCost, effects[], damageType | — |
| `passive/catalog.json` | `abilities_catalog` v4.2 | 148 | slug, name, triggerCondition, buffType, magnitude | — |
| `reactive/catalog.json` | `abilities_catalog` v4.2 | 36 | slug, name, triggerCondition, reaction | — |
| `ultimate/catalog.json` | `abilities_catalog` v4.2 | 39 | slug, name, cooldown (long), manaCost (high), tier:5 | — |

**Subtypes in `active`**: offensive (majority), defensive, support, utility, healing

### 2.2 Backgrounds

| File | Type | Entries | Key Fields |
|------|------|---------|-----------|
| `catalog.json` | `hierarchical_catalog` v4.0 | 12 | slug, name, primaryAttribute, startingGold, skillBonuses[], traits{} |

Two backgrounds per attribute group: strength, dexterity, constitution, intelligence, wisdom, charisma.

### 2.3 Classes

| File | Type | Entries | Key Fields | Notable References |
|------|------|---------|-----------|-------------------|
| `catalog.json` | `hierarchical_catalog` v4.0 | ~15+ | slug, name, primaryAttribute, hitDice, armor[], weaponProficiencies[], abilities[] | `@abilities/*` |
| `names.json` | `pattern_generation` v4.3 | 7 patterns | components: prefix, suffix, title | — |

### 2.4 Configuration

| File | Type | Version | Purpose |
|------|------|---------|---------|
| `experience.json` | `experience_config` | 4.0 | XP formula: `baseXP(100) * level^1.5`, max level 100, activity multipliers |
| `generation-rules.json` | `generation_rules` | 5.1 | Per-rarity component count limits, name format rules |
| `growth-stats.json` | `growth_stats_config` | 4.0 | Derived stat formulas (HP, mana, damage, crit, armor) |
| `harvesting-config.json` | `harvesting_config` | 5.1 | Yield `baseYield × (1 + skill×0.003 + toolBonus)`, crit 5% base |
| `material-filters.json` | `material_filter_config` | 5.1 | item category → allowed material pools |
| `rarity.json` | `rarity_config` | 5.1 | 5 tiers: Common(50-100), Uncommon(30-49), Rare(15-29), Epic(5-14), Legendary(1-4) |
| `resource-nodes.json` | `resource_node_config` | — | Harvesting node spawn definitions |
| `socket-config.json` | `socket_config` | 5.1 | Socket counts by rarity: Common(0-1), Uncommon(0-2), Rare(0-3), Epic(1-4), Legendary(2-6) |

### 2.5 Enemies

All enemy catalogs use `hierarchical_catalog` v5.1 with `*_types.*` grouping structure.  
Entries share: `slug`, `name`, `level`, `rarityWeight`, `xp`, `attributes{}`, `stats{}` (formula-based), `combat.abilities[]`, `traits{}`.

| Subdomain | Entries | Notable Entries |
|-----------|---------|----------------|
| `beasts/` | ~15 | timber wolf, cave bear, mountain lion, dire raptor |
| `demons/` | 17 | imp, shadow demon, balor, **Dark Lord** (final boss) |
| `dragons/` | 13 | red/black/blue/green/white chromatic; gold/silver metallic; elder variants |
| `elementals/` | 12 | fire elemental, greater ice elemental, elder earth/air elementals |
| `goblinoids/` | 12 | goblin, hobgoblin warchief, bugbear, goblin king |
| `humanoids/` | ~15 | bandit, brigand leader, town guard, paladin knight, crime boss |
| `insects/` | 14 | spider, giant beetle, wasp swarm, queen bee |
| `orcs/` | 11 | orc warrior, orc berserker, orc warlord |
| `plants/` | 13 | thorn vine, treant sapling, myconid, ancient treant |
| `reptilians/` | 13 | kobold, lizardfolk shaman, yuan-ti abomination |
| `trolls/` | 10 | cave troll, fire troll, troll king |
| `undead/` | 14 | zombie, skeleton warrior, banshee, lich |
| `vampires/` | 11 | fledgling vampire, vampire lord, ancient vampire |
| `wolves/` | 5 | wolf, timber-wolf, frost-wolf, direwolf, alpha wolf |

**enemy-types.json** — Defines budget multipliers and material pool references for 20+ enemy archetype names used by the item generation system.

### 2.6 General

| File | Purpose |
|------|---------|
| `adjectives.json` | Vocabulary pool: positive/negative/size/appearance/condition word lists |
| `budget-config.json` | ⚠️ Budget formula config — see Issues §6.1 |
| `colors.json` | Color vocabulary |
| `material-pools.json` | Named material pool definitions for loot resolution: metals/fabrics/leathers/gems/woods by rarity tier |
| `smells.json`, `sounds.json`, `textures.json` | World flavor vocabulary |
| `time_of_day.json` | Time period definitions |
| `verbs.json` | Action vocabulary |
| `weather.json` | Weather component arrays (precipitation, wind, temperature) |

### 2.7 Items

#### Weapons (`items/weapons/`)

| File | Type | Entries | Key Fields |
|------|------|---------|-----------|
| `catalog.json` | `item_catalog` v5.1 | 74 | slug, name, weaponType, damage, weight, rarityWeight, traits{}, sockets |
| `names.json` | `pattern_generation` v4.3 | 4 patterns | components: prefix (Rusty/Old/Sharp...), suffix |

**Weapon types**: heavy-blades (Longsword, Greatsword, Katana), light-blades (Blade, Shortsword, Rapier), polearms, blunt (Mace, Warhammer), bows (Short/Long/Crossbow), staves, daggers

#### Armor (`items/armor/`)

| File | Type | Entries | Key Fields |
|------|------|---------|-----------|
| `catalog.json` | `item_catalog` v5.1 | 45 | slug, name, armorClass, defense, weight, rarityWeight |
| `names.json` | `pattern_generation` v4.3 | 4 patterns | components: prefix (Sturdy/Reinforced/Hardened...), suffix |

**Armor types**: light-armor (Leather Armor, Studded), medium-armor (Chain Mail, Scale Mail), heavy-armor (Plate Armor, Full Plate)

#### Consumables (`items/consumables/`)

| File | Type | Entries | Key Fields |
|------|------|---------|-----------|
| `catalog.json` | `item_catalog` v5.1 | 50 | slug, effectType, magnitude, duration, stackSize |
| `names.json` | `pattern_generation` v4.3 | 3 patterns | components: effect, suffix |

**Consumable types**: potions (health/mana), elixirs (stat buffs), tonics (resistance), food (passive), misc

#### Materials (`items/materials/`)

Item-side material catalogs — these are **lootable inventory items** that reference property definitions via `propertyRef: "@properties/..."`.

| Subdir | Entries | Rarity Range |
|--------|---------|-------------|
| `ingot/` | 16 | copper (rw:90) → void-crystal (rw:1) |
| `ore/` | 5 | iron-ore, copper-ore, tin-ore, mithril-ore + 1 |
| `wood/` | 15 | pine-wood (rw:100) → legendary wood (rw:2) |
| `leather/` | 21 | scraps (rw:100) → dragon-leather (rw:2) v5.2 |
| `fabric/` | 15 | burlap-cloth (rw:100) → arcane-weave (rw:3) |
| `gem/` | 14 | rough-gemstone (rw:85) → dragon-tear (rw:2) v5.2 |
| bone, ceramic, chitin, coral, crystal, essence, glass, ice, organic, paper, reagent, rubber, scale, scrap, stone | varies | [not fully read] |

**`names.json`** — ⚠️ Despite its location, this file contains **quality modifier tiers** (Damaged, Worn, Cracked, Standard, Fine, Superior, Masterwork) with `budgetModifier` values — it is not a name-generation file.

#### Enchantments (`items/enchantments/`)

| File | Type | Entries |
|------|------|---------|
| `catalog.json` | `item_catalog` | ~8 | Agility Scroll, Intellect Scroll, etc. |
| `names.json` | `pattern_generation` v4.3 | 4 patterns | components: element_prefix, element_suffix, combat_suffix, magic_suffix |
| `names.json.broken` | ⚠️ ARCHIVED v4.2 | — | Old position-based prefix/suffix model — safe to delete |

#### Gems (`items/gems/`)

| Subdir | Entries | Examples |
|--------|---------|---------|
| `blue/` | ~6 | sapphire, aquamarine, lapis lazuli |
| `red/` | ~6 | [not fully read] |
| `special/` | ~4 | [not fully read] — missing `.cbconfig.json` |

#### Crystals (`items/crystals/`)
- `life/`: life-crystal, vitality-crystal, rejuvenation-crystal (~6 items)
- `mana/`: [not fully read]

#### Essences (`items/essences/`)
- `fire/`: flame-essence, inferno-essence, ember-essence (3 items, rw: 30–8)
- `shadow/`: [not fully read]

#### Orbs (`items/orbs/`) — type: `orb_catalog`
- `combat/`: strike-orb (rw:55), berserk-orb (rw:40), precision-orb (rw:45) + more. Traits: meleeSkillDamage, cooldown reduction
- `magic/`: [not fully read]

#### Runes (`items/runes/`) — type: `rune_catalog`
- `offensive/`: fury-rune (rw:50), piercing-rune (rw:45), devastation-rune (rw:30) + more. Traits: attackPower, armorPenetration, critChance
- `defensive/`: [not fully read] — also has `names.json`, `offensive/` has `names.json` too

### 2.8 Loot Tables

All use `hierarchical_catalog` structure with pools referencing `@material_pools/*` or `@items/*`.

| File | Entries |Key Fields |
|------|---------|----------|
| `chests/catalog.json` | 5 tiers | rarityWeight, minItems, maxItems, pools[], guaranteed[] |
| `enemies/catalog.json` | ~10 groups | enemyType, lootPools[], additionalPools[] (inheritance), dropChance |
| `harvesting/catalog.json` | ~8 node types | nodeType, tier, guaranteedDrops[], bonusDrops[], toolRequirement |

### 2.9 NPCs

| File | Type | Entries | Key Fields |
|------|------|---------|-----------|
| `names.json` | `pattern_generation` v4.3 | 16 patterns | title, first_name, surname, suffix components; `weightMultiplier` soft filter by social class |
| `relationships.json` | `relationship_catalog` v4.0 | ~6 factions | members[], allies[], enemies[], neutralTowards[], benefits{} |
| `schedules.json` | template config | ~7 templates | timeSlots[], location, activity per template (merchant/craftsman/innkeeper/guard/noble…) |
| `traits.json` | trait config | 75 entries | 40 personality traits + 35 quirks across social_positive, social_negative, professional, etc. |

**NPC Catalog Subcategories:**

| Subdir | Entries | Distinguishing Attributes |
|--------|---------|--------------------------|
| `common/` | ~5 | strength 10, standard stats; farmer/laborer/troubled-past |
| `craftsmen/` | ~6 | dex 13, crafting/perception/appraisal skills; `shopAppearance: "workshop"` |
| `criminal/` | ~4 | dex 15, stealth/deception skills |
| `magical/` | ~5 | int 16, arcane_lore/spellcraft skills; `shopAppearance: "mystical"` |
| `merchants/` | ~6 | cha 13, persuasion/appraisal; `buyPriceMultiplier: 0.35` |
| `military/` | ~5 | str 14/con 13, athletics/intimidation |
| `noble/` | ~5 | cha 14/int 13; `shopAppearance: "noble_estate"` |
| `professionals/` | ~4 | [not fully read] |
| `religious/` | ~5 | wis 16, divine skills |
| `service/` | ~4 | [not fully read] |

### 2.10 Organizations

| File | Type | Entries | Key Fields |
|------|------|---------|-----------|
| `factions/catalog.json` | `hierarchical_catalog` v5.0 | ~8 | name, type (political/military/religious/guild), members[], allies[], enemies[], headquarters ref |
| `guilds/catalog.json` | `hierarchical_catalog` | ~3 | ranks[], rankRequirements{}, hqRef `@world/locations/towns/cities:*` ⚠️ |
| `shops/catalog.json` | `hierarchical_catalog` | ~6 | shopType, inventoryCount range, restockPeriod, owner `@npcs/*:*` |
| `businesses/catalog.json` | `hierarchical_catalog` | ~5 | businessType (inn/tavern), roomRate, services[], owner ref |

### 2.11 Properties

**Properties** are stat definitions applied to items when a material is used in crafting. They are referenced by `items/materials/*/catalog.json` via `propertyRef`.

#### Metals (`properties/materials/metals/`)
18 entries: copper, tin, iron, steel, bronze, silver, cold-iron, mithril, adamantine, darksteel, dragonbone, dragon-steel, obsidian, celestial-ore, godforged, void-crystal + 2 more.  
Key fields: `itemTypeTraits.weapon.damage`, `itemTypeTraits.armor.armorRating`, `durabilityMultiplier`, `weightMultiplier`

#### Leathers (`properties/materials/leathers/`)
11 entries: rawhide, scraps, hide, tanned-hide, reinforced-hide, exotic-leather, dragon-hide + 4 more.  
Key fields: `itemTypeTraits.armor.armorRating`, `itemTypeTraits.armor.magicResist`, `flexibility`

#### Woods (`properties/materials/woods/`)
11 entries: pine, birch, oak, ash, mahogany, ebony, ironwood, shadowwood, dragonwood, worldtree + 1 more.  
Key fields: `itemTypeTraits.weapon.damage`, `itemTypeTraits.weapon.speed`, `itemTypeTraits.weapon.critChance`

#### Fabrics (`properties/materials/fabrics/`)
13 entries: burlap, linen, cotton, wool, silk, velvet, canvas, mageweave, shadowweave, spidersilk, mooncloth, dragonweave, arcane-weave.  
Key fields: `itemTypeTraits.armor.armorRating`, `itemTypeTraits.armor.magicResist`, `itemTypeTraits.armor.spellPower`

#### Qualities (`properties/qualities/`)
4 entries: Fine (armorStat +8%, damage +5%), Superior (+12%/+8%), Exceptional (+18%/+12%), Masterwork (+25%/+18%).

### 2.12 Quests

| File | Type | Entries | Notes |
|------|------|---------|-------|
| `catalog.json` | `hierarchical_catalog` v4.0 | 27 templates | fetch(3), kill(3), escort(3), delivery(3), investigate(5), + more types |
| `objectives.json` | `hierarchical_catalog` v4.0 | 51 objectives | 20 primary, 17 secondary, 14 hidden; all support `rarityWeight` selection |
| `rewards.json` | `hierarchical_catalog` v4.0 | 38 total | 20 item rewards, 9 gold tiers, 9 XP tiers; gold formula: `base × (1 + level × 0.05)` |

**Reward multipliers**: secondary objectives +25–50% XP/gold; hidden objectives +50–100% + item tier upgrade; perfect completion +100% XP + item tier upgrade.

### 2.13 Recipes

`catalog.json` — `recipes_catalog` v5.1 — smithing, alchemy, leatherworking, woodworking recipes.  
Key fields: `producedItem` (ref or inline), `components[]` (material refs via `@items/materials/*`), `requiredSkill`, `skillLevel`, `craftingTime`.

### 2.14 Skills

`catalog.json` — `skills_catalog` v4.2 — 55 entries across attribute groups:
- Strength: athletics, endurance, intimidation, heavy-weapons...
- Dexterity: acrobatics, stealth, lockpicking, ranged-weapons...
- Constitution: survival, fortitude...
- Intelligence: arcane-lore, alchemy, history, investigation...
- Wisdom: perception, insight, divine-lore, nature...
- Charisma: persuasion, deception, performance, leadership...
- Weapon/armor/magic proficiency tracks

### 2.15 Social / Dialogue

`social/dialogue/` — Dialogue content organized by context and social class:
- `greetings/`: merchant/, military/, noble/ (contextual opening lines)
- `farewells/`: merchant/, noble/, simple/
- `responses/`: generic response pools
- `styles/`: casual/, formal/ (tone modifiers)

These feed `@social/dialogue/styles/casual:professional` style references in NPC catalogs.

### 2.16 Spells

`catalog.json` — `spells_catalog` v4.2 — 144 entries across traditions:
- arcane (evocation, conjuration, abjuration…)
- divine (healing, smite, holy…)
- natural (druidic, elemental, growth…)
- shadow (necromancy, illusion, debuff…)

Key fields: `slug`, `name`, `tradition`, `school`, `manaCost`, `castTime`, `range`, `effects[]`, `rarityWeight`

### 2.17 World

| File | Type | Entries | Key Fields |
|------|------|---------|-----------|
| `regions/catalog.json` | `world_catalog` | ~6 | name, capital, factions[], politicalRelations{}, climate |
| `environments/catalog.json` | `world_catalog` | ~8 | biome, commonEncounters[] `@enemies/*`, resources[] `@items/*` |
| `locations/towns/catalog.json` | `world_catalog` | ~12 | tier (outpost/village/town/city/capital), population, services[] |
| `locations/dungeons/catalog.json` | `world_catalog` | ~9 | difficulty, enemyTypes[], bossRef, lootTier |
| `locations/wilderness/catalog.json` | `world_catalog` | ~9 | dangerLevel, encounters[], resources[], weather[] |

**Kingdoms**: Goldenvale (GoldenSpire capital), Frostheim (FrostPeak capital), Shadow_Empire, Desert_Emirates, Elven_Reaches, Sanctuary_Theocracy.

---

## 3. Entry Count Reference Table

| Domain | File | Entries |
|--------|------|---------|
| Abilities — Active | `abilities/active/catalog.json` | **177** |
| Abilities — Passive | `abilities/passive/catalog.json` | **148** |
| Abilities — Reactive | `abilities/reactive/catalog.json` | **36** |
| Abilities — Ultimate | `abilities/ultimate/catalog.json` | **39** |
| Backgrounds | `backgrounds/catalog.json` | **12** |
| Enemies — Beasts | `enemies/beasts/catalog.json` | **~15** |
| Enemies — Demons | `enemies/demons/catalog.json` | **17** |
| Enemies — Dragons | `enemies/dragons/catalog.json` | **13** |
| Enemies — Elementals | `enemies/elementals/catalog.json` | **12** |
| Enemies — Goblinoids | `enemies/goblinoids/catalog.json` | **12** |
| Enemies — Humanoids | `enemies/humanoids/catalog.json` | **~15** |
| Enemies — Insects | `enemies/insects/catalog.json` | **14** |
| Enemies — Orcs | `enemies/orcs/catalog.json` | **11** |
| Enemies — Plants | `enemies/plants/catalog.json` | **13** |
| Enemies — Reptilians | `enemies/reptilians/catalog.json` | **13** |
| Enemies — Trolls | `enemies/trolls/catalog.json` | **10** |
| Enemies — Undead | `enemies/undead/catalog.json` | **14** |
| Enemies — Vampires | `enemies/vampires/catalog.json` | **11** |
| Enemies — Wolves (standalone) | `enemies/wolves/catalog.json` | **5** |
| **Enemies TOTAL** | | **~187** |
| Items — Weapons | `items/weapons/catalog.json` | **74** |
| Items — Armor | `items/armor/catalog.json` | **45** |
| Items — Consumables | `items/consumables/catalog.json` | **50** |
| Items — Materials (ingots) | `items/materials/ingot/catalog.json` | **16** |
| Items — Materials (ore) | `items/materials/ore/catalog.json` | **5** |
| Items — Materials (wood) | `items/materials/wood/catalog.json` | **15** |
| Items — Materials (leather) | `items/materials/leather/catalog.json` | **21** |
| Items — Materials (fabric) | `items/materials/fabric/catalog.json` | **15** |
| Items — Materials (gem) | `items/materials/gem/catalog.json` | **14** |
| Items — Enchantments | `items/enchantments/catalog.json` | **~8** |
| Properties — Metals | `properties/materials/metals/catalog.json` | **18** |
| Properties — Leathers | `properties/materials/leathers/catalog.json` | **11** |
| Properties — Woods | `properties/materials/woods/catalog.json` | **11** |
| Properties — Fabrics | `properties/materials/fabrics/catalog.json` | **13** |
| Properties — Qualities | `properties/qualities/catalog.json` | **4** |
| Quests — Templates | `quests/catalog.json` | **27** |
| Quests — Objectives | `quests/objectives.json` | **51** |
| Quests — Rewards | `quests/rewards.json` | **38** |
| Skills | `skills/catalog.json` | **55** |
| Spells | `spells/catalog.json` | **144** |
| World — Regions | `world/regions/catalog.json` | **~6** |
| World — Environments | `world/environments/catalog.json` | **~8** |
| World — Towns | `world/locations/towns/catalog.json` | **~12** |
| World — Dungeons | `world/locations/dungeons/catalog.json` | **~9** |
| World — Wilderness | `world/locations/wilderness/catalog.json` | **~9** |

**Estimated total named entries (all catalogs):** ~1,600+

---

## 4. JSON File Type Schemas

### 4.1 `catalog.json` — Hierarchical Catalog

```json
{
  "metadata": {
    "type": "<domain>_catalog",
    "version": "5.1",
    "description": "...",
    "lastUpdated": "2026-01-08",
    "notes": ["..."]
  },
  "<domain>_types": {
    "<subtype_key>": {
      "metadata": { "description": "...", "notes": ["..."] },
      "items": [
        {
          "slug": "kebab-case-id",
          "name": "PascalCaseName",
          "displayName": "Human Readable Name",
          "description": "Brief description",
          "rarityWeight": 50,
          "traits": {
            "traitKey": { "value": 42, "type": "number" }
          }
        }
      ]
    }
  }
}
```

**Enemy-specific additions**: `level`, `xp`, `attributes{}` (str/dex/con/int/wis/cha), `stats{}` (formula strings), `combat.abilities[]`, `combat.abilityUnlocks{}`  
**Item-specific additions**: `weight`, `stackSize`, `sockets`, `baseValue`/`value`  
**NPC-specific additions**: `socialClass`, `startingGold`, `skillBonuses[]`, `shopModifiers{}`, `specializationHints[]`, `dialogueStyle`

### 4.2 `names.json` — Pattern Generation

```json
{
  "metadata": {
    "type": "pattern_generation",
    "version": "4.3",
    "supportsTraits": true,
    "lastUpdated": "2026-01-08",
    "description": "...",
    "componentKeys": ["prefix", "suffix"],
    "patternTokens": ["{prefix}", "{base}", "{suffix}"]
  },
  "components": {
    "prefix": [
      {
        "name": "Sharp",
        "rarityWeight": 30,
        "traits": {
          "damageBonus": { "value": 4, "type": "number" }
        }
      }
    ],
    "suffix": [ ... ]
  },
  "patterns": [
    { "template": "{prefix} {base}", "rarityWeight": 40 },
    { "template": "{base} of {suffix}", "rarityWeight": 30 }
  ]
}
```

**Soft filtering in NPC names.json**: components use `weightMultiplier: { "socialClass": 2.0 }` to bias selection by social context without hard exclusion.

### 4.3 `.cbconfig.json` — Directory Metadata

```json
{
  "description": "Human-readable description of this directory's content",
  "displayName": "Optional override display name",
  "icon": "MaterialDesignIconName",
  "sortOrder": 10
}
```

### 4.4 `budget-config.json`

```json
{
  "metadata": { "type": "budget_config", ... },
  "allocation": {
    "materialBudget": 0.30,
    "componentBudget": 0.70
  },
  "costFormulas": {
    "material": {
      "type": "inverse_scaled",
      "numerator": 60,          ⚠️ likely should be 6000 — see Issues §6.1
      "denominator": "rarityWeight",
      "costScaleField": "costScale"
    },
    "component": {
      "type": "inverse",
      "numerator": 100,
      "denominator": "rarityWeight"
    },
    "enchantment": {
      "type": "inverse",
      "numerator": 130,
      "denominator": "rarityWeight"
    }
  },
  "budgetRanges": {
    "minimal": [20, 40],
    "common": [40, 80],
    "uncommon": [80, 150],
    "rare": [150, 250],
    "epic": [250, 400],
    "legendary": [400, 600]
  }
}
```

### 4.5 `material-pools.json`

```json
{
  "metadata": { "type": "material_pools", ... },
  "pools": {
    "metals_common": {
      "description": "Common metal ingots",
      "items": [
        { "ref": "@items/materials/ingot:copper-ingot", "weight": 80 },
        { "ref": "@items/materials/ingot:tin-ingot",    "weight": 70 }
      ]
    },
    "metals_rare": { ... }
  }
}
```

---

## 5. Cross-Domain Reference Map

### 5.1 Reference Syntax (JSON Reference System v4.1)

```
@domain/subpath/category:slug
@domain/subpath/category:*        ← wildcard (random selection by rarityWeight)
@domain/subpath/category:slug?    ← optional (null if not found)
@domain/path:slug.property        ← property access via dot notation
```

### 5.2 Reference Usage by Domain

| Referencing File | References | Target Domain |
|-----------------|-----------|---------------|
| `enemies/*/catalog.json` | `combat.abilities[]` | `@abilities/active/offensive:*` (wildcard majority), specific slugs in wolves/ |
| `enemies/*/catalog.json` | `combat.loot[]` | `@material_pools/*` |
| `items/materials/*/catalog.json` | `propertyRef` | `@properties/materials/<type>:<slug>` |
| `recipes/catalog.json` | `components[].ref` | `@items/materials/*:<slug>` |
| `recipes/catalog.json` | `producedItem` | `@items/weapons/*`, `@items/armor/*` |
| `loot-tables/*/catalog.json` | `pools[]` | `@material_pools/*` |
| `loot-tables/enemies/catalog.json` | `enemyType` | `@enemies/*:*` |
| `world/environments/catalog.json` | `commonEncounters[]` | `@enemies/*:<slug>` |
| `world/environments/catalog.json` | `resources[]` | `@items/materials/*:<slug>` |
| `world/regions/catalog.json` | `capital` | `@world/locations/towns/capitals:*` ⚠️ (`capitals/` doesn't exist) |
| `world/regions/catalog.json` | `factions[]` | `@organizations/factions:<slug>` |
| `organizations/guilds/catalog.json` | `headquarters` | `@world/locations/towns/cities:*` ⚠️ (`cities/` doesn't exist) |
| `organizations/shops/catalog.json` | `owner` | `@npcs/*:*` |
| `organizations/businesses/catalog.json` | `owner` | `@npcs/common:*` |
| `npcs/*/catalog.json` | `dialogueStyle` | `@social/dialogue/styles/*:<slug>` |
| `classes/catalog.json` | `abilities[]` | `@abilities/active/*:<slug>` |
| `quests/catalog.json` | `location` (migrated) | `@world/locations/*:<slug>` |
| `general/enemy-types.json` | `materialPool` | `@material_pools/*` |

### 5.3 Material Reference Chain

```
loot-tables → @material_pools (general/material-pools.json)
                         ↓
               @items/materials/ingot:copper-ingot  (items/materials/ingot/catalog.json)
                         ↓
               propertyRef → @properties/materials/metals:copper  (properties/materials/metals/catalog.json)
```

This three-tier chain: **loot-table → item → property** ensures that a single property definition drives all stat bonuses regardless of which loot table originally dropped the item.

---

## 6. Issues and Inconsistencies

### 6.1 ✅ RESOLVED — `budget-config.json` numerator is `60` (correct for current scale)

**File**: `general/budget-config.json`  
**Field**: `costFormulas.material.numerator = 60`

**Analysis**: With `enemyLevelMultiplier = 5`, a level 1 enemy has equipment budget = 5. With numerator=60, common items (rw=100) cost ~1 gold, which is affordable. The `budgetRanges` in the config (minimal: 20-40) are for shop inventory or loot quality tiers — a separate concern from enemy NPC equipment. Changing to 6000 breaks enemy equipment generation because level 1 budgets (5) can no longer afford common items (cost ~60).

**Status**: No change needed. Numerator=60 is correct for the current game scale.

---

### 6.2 ⚠️ STRUCTURAL — `enemies/wolves/` is an apparent duplicate of `enemies/beasts/`

**Files involved**:
- `enemies/beasts/catalog.json` contains `beast_types.wolves` (timber-wolf, cave bear uses same folder)
- `enemies/wolves/catalog.json` exists as a **separate standalone file** with 5 wolf entries using full v5.1 reference format, with specific ability slugs (`@abilities/active/offensive:bite`)

**Also**: `enemies/wolves/` has **no `.cbconfig.json`**.

**Recommendation**: Decide if wolves should be a standalone enemy type (remove from beasts, add `.cbconfig.json` to wolves/) or consolidate back into `enemies/beasts/`. The v5.1 wolves catalog is higher quality with specific ability references, suggesting it may be the intended future direction.

---

### 6.3 ⚠️ CRUFT — `items/enchantments/names.json.broken` should be removed

**File**: `items/enchantments/names.json.broken`  
This is an archived v4.2 format with explicit `"position": "prefix"` metadata instead of the current pattern-token system. The live `names.json` is v4.3. No production code should load this, but the `.broken` extension provides weak protection against accidental inclusion in glob loads.

**Recommendation**: Delete the file, or move it to `backups/`. A `*.broken` file in an active data directory is a maintenance hazard.

---

### 6.4 ⚠️ NAMING CONFUSION — `items/materials/names.json` is not a names file

**File**: `items/materials/names.json`  
Despite its location implying pattern generation, this file contains **quality modifier tiers** (Damaged/Worn/Cracked/Standard/Fine/Superior/Masterwork) with `budgetModifier` values — it configures the item quality cost scaling system.

**Overlap**: `properties/qualities/catalog.json` also defines quality tiers (Fine/Superior/Exceptional/Masterwork) but from a stat-bonus perspective, not a budget perspective. These are two different quality systems oriented differently:
- `items/materials/names.json` → 7 tiers, budget-oriented (how quality affects cost)
- `properties/qualities/catalog.json` → 4 tiers, stat-oriented (how quality affects item stats)

**Recommendation**: Rename `items/materials/names.json` → `items/materials/quality-modifiers.json` to eliminate the naming confusion. Evaluate whether the two quality systems should be merged.

---

### 6.5 ⚠️ MISSING `.cbconfig.json` entries

The following directories that contain actual game data are missing `.cbconfig.json` files:
- `enemies/wolves/` — no icon/sortOrder config
- `items/gems/special/` — no icon/sortOrder config
- Several `items/materials/` subdirectories (most subdirs are missing it)
- `quests/locations/` — has a `.cbconfig.json` but appears to be empty/placeholder

**Recommendation**: Add `.cbconfig.json` to all directories that are direct children of domain root directories, particularly `enemies/wolves/` and `items/gems/special/`.

---

### 6.6 ⚠️ BROKEN CROSS-REFERENCES — `world/locations/` missing subcategories

**Files affected**: `world/regions/catalog.json`, `organizations/guilds/catalog.json`

References used in the data:
- `@world/locations/towns/capitals:GoldenSpire` — there is no `locations/towns/capitals/` subdirectory; only `towns/`, `dungeons/`, `wilderness/`
- `@world/locations/towns/cities:Stormhaven` — no `cities/` subdirectory
- `@world/locations/towns/cities:TheNexus` — same

The `towns/catalog.json` uses settlement tiers (outpost/village/town/city/capital) as data fields, but there are no physical subdirectories to match these tier references.

**Recommendation**: Either add `cities/` and `capitals/` subdirectories with their own catalogs, or update all references to use `@world/locations/towns:<slug>` (flat reference into the single towns catalog).

---

### 6.7 INFO — Wildcard enemy abilities are ubiquitous (design note)

Most enemy catalogs use `@abilities/active/offensive:*` (wildcard) for `combat.abilities[]`, meaning enemies receive random abilities at runtime. Only `enemies/wolves/catalog.json` uses specific slug references (e.g., `@abilities/active/offensive:bite`). This is intentional variance by design but means enemy ability sets are non-deterministic across the board.

---

## 7. Canonical Entry Templates

### 7.1 Enemy Catalog Entry (full)

```json
{
  "slug": "goblin-warrior",
  "name": "GoblinWarrior",
  "displayName": "Goblin Warrior",
  "description": "A battle-hardened goblin armed with crude weapons.",
  "level": 4,
  "rarityWeight": 80,
  "xp": 75,
  "attributes": {
    "strength": 10,
    "dexterity": 14,
    "constitution": 11,
    "intelligence": 8,
    "wisdom": 8,
    "charisma": 7
  },
  "stats": {
    "health": "constitution_mod * 2 + level * 5 + 10",
    "attack": "strength_mod + level",
    "defense": "10 + dexterity_mod + constitution_mod",
    "speed": "30 + dexterity_mod * 5"
  },
  "combat": {
    "abilities": ["@abilities/active/offensive:*"],
    "abilityUnlocks": {
      "5": ["@abilities/active/support:pack-tactics"]
    }
  },
  "traits": {
    "darkvision": { "value": 60, "type": "number" },
    "nimble": { "value": true, "type": "boolean" }
  }
}
```

### 7.2 Weapon Catalog Entry (full)

```json
{
  "slug": "iron-longsword",
  "name": "IronLongsword",
  "displayName": "Iron Longsword",
  "description": "A sturdy longsword forged from iron.",
  "rarityWeight": 60,
  "weaponType": "heavy-blades",
  "damage": "1d8",
  "damageType": "slashing",
  "weight": 3.5,
  "value": 15,
  "sockets": 1,
  "stackSize": 1,
  "itemType": "weapon",
  "subType": "heavy-blades",
  "traits": {
    "critChance": { "value": 0.05, "type": "number" },
    "critMultiplier": { "value": 1.5, "type": "number" }
  },
  "tags": ["sword", "martial", "melee"]
}
```

### 7.3 Material Item Entry (with propertyRef)

```json
{
  "slug": "iron-ingot",
  "name": "Iron Ingot",
  "description": "A solid bar of smelted iron.",
  "propertyRef": "@properties/materials/metals:iron",
  "rarityWeight": 80,
  "value": 10,
  "weight": 1.0,
  "stackSize": 50,
  "itemType": "material",
  "subType": "ingot",
  "tier": "common",
  "traits": {},
  "tags": ["metal", "crafting", "smithing"]
}
```

### 7.4 Material Property Entry

```json
{
  "slug": "iron",
  "name": "Iron",
  "displayName": "Iron",
  "rarityWeight": 80,
  "durabilityMultiplier": 1.0,
  "weightMultiplier": 1.0,
  "itemTypeTraits": {
    "weapon": {
      "damage": { "value": 5, "type": "number" },
      "critChance": { "value": 0.03, "type": "number" }
    },
    "armor": {
      "armorRating": { "value": 8, "type": "number" },
      "magicResist": { "value": 2, "type": "number" }
    }
  }
}
```

### 7.5 Names Pattern Generation Entry (weapon prefix)

```json
{
  "name": "Sharp",
  "rarityWeight": 30,
  "traits": {
    "damageBonus": { "value": 4, "type": "number" },
    "criticalMultiplier": { "value": 1.5, "type": "number" },
    "durability": { "value": 90, "type": "number" }
  }
}
```

With pattern template: `{ "template": "{prefix} {base}", "rarityWeight": 40 }` → produces "Sharp Iron Longsword".

### 7.6 NPC Catalog Entry (full)

```json
{
  "slug": "blacksmith",
  "name": "Blacksmith",
  "displayName": "Blacksmith",
  "description": "A skilled metalworker who crafts and repairs items.",
  "attributes": {
    "strength": 14,
    "dexterity": 13,
    "constitution": 12,
    "intelligence": 12,
    "wisdom": 11,
    "charisma": 10
  },
  "rarityWeight": 8,
  "socialClass": "craftsman",
  "startingGold": "3d10",
  "skillBonuses": ["crafting", "appraising", "smithing"],
  "shopModifiers": {
    "qualityBonus": 8,
    "priceMultiplier": 1.05,
    "shopAppearance": "workshop"
  },
  "specializationHints": ["weapons.melee", "armor.heavy"],
  "uniqueItems": ["Blacksmith's Hammer"],
  "dialogueStyle": "@social/dialogue/styles/casual:professional"
}
```

### 7.7 Quest Template Entry

```json
{
  "slug": "kill-monster-threat",
  "name": "MonsterThreat",
  "displayName": "Monster Threat",
  "questType": "kill",
  "difficulty": "normal",
  "rarityWeight": 25,
  "description": "Eliminate {count} {enemyType} threatening {location}.",
  "objectives": ["@quests/objectives:DefeatEnemies"],
  "possibleRewards": {
    "gold": "@quests/rewards:medium_gold",
    "xp": "@quests/rewards:medium_xp",
    "items": "@quests/rewards:uncommon_equipment"
  },
  "location": "@world/locations/wilderness:*",
  "traits": {
    "minPlayerLevel": { "value": 3, "type": "number" },
    "timeLimit": { "value": null, "type": "number" }
  }
}
```

### 7.8 Loot Table Entry

```json
{
  "slug": "chest-uncommon",
  "name": "UncommonChest",
  "displayName": "Uncommon Chest",
  "rarityWeight": 30,
  "minItems": 2,
  "maxItems": 4,
  "pools": [
    { "ref": "@material_pools/metals_uncommon", "weight": 40 },
    { "ref": "@material_pools/gems_uncommon",   "weight": 20 },
    { "ref": "@material_pools/leathers_uncommon","weight": 20 },
    { "ref": "@material_pools/woods_uncommon",   "weight": 20 }
  ],
  "guaranteed": [],
  "traits": {
    "goldMin": { "value": 10, "type": "number" },
    "goldMax": { "value": 50, "type": "number" }
  }
}
```

---

*End of inventory. ~100 files catalogued across 18 domains.*
