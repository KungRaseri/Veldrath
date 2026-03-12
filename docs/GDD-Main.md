# Game Design Document - RealmEngine

**Project**: RealmEngine  
**Version**: 1.7  
**Last Updated**: January 6, 2026  
**Engine**: .NET 9.0 Console Application  
**Architecture**: Vertical Slice + CQRS Pattern  

---

## Table of Contents

1. [Executive Summary](#executive-summary)
2. [Game Overview](#game-overview)
3. [Core Systems](#core-systems)
4. [Game Features](#game-features)
5. [Technical Architecture](#technical-architecture)
6. [Content & Progression](#content--progression)
7. [User Interface](#user-interface)

---

## Executive Summary

**RealmEngine** is a feature-rich, turn-based role-playing game built as a .NET Core console application. It combines classic RPG mechanics (character creation, combat, inventory, quests) with modern software architecture patterns (CQRS, Vertical Slice, MediatR event-driven design).

### Key Highlights

- **D20 System**: Six-attribute system (STR, DEX, CON, INT, WIS, CHA) with derived stats
- **6 Character Classes**: Warrior, Rogue, Mage, Cleric, Ranger, Paladin
- **Turn-Based Combat**: Tactical combat with dodge, critical hits, blocking, and item usage
- **Procedural Content**: Random enemies, loot, and NPCs using Bogus library
- **Rich UI**: Interactive menus, character sheets, and combat displays
- **Persistent State**: LiteDB for save/load functionality with auto-save
- **Quest System**: Main quest chain with 6 major quests and achievement tracking
- **Difficulty Modes**: 5 difficulty levels including permadeath and Apocalypse mode
- **New Game+**: Replay with bonus stats and retained achievements

### Development

This game features a modern software architecture with vertical slice organization, CQRS pattern, and event-driven design using MediatR. A companion WPF tool (**RealmForge**) provides visual editing of game data files.

**For current implementation status, test counts, and feature completion percentages, see IMPLEMENTATION_STATUS.md.**

---

## Game Overview

### Genre & Theme

**Genre**: Turn-based RPG, Dungeon Crawler  
**Theme**: High Fantasy Adventure  
**Tone**: Epic quest with lighthearted moments  
**Target Audience**: RPG enthusiasts, retro gaming fans, developers learning software architecture

### Game Concept

Players create a character, explore locations, battle enemies, complete quests, collect loot, and build their character through leveling and equipment. The game features a main quest chain leading to a climactic final battle, with optional achievements and New Game+ for replayability.

### Victory Conditions

1. **Complete Main Quest Chain**: Finish all 6 main quests culminating in "The Final Confrontation"
2. **Defeat the Final Boss**: Win the climactic battle
3. **Victory Celebration**: Dramatic victory sequence with statistics and rewards
4. **Optional**: Unlock all achievements, reach max level (50), or complete Apocalypse mode

---

## Core Systems

### 1. Character System

#### Attribute System

Characters have six core attributes affecting all aspects of gameplay:

- **Strength**: Affects melee damage output and physical carrying capacity
- **Dexterity**: Affects dodge chance, initiative, and ranged attack precision
- **Constitution**: Affects maximum health and damage resistance
- **Intelligence**: Affects magic damage and mana pool size
- **Wisdom**: Affects mana regeneration and magical defense
- **Charisma**: Affects NPC interactions and shop pricing

**Derived Stats:**
Attributes feed into derived statistics:
- **Health Points (HP)**: Determined by Constitution
- **Mana Points (MP)**: Determined by Intelligence
- **Damage Output**: Determined by Strength (melee/thrown) or Dexterity (ranged)
- **Defensive Rating**: Determined by Dexterity and equipped armor
- **Dodge Chance**: Determined by Dexterity
- **Critical Hit Chance**: Base chance modified by specific attributes and skills

#### Class System

Multiple unique character classes provide distinct playstyles and role-playing opportunities. Each class offers different starting advantages, recommended builds, and tactical approaches to combat and exploration.

**Class Features:**
- Starting attribute bonuses matching class archetype
- Class-appropriate starting equipment
- Signature abilities suited to class role
- Recommended progression paths for new players

#### Character Creation

The character creation process guides players through:

1. **Name Selection**: Choose character identity with input validation
2. **Class Selection**: Review class descriptions and select preferred archetype
3. **Attribute Distribution**: Attributes allocated based on class choice
4. **Equipment Setup**: Receive class-appropriate starting gear
5. **Resource Initialization**: Begin with starting gold and consumables

### 2. Combat System

#### Turn-Based Combat

Combat uses a turn-based system where players and enemies alternate actions. Each turn, players choose from multiple tactical options:

**Player Actions:**
- **Attack**: Strike enemies with equipped weapons, dealing damage based on attributes and gear
- **Defend**: Enter a defensive stance, reducing incoming damage and potentially blocking attacks
- **Use Item**: Consume potions or scrolls for healing, buffs, or tactical advantages
- **Flee**: Attempt to escape combat, with success based on character speed and enemy threat

#### Combat Mechanics

**Damage Calculation:**
Damage output combines weapon power, character attributes, enemy defenses, and situational modifiers. The system rewards matching weapon types to character builds (melee weapons scale with physical attributes, magic weapons scale with mental attributes).

**Defensive Mechanics:**
- **Dodge**: Chance to completely avoid attacks based on agility
- **Block**: Chance to negate damage when actively defending
- **Damage Reduction**: Armor and constitution reduce incoming damage
- **Resistance**: Elemental resistances protect against specific damage types

**Critical Hits:**
Attacks have a chance to critically strike, dealing significantly increased damage. Critical hit chance increases with attributes and specific gear. Visual feedback alerts players to successful critical strikes.

**Combat Feedback:**
The combat log displays turn-by-turn actions, damage dealt, status effects, and tactical outcomes with color-coded text for clarity.

#### Enemy System

**Procedural Generation:**
Enemies are generated dynamically with level scaling, ensuring appropriate challenge for player progression. Enemy power scales with player level while allowing for easier and harder encounters.

**Enemy Traits:**
Enemies possess behavioral and statistical traits affecting combat style:
- Behavioral traits (aggressive, defensive, cowardly, berserker)
- Defensive traits (tank, evasive, regenerating)
- Offensive traits (glass cannon, life steal, thorns)
- Elemental affinities (fire, ice, lightning, poison)

**Boss Encounters:**
Boss enemies feature enhanced statistics and unique mechanics, providing memorable challenges with increased rewards.

### 3. Inventory System

#### Item Management

Players manage a limited inventory with multiple organizational features:

- **Item Storage**: Finite inventory capacity requiring strategic item management
- **Equipment Slots**: Dedicated slots for weapons, armor, shields, and accessories
- **Consumables**: Potions and scrolls usable both in and out of combat
- **Sorting Options**: Organize inventory by name, type, or rarity for easy access

#### Item Categories

The game features diverse item types for different playstyles:

**Weapons:**
Melee and ranged weapons with varying damage types, attack speeds, and attribute requirements. Weapon types suit different character builds and combat strategies.

**Armor:**
Protective gear ranging from light cloth to heavy plate, with varying defense values and mobility impacts. Armor types balance protection against movement freedom.

**Shields:**
Defensive equipment increasing block chance and damage reduction. Shield sizes offer different balance points between protection and combat flexibility.

**Consumables:**
Single-use items providing healing, mana restoration, temporary buffs, or combat advantages. Consumables offer tactical flexibility during challenging encounters.

**Accessories:**
Rings, amulets, and trinkets providing passive bonuses to attributes, resistances, or special abilities. Accessories allow build customization beyond primary equipment.

#### Rarity System

Items exist across multiple rarity tiers, with rarer items offering superior stats and unique properties:

- **Common Items**: Basic equipment readily available
- **Uncommon Items**: Improved stats and occasional bonuses
- **Rare Items**: Significantly enhanced with multiple bonuses
- **Epic Items**: Powerful equipment with substantial advantages
- **Legendary Items**: Exceptionally rare with game-changing properties

Rarity affects drop rates, gold value, and overall item power. Visual color coding helps identify item quality at a glance.

#### Trait System

Items possess traits providing gameplay bonuses and strategic variety:

**Offensive Traits:**
Increase damage output through flat bonuses, percentage multipliers, elemental damage, or special attack effects (life steal, armor penetration, status application).

**Defensive Traits:**
Enhance survivability through defense increases, damage resistance, elemental resistances, or regeneration effects.

**Utility Traits:**
Provide quality-of-life benefits like increased carrying capacity, improved loot drops, faster movement, or resource cost reduction.

**Enchantments:**
Prefix and suffix modifiers that can combine to create uniquely powerful items. The enchantment system allows for tremendous item variety through component combinations.

#### Procedural Generation System

Items are generated dynamically using a data-driven pattern system:

**Name Generation:**
Item names are constructed from composable patterns combining base names, material types, quality modifiers, and magical properties. This creates virtually unlimited item variety while maintaining naming consistency.

**Stat Generation:**
Item statistics scale based on rarity tier, item level, and applied traits. The system ensures items remain balanced while providing meaningful progression.

**Trait Assignment:**
Traits are assigned based on item type, rarity, and available trait pools. Trait combinations create build-defining equipment without manual item design.

**Data-Driven Architecture:**
All item generation rules, trait definitions, and naming patterns live in external data files following established standards (v4.0 JSON specification). This allows content expansion without code changes.

### 4. Progression System

#### Experience & Leveling

Character progression is driven by an experience point (XP) system with an exponential leveling curve:

**XP Sources:**
- Combat victories (scaled to enemy difficulty)
- Quest completion (main and side quests)
- Exploration milestones (discovering locations, finding secrets)
- Achievement unlocks

**Level-Up Rewards:**
Gaining levels increases core stats (HP, MP), provides attribute points for character customization, and unlocks new abilities or skills. The level cap ensures a defined progression arc while providing substantial power growth.

#### Attribute Allocation

Each level-up grants attribute points for flexible character building:

**Build Variety:**
- **Specialization**: Focus points into primary attributes for class mastery
- **Hybrid Builds**: Distribute across multiple attributes for versatile characters
- **Balanced Approach**: Spread points evenly for generalist characters

**Customization Impact:**
Attribute choices directly impact combat effectiveness, survivability, magic power, and utility. Players can respec their builds or commit to focused archetypes based on preferred playstyle.

#### Skills System (Practice-Based Progression)

**Design Philosophy:**
Skills represent learned proficiencies that improve through use—"learning by doing." The skill system provides passive bonuses that automatically apply during relevant actions, creating organic character development distinct from level-based progression.

**Total Skills: 54** organized into 5 major categories with practice-based XP progression.

**Core Concept: "Use It to Improve It"**
- Swing swords → improve weapon skill (Light Blades, Heavy Blades, Axes, etc.)
- Cast spells → improve magic tradition (Arcane, Divine, Occult, Primal) and specializations
- Sneak around → improve Stealth skill (DEX-based)
- Craft items → improve profession skills (Blacksmithing, Alchemy, etc.)

**Skill Categories:**

**1. Attribute Skills (24 total - 4 per attribute):**
Each attribute governs 4 related skills that use attribute bonuses in checks:

- **STR (Strength)**: Athletics, Swimming, Climbing, Carrying
- **DEX (Dexterity)**: Acrobatics, Stealth, Sleight of Hand, Lockpicking
- **CON (Constitution)**: Endurance, Concentration, Disease Resistance, Poison Resistance
- **INT (Intelligence)**: Arcana, Investigation, Nature, History
- **WIS (Wisdom)**: Perception, Insight, Medicine, Survival
- **CHA (Charisma)**: Persuasion, Intimidation, Deception, Performance

*Skill checks combine: d20 + Skill Rank + Attribute Modifier*

**2. Weapon Skills (10 total):**
- Light Blades (daggers, short swords, rapiers) - DEX synergy
- Heavy Blades (longswords, greatswords) - STR synergy
- Axes (hand axes, battleaxes, great axes) - STR synergy, armor penetration
- Blunt (maces, hammers, mauls) - STR synergy, stagger enemies
- Polearms (spears, halberds, glaives) - STR/DEX hybrid, reach advantage
- Bows (shortbow, longbow, composite) - DEX synergy
- Crossbows (light, heavy, repeating) - armor penetration
- Throwing (knives, javelins, axes) - DEX synergy
- Unarmed (fists, kicks, grappling) - STR/DEX hybrid
- Shield (blocking, bash attacks) - STR synergy

*Weapon skills provide accuracy, damage multipliers, and special techniques. Attributes provide base damage.*

**3. Armor Skills (4 total):**
- Light Armor (cloth, leather) - dodge bonuses
- Medium Armor (chainmail, scale) - balanced protection
- Heavy Armor (plate, full plate) - maximum protection, damage reduction
- Unarmored Defense (monk-style) - dodge chance, movement speed

**4. Magic Skills (16 total - 4 core traditions + 3 specializations each):**

**Arcane Tradition (study-based magic, INT):**
- Arcane (core) - spell access, general power
- Force Magic - pure magical energy spells
- Chronomancy - time manipulation (haste, slow, time stop)
- Conjuration - summoning constructs/outsiders

**Divine Tradition (faith-based magic, WIS):**
- Divine (core) - spell access, general power
- Restoration - healing, resurrection
- Smiting - holy damage vs evil/undead
- Warding - protection, blessings, sanctuary

**Occult Tradition (mental magic, CHA):**
- Occult (core) - spell access, general power
- Enchantment - mind control, domination
- Illusion - deception, invisibility
- Shadowcraft - darkness, fear, psychic damage

**Primal Tradition (nature magic, WIS):**
- Primal (core) - spell access, general power
- Elementalism - fire, ice, lightning, earth
- Beast Mastery - shapeshifting, animal companions
- Verdancy - plants, growth, natural healing

*Core tradition skills unlock spells. Specialization skills boost specific spell types.*

**5. Profession Skills (12 total):**
- **Crafting**: Blacksmithing, Leatherworking, Tailoring, Woodworking, Jewelcrafting
- **Magic Crafting**: Alchemy (potions/poisons), Enchanting, Runecrafting
- **Gathering**: Mining (ore/gems), Herbalism (plants)
- **Utility**: Cooking (food buffs), Fishing

**Skill Mechanics:**
- **Ranking System**: Skills rank from 0 (untrained) to 100 (master)
- **Use-Based XP**: Gain skill XP by performing related actions
- **Scaling Costs**: Base XP cost × cost multiplier per rank
- **Per-Rank Bonuses**: Each skill rank provides % bonuses (varies by skill)
- **Multiple XP Actions**: Different actions grant different XP amounts
- **Passive Application**: Bonuses automatically apply, no activation required

**Build Depth:**
Skills create organic specialization—characters naturally improve at what they do most. The dual system (attributes provide base power, skills provide proficiency) creates meaningful build choices: high STR + low Blade skill vs low STR + high Blade skill produce different results.

**Distinct from Abilities & Spells:**
- **Skills** = Proficiency level (passive bonuses, improves with use, 54 total)
- **Abilities** = Class/race powers (active, tiered 1-5, 383 total)
- **Spells** = Learnable magic (active, ranked 0-10, 144 total)

#### Abilities System (Class & Species Powers)

**Design Philosophy:**
Abilities are active powers from class, race, or items that define character identity. Unlike passive skills, abilities require activation and strategic timing. They provide tactical depth in combat and utility outside it.

**Total Abilities: 383** organized by activation type and power tier.

**Core Concept: "Special Powers"**
Abilities are signature moves granted by class/race, unlocked through progression, or gained from special items/quests. They're inherent to character identity, not learned like spells.

**Activation Types:**

**1. Active Abilities (177 total)**
Manually activated powers requiring conscious decision-making:
- **Offensive (88)**: Damage-dealing attacks (Backstab, Execute, Power Strike)
- **Defensive (34)**: Protective maneuvers (Shield Wall, Parry, Iron Skin)
- **Support (27)**: Healing and buffs (Battle Cry, Heal, Rally)
- **Utility (28)**: Non-combat utility (Teleportation, Detect Traps, Lockbreaking)
- **Control (8)**: Crowd control (Fear Aura, Charm, Stun)
- **Summon (4)**: Creature summoning (Animal Companion, Summon Skeleton)
- **Mobility (2)**: Movement abilities (Shadow Step, Leap)

**2. Passive Abilities (131 total)**
Always-active bonuses requiring no activation:
- **General (16)**: Core class passives (Weapon Mastery, Combat Supremacy)
- **Offensive (38)**: Damage bonuses (Venomous Bite, Critical Strike, Berserker)
- **Defensive (39)**: Protection bonuses (Thick Hide, Regeneration, Elemental Immunity)
- **Leadership (24)**: Auras affecting allies (Alpha Predator, Warchief, Inspire)
- **Environmental (22)**: Terrain adaptations (Night Hunter, Aquatic, Lair Actions)
- **Mobility (7)**: Movement enhancements (Flight, Burrowing, Shadow Walk)
- **Sensory (1)**: Perception bonuses (Keen Senses)

**3. Reactive Abilities (36 total)**
Triggered automatically by specific conditions:
- **Offensive (14)**: Counterattacks (Vengeance Strike, Retaliation, Death Throes)
- **Defensive (12)**: Defensive triggers (Thorns, Last Stand, Iron Will)
- **Utility (10)**: Situation responses (Desperate Escape, Phoenix Rebirth, Cleanse)

**4. Ultimate Abilities (39 total)**
Powerful special moves with long cooldowns - all Tier 5:
- Game-changing abilities for climactic moments
- Examples: Demon Lord, Reality Warp, Ragnarok, Fortress

**Tier System (Power Levels 1-5):**
- **Tier 1**: Basic abilities (available early, low selectionWeight < 50)
- **Tier 2**: Trained abilities (mid-game, selectionWeight 50-99)
- **Tier 3**: Expert abilities (advanced, selectionWeight 100-199)
- **Tier 4**: Master abilities (late-game, selectionWeight 200-399)
- **Tier 5**: Legendary abilities (endgame, selectionWeight 400+ or Ultimate category)

**Ability Mechanics:**
- **Mana Costs**: Prevent spamming (stored in traits, 10-150 mana typical)
- **Cooldown Timers**: Tactical pacing (0-300 seconds, in traits)
- **Range/AoE**: Targeting parameters (in traits)
- **Damage/Effects**: Stored in traits with type annotations
- **Trigger Conditions**: For reactive abilities (onHit, onDeath, onLowHealth, etc.)

**Class Integration:**
Each class has signature abilities defining their playstyle. Classes start with 2-3 core abilities and unlock more through leveling, questing, or specialization choices.

**Data Structure (v4.2):**
Abilities use consolidated catalogs with unified traits system:
- Flat fields: slug, name, displayName, description, tier, selectionWeight
- Traits contain: all gameplay properties (baseDamage, cooldown, range, etc.)
- 4 catalog files: active, passive, reactive, ultimate

**Distinct from Skills & Spells:**
- **Skills** = Passive proficiency (always active, improves with use, no activation)
- **Abilities** = Active/reactive powers (class-granted, tiered 1-5, cooldown/cost)
- **Spells** = Learnable magic (must be acquired, ranked 0-10, tradition-based)

#### Magic & Spell System (Learnable Magic)

**Design Philosophy:**
Spells are universal magical knowledge accessible to any character through magical traditions and specializations. Unlike class-specific abilities, spells require study, practice, and tradition-specific skills.

**Total Spells: 144** (36 per tradition) organized by **Pathfinder 2e magic traditions** with rank-based progression (Rank 0-10).

**Core Concept: "Knowledge & Tradition"**
Spells must be learned through spellbooks, scrolls, teachers, or quest rewards. Any character can learn spells if they develop the required tradition skill (Arcane, Divine, Occult, or Primal), though non-magical classes are less effective.

**Magic Traditions (4 Core):**

**1. Arcane Tradition (36 spells, INT-based)**
Study-based magic from scholarly learning and manipulation of fundamental forces:
- **Themes**: Force energy, elemental damage (fire/lightning), transmutation, teleportation
- **Sample Spells**: Ray of Frost, Magic Missile, Fireball, Lightning Bolt, Haste, Teleport, Time Stop
- **Ranks 0-10**: 8 cantrips (rank 0) → 1 legendary spell per rank (9-10)
- **Ultimate**: Time Stop (rank 10, 200 mana, stop time for 2 turns)

**2. Divine Tradition (36 spells, WIS-based)**
Faith-based magic channeling divine power through devotion:
- **Themes**: Healing, holy damage, protection, curing ailments, resurrection
- **Sample Spells**: Stabilize, Heal, Bless, Prayer of Healing, Resurrection, True Resurrection
- **Ranks 0-10**: 8 cantrips → Ultimate resurrection spells
- **Ultimate**: True Resurrection (rank 10, 250 mana, restore fully from death)

**3. Occult Tradition (36 spells, CHA-based)**
Mental and psychic magic through force of personality and forbidden knowledge:
- **Themes**: Mind control, illusions, psychic damage, fear, charm
- **Sample Spells**: Daze, Charm, Invisibility, Fear, Hypnotic Pattern, Dominate Person, Weird
- **Ranks 0-10**: 8 cantrips → Mass mind control
- **Ultimate**: Weird (rank 10, 250 mana, mass phantasmal killer 25d10 psychic)

**4. Primal Tradition (36 spells, WIS-based)**
Nature-based magic from connection to natural world and primal forces:
- **Themes**: Elements, animals, plants, weather, shapeshifting
- **Sample Spells**: Produce Flame, Entangle, Call Lightning, Summon Animal, Earthquake, Nature Incarnate
- **Ranks 0-10**: 8 cantrips → Transform into nature avatar
- **Ultimate**: Nature Incarnate (rank 10, 250 mana, become avatar of nature)

**Spell Rank Distribution (per tradition):**
- **Rank 0 (Cantrips)**: 8 spells (0 mana cost, unlimited casting)
- **Rank 1**: 6 spells
- **Rank 2**: 5 spells
- **Rank 3**: 4 spells
- **Rank 4**: 3 spells
- **Rank 5**: 3 spells
- **Rank 6**: 2 spells
- **Rank 7**: 2 spells
- **Rank 8**: 1 spell
- **Rank 9**: 1 spell
- **Rank 10**: 1 spell (Ultimate)
**Total**: 36 spells per tradition × 4 traditions = 144 total

**Spell Mechanics:**
- **Acquisition**: Find spellbooks, purchase from vendors, learn from trainers, earn through quests
- **Skill Requirements**: Core tradition skill determines which spells you can learn (e.g., Arcane 25 for Fireball)
- **Rank Gates**: Spell rank limits power progression (Rank 0-10)
- **Mana Costs**: 0 (cantrips) to 250 mana (ultimate spells)
- **Casting Success**: Based on tradition skill rank vs spell rank
- **Skill Scaling**: Higher tradition skill = more damage/healing, better success rate
- **Specialization Bonuses**: Specialist skills (Force Magic, Restoration, Illusion, etc.) provide targeted bonuses

**Magic Skill Synergy:**
The magic skill system provides depth through specialization:
- **Core Tradition Skill** (Arcane/Divine/Occult/Primal): Unlocks spells, general effectiveness
- **Specialization Skills** (Force Magic, Chronomancy, Restoration, Elementalism, etc.): Boost specific spell types
- **Governing Attributes** (INT/WIS/CHA): Provide base spell power
- **Combined Power**: Tradition skill + specialization + attribute = maximum effectiveness

**Example**: A character with Arcane 50, Force Magic 30, INT 18 deals maximum damage with force-based arcane spells.

**Data Structure (v4.2):**
Spells use single consolidated catalog organized by tradition:
- Flat fields: slug, name, displayName, description, rank, selectionWeight
- Traits contain: minimumSkillRank, manaCost, range, targetType, damageType, baseDamage, duration, etc.
- All 144 spells in `spells/catalog.json`

**Distinct from Skills & Abilities:**
- **Skills** = Passive proficiency (determines spell access and effectiveness)
- **Abilities** = Class powers (instant activation, tiered 1-5)
- **Spells** = Learnable magic (must acquire, ranked 0-10, tradition-based, mana cost)

**Spell Learning:**
- **Spellbooks**: Consumable items that teach spells permanently
- **Scrolls**: Single-use spell casting (no skill check required)
- **Trainers**: NPCs teach spells for gold (must meet skill requirement)
- **Quest Rewards**: Unique spells from story/side quests

**Build Variety:**
Spells enable hybrid builds—Warriors casting buffs, Rogues using invisibility, Clerics summoning allies. Spell collection creates exploration incentives and replayability (different spells each playthrough).

**Distinct from Skills & Abilities:**
- **Skills** = Passive proficiency (determines spell effectiveness)
- **Abilities** = Class powers (always available to class members)
- **Spells** = Learnable magic (must be acquired, universal access, skill-dependent)

### 5. Quest System

The quest system provides structured objectives for players to pursue, ranging from a core main quest chain to procedurally generated side quests. Completing quests grants rewards (XP, gold, items) and drives narrative progression.

#### Quest Categories

**Main Quest Chain:**
- Linear story-driven quests forming the core narrative
- Sequential prerequisites (must complete Quest A to unlock Quest B)
- Culminates in a final confrontation that grants victory
- Provides time bonuses in Apocalypse mode

**Side Quests:**
- Optional quests for additional rewards and world-building
- Procedurally generated from quest templates
- Can be accepted and completed in any order
- Provides variety and replay value

**Daily/Repeatable Quests:** *(Future)*
- Refresh daily for consistent engagement
- Lower rewards but can be repeated

#### Quest Types

The system supports diverse objective types to create varied gameplay:

**Investigation Quests:**
- Gather clues (3-8 clues depending on difficulty)
- Track targets across multiple locations
- Optional combat encounters
- May have multiple endings based on player choices

**Fetch Quests:**
- Retrieve specific items (herbs, artifacts, lost belongings)
- Quantity varies by difficulty (5-20 items for easy, fewer for rare items)
- Item types: herbs, relics, documents, supplies
- May require exploring specific location types

**Kill Quests:**
- Eliminate specific enemy types (goblins, undead, bandits, dragons)
- Target count scales with difficulty (3-15 enemies)
- Location hints guide players to enemy spawns
- Boss quests require defeating unique enemies

**Escort Quests:**
- Protect NPCs traveling between locations
- Difficulty based on distance and danger level
- NPCs may provide dialogue or lore during journey
- Failure if NPC dies

**Delivery Quests:**
- Transport items/messages between locations
- Time limits for urgent deliveries
- Stealth/speed challenges for high-difficulty versions

#### Quest Structure

**Prerequisites:**
- Minimum level requirements
- Completed previous quests (for chains)
- Reputation thresholds *(Future)*
- Owned items *(Future)*

**Objective Tracking:**
- Primary objectives (required for completion)
- Secondary objectives (optional bonus rewards)
- Hidden objectives (discovered during gameplay, unlock secrets)
- Real-time progress updates (kill counters, collection tracking)

**Quest States:**
- Available (meets prerequisites, not yet accepted)
- Active (accepted and in progress)
- Completed (all objectives finished)
- Failed (time limit expired or critical failure) *(Future)*

**Difficulty Tiers:**
- Easy (levels 1-10, low rewards)
- Medium (levels 10-20, uncommon/rare rewards)
- Hard (levels 20-30, rare/epic rewards)
- Very Hard (levels 30-40, epic/legendary rewards)
- Epic/Legendary (levels 40+, legendary/mythic rewards)

#### Reward System

**Gold Rewards:**
- 9 tiers (trivial to ancient: 10g - 10,000g base)
- Scales with player level: `final_amount = base_amount × (1 + player_level × 0.05)`
- Matched to quest difficulty

**Experience Rewards:**
- 9 tiers (trivial to ancient: 50 XP - 50,000 XP base)
- Scales with level and difficulty: `final_xp = base_xp × difficulty_multiplier × (1 + player_level × 0.1)`
- Maintains progression balance across level ranges

**Item Rewards:**
- Equipment (weapons, armor, accessories)
- Consumables (potions, scrolls, materials)
- Unique quest items (progression unlocks)
- Rarity matches quest difficulty (easy = common/uncommon, hard = epic/legendary)
- Item level scales near player level (±2 levels)

**Special Rewards:**
- Apocalypse time bonuses (5-60 minutes)
- Achievement unlocks
- Reputation gains *(Future)*
- Skill points *(Future)*

#### Quest Integration

**Quest Givers:**
- NPCs in towns/locations offer quests
- Quest giver types: common folk, merchants, military, nobles, professionals
- Quest type matches NPC background (merchants give fetch, military gives kill)

**Location Integration:**
- Quest objectives reference location types (towns, dungeons, wilderness)
- Location danger/difficulty matched to quest tier
- Location hints guide players ("forests", "graveyards", "dragon-lair")

**Combat Integration:**
- Kill objectives track specific enemy types
- Combat during investigation/escort quests
- Boss encounters for story quests

**Inventory Integration:**
- Fetch quests require inventory space
- Delivery quests place items in inventory
- Item rewards automatically granted on completion

#### Quest UI

**Quest Journal:** *(Future)*
- View active quests with progress
- Read quest descriptions and lore
- Track primary/secondary/hidden objectives
- Mark quests for tracking

**Quest Markers:** *(Future)*
- Show relevant locations on map
- Update as objectives progress
- Distinguish between main/side quests

**Completion Notifications:**
- Display rewards earned
- Show objective completion in real-time
- Celebrate quest chain completion

### 6. Crafting System

The crafting system allows players to create, modify, and enhance equipment using gathered materials and recipes. Crafting provides an alternative progression path and enables customization beyond found loot.

#### Crafting Stations

**Station Types:**
- **Blacksmith Forge**: Craft and upgrade weapons and armor
- **Alchemy Lab**: Brew potions, elixirs, and consumables
- **Enchanting Altar**: Add magical properties to equipment
- **Tinker's Workbench**: Create utility items and accessories *(Future)*

**Station Access:**
- Found in towns and some dungeon safe rooms
- Portable crafting kits allow field crafting *(Future)*
- Higher-tier stations unlock advanced recipes

#### Materials System

**Material Sources:**
- **Resource Gathering**: Mine ore, harvest herbs, collect components in wilderness
- **Enemy Drops**: Creature parts, elemental essences, rare materials
- **Salvaging**: Dismantle unwanted equipment for materials
- **Vendor Purchase**: Buy common materials from merchants
- **Quest Rewards**: Rare materials as quest completion rewards

**Material Types:**
- **Metals**: Iron, Steel, Mithril, Adamantine (weapon/armor crafting)
- **Cloth & Leather**: Fabric, Hide, Dragon Scale (armor crafting)
- **Herbs & Reagents**: Healing herbs, mana flowers, alchemical components (potions)
- **Gems & Crystals**: Emeralds, Rubies, Soul Gems (enchanting)
- **Creature Parts**: Claws, fangs, scales, essences (special crafting)

**Material Quality:**
- Common, Uncommon, Rare, Epic, Legendary tiers
- Higher quality materials produce superior items
- Material traits affect crafted item properties

#### Recipe System

**Recipe Acquisition:**
- **Starting Recipes**: Basic recipes available immediately
- **Discovery**: Learn recipes by experimenting with materials
- **Trainers**: Purchase recipes from crafting trainers
- **Quest Rewards**: Unique recipes from special quests
- **Found Loot**: Recipe books in dungeons and hidden locations

**Recipe Types:**
- **Equipment Recipes**: Create weapons, armor, shields, accessories
- **Consumable Recipes**: Brew potions and craft utility items
- **Enchantment Recipes**: Apply magical effects to equipment
- **Upgrade Recipes**: Improve existing equipment stats

**Recipe Requirements:**
- Specific materials in required quantities
- Crafting station type and tier
- Character level or skill requirements *(Future)*
- Previous recipe prerequisites for advanced crafts

#### Crafting Process

**Creating Items:**
1. Access appropriate crafting station
2. Select recipe from available recipes
3. Verify material availability in inventory
4. Confirm crafting action
5. Consume materials and create item

**Success and Quality:**
- Crafting success guaranteed for standard recipes *(may add skill checks in future)*
- Item quality matches material quality used
- Critical success chance creates superior items *(Future)*
- Crafting skill affects outcome quality *(Future)*

#### Equipment Enhancement

**Upgrade System:**
- Improve existing equipment with additional materials
- Increases base stats (damage, defense, attributes)
- Multiple upgrade levels per item (typically 3-5 tiers)
- Upgrade costs increase exponentially with tier
- Upgraded items retain enchantments and traits

**Enchanting System:**
- Apply magical effects to crafted or found equipment
- Enchantments provide bonuses (attribute boosts, resistances, special effects)
- Multiple enchantment slots per item based on rarity
- Stronger enchantments require rarer materials
- Enchantments can be replaced but not removed without loss

**Modification System:** *(Future)*
- Socket gems into special item slots
- Reforge items to change stat distributions
- Add trait modifiers using special materials
- Rename custom-crafted items

#### Crafting Skills *(Future)*

**Skill Progression:**
- Separate crafting skills for each profession (Blacksmithing, Alchemy, Enchanting)
- Skills improve through successful crafting
- Higher skill unlocks advanced recipes
- Skill level affects item quality and critical success chance
- Master crafters produce superior items

**Specialization:**
- Focus on specific crafting types for unique bonuses
- Weapon specialist creates superior weapons
- Armor specialist creates superior armor
- Master Alchemist brews more potent potions

#### Economic Integration

**Crafting Value:**
- Crafted items can be sold to merchants for profit
- High-quality crafted items command premium prices
- Crafting can be profitable with rare materials
- Player-crafted items available in shops *(Future)*

**Material Market:**
- Purchase common materials from vendors
- Rare materials sold at premium prices
- Material prices fluctuate based on demand *(Future)*
- Sell excess materials for gold

#### Strategic Depth

**Build Synergy:**
- Craft equipment tailored to specific character builds
- Create items with exact stat distributions desired
- Enchant for targeted bonuses (elemental damage, resistances)
- Optimization beyond random loot drops

**Self-Sufficiency:**
- Reduce reliance on shop inventory
- Craft healing potions for sustainable adventuring
- Create equipment upgrades during long expeditions
- Emergency crafting in dungeon safe rooms

**Collection Goals:**
- Gather all recipes for completionists
- Collect rare materials from elite enemies
- Master all crafting professions
- Create legendary custom equipment sets

### 7. Exploration System

The exploration system provides diverse locations for players to discover and traverse. Locations vary in purpose (towns provide services, dungeons provide combat/loot, wilderness provides random encounters) and difficulty (scaled to player level or fixed challenge rating).

#### Location Types

**Towns:**
- Safe zones where players can access services
- No random enemy encounters
- NPCs provide quests, shops, lore, and social interactions
- Resting/healing available at inns
- Procedurally generated towns with unique names
- Hand-crafted story-critical towns (Hub Town, capital cities)

**Dungeons:**
- Multi-room exploration with increasing difficulty
- Combat encounters in most rooms
- Treasure rooms with rare loot
- Boss encounters in final rooms
- Procedurally generated layouts from templates
- Hand-crafted dungeons for story quests
- May have environmental hazards (traps, darkness, poison gas)

**Wilderness:**
- Open areas connecting towns and dungeons
- Random encounter rate (varies by location danger level)
- Resource gathering opportunities *(Future)*
- Dynamic weather affecting gameplay *(Future)*
- Multiple biomes (forests, mountains, deserts, swamps)

**Points of Interest:** *(Future)*
- Unique, discoverable locations with lore
- One-time special encounters
- Hidden treasure caches
- Lore books and environmental storytelling

#### Exploration Mechanics

**Location Discovery:**
- Locations start hidden until discovered
- Discovery through exploration, quests, or NPC hints
- First visit grants XP bonus
- Discovered locations added to map

**Fast Travel:**
- Return instantly to previously visited locations
- Only accessible from safe zones (towns, camp sites)
- Cannot fast travel during combat or quests
- May cost gold *(Future)* or have cooldown

**Location Levels:**
- Locations have fixed or dynamic difficulty levels
- Enemy spawns match location level (±2 levels)
- Loot quality matches location level
- Low-level players warned when entering high-level areas

**Location States:**
- Undiscovered (hidden, not yet found)
- Discovered (found, can visit)
- Cleared (dungeon boss defeated, reduced encounter rate)
- Active Quest (location relevant to active quest)
- Locked (requires key, quest completion, or level) *(Future)*

#### Location Features

**Enemy Spawns:**
- Location defines enemy types that can appear
- Spawn probability per enemy type
- Boss spawns in specific locations
- Combat frequency varies by location type (towns 0%, dungeons 80%, wilderness 40-60%)

**Loot Distribution:**
- Location defines loot tables
- Rare items in dangerous locations
- Common supplies in towns/safe areas
- Unique items in story locations

**Environmental Properties:**
- Weather conditions (clear, rain, fog, snow) *(Future)*
- Lighting levels (bright, dim, dark) affecting stealth *(Future)*
- Temperature (hot, cold, moderate) affecting stamina *(Future)*
- Danger rating (safe, low, moderate, high, extreme)

**NPC Presence:**
- Towns populated with merchants, quest givers, trainers
- Dungeons may have hostile NPCs (bandits, cultists)
- Wilderness may have travelers, hermits, or wandering merchants
- NPC schedules *(Future)* - appear at different times

#### Town Services

**Shops:**
- Buy and sell items
- Shop inventory based on town tier and merchant type
- Blacksmith (weapons/armor), Alchemist (potions), General Store (supplies)
- Prices affected by player Charisma attribute

**Inns:**
- Rest to fully restore HP/MP
- Save game at inn
- Rumors and lore from patrons
- May offer temporary buffs (well-rested bonus) *(Future)*

**Quest Hubs:**
- NPCs offer quests appropriate to player level
- Quest boards for procedural quests *(Future)*
- Completed quests turn in to quest givers

**Trainers:** *(Future)*
- Teach new abilities
- Respec attribute points (fee required)
- Provide skill training

**Crafting Stations:** *(Future)*
- Blacksmith forges (weapon/armor crafting)
- Alchemy labs (potion brewing)
- Enchanting altars (item enhancement)

#### Dungeon Systems

**Room Types:**
- Combat rooms (enemy encounters)
- Treasure rooms (loot chests, no enemies)
- Rest rooms (safe zone mid-dungeon, limited use)
- Puzzle rooms (challenges requiring wit, not combat) *(Future)*
- Boss rooms (final challenge, unique rewards)

**Multi-Room Progression:**
- Navigate through 5-15 rooms depending on dungeon size
- Map reveals explored rooms *(Future)*
- Cannot backtrack once room cleared (forward-only progression)
- Escape scrolls allow immediate exit *(Future)*

**Dungeon Difficulty:**
- Easy dungeons: 5-7 rooms, common/uncommon loot, level-appropriate enemies
- Medium dungeons: 8-10 rooms, rare loot, +2 level enemies
- Hard dungeons: 11-15 rooms, epic loot, +5 level enemies, boss required
- Legendary dungeons: 15+ rooms, legendary loot, +10 level enemies, multiple bosses

**Dungeon Generation:**
- Procedural generation from room templates
- Ensures path to boss room exists
- Treasure room placement varies (1-3 per dungeon)
- Room layout uses branching paths or linear progression

#### Wilderness Encounters

**Random Events:**
- Combat encounters (60% of wilderness exploration)
- Peaceful encounters (NPCs, travelers, merchants)
- Resource nodes (gather herbs, mine ore) *(Future)*
- Hidden caches (treasure chests off the beaten path)
- Ambushes (higher-level enemies surprise attack) *(Future)*

**Biome-Specific Features:**
- Forests: Abundant herbs, animal enemies, thick undergrowth
- Mountains: Ore nodes, flying enemies, cliff hazards
- Deserts: Heat exhaustion risk, scorpion enemies, oases
- Swamps: Poison hazards, undead enemies, disease risk
- Tundra: Cold damage, ice enemies, limited visibility

**Encounter Scaling:**
- Wilderness difficulty based on distance from safe zones
- Near towns: Low-level, common enemies
- Remote areas: High-level, elite enemies
- Unknown regions: Unpredictable, mixed difficulty

#### Location-Quest Integration

**Quest Objectives:**
- Quests direct players to specific locations
- "Explore [Location]" objectives mark location as quest target
- "Kill enemies in [Location]" require visiting and combat
- "Deliver item to [Location]" require travel and NPC interaction

**Location Hints:**
- Quest descriptions provide location type hints (forests, graveyards, dragon-lair)
- NPCs give directions to quest locations
- Location discovery may complete exploration objectives

**Dynamic Events:** *(Future)*
- Locations change state based on quest progress
- Clearing dungeons reduces enemy respawns
- Completing town quests improves services
- Failed quests may alter location availability

### 8. Achievement System

**6 Achievements** tracking player milestones:

| Achievement | Requirement | Unlocks |
|-------------|-------------|---------|
| **First Blood** | Defeat your first enemy | Badge, 50 bonus XP |
| **Survivor** | Reach level 10 | Title: "The Survivor" |
| **Dragon Slayer** | Defeat a boss enemy | Badge, bonus loot modifier |
| **Treasure Hunter** | Collect 1000 gold | Access to secret shop |
| **Master Explorer** | Visit all locations | Map completion bonus |
| **Champion** | Complete main quest | Victory screen, New Game+ |

Achievements persist across saves and New Game+ playthroughs.

### 9. Difficulty System

Multiple difficulty modes cater to different player preferences and skill levels, adjusting challenge and rewards accordingly.

**Difficulty Modes:**

**Casual Mode:**
- Reduced enemy strength for relaxed gameplay
- Forgiving death penalties
- Focus on story and exploration

**Normal Mode:**
- Balanced challenge for standard play
- Moderate death penalties
- Standard progression and rewards

**Hard Mode:**
- Increased enemy strength requiring tactical play
- Harsher death penalties
- Bonus rewards for skilled players

**Nightmare Mode:**
- Significantly tougher enemies
- Permadeath (character deletion on death)
- Substantially increased rewards

**Apocalypse Mode:**
- Maximum enemy strength
- Permadeath with time pressure
- Countdown timer creates urgency
- Legendary rewards for successful completion
- Quest completion grants time extensions

### 10. Death System

The death system handles character defeat with consequences varying by difficulty mode.

#### Standard Death (Casual, Normal, Hard Modes)

**Death Sequence:**
- Death screen displays character statistics and final moments
- Character respawns with restored health and mana
- Death penalties vary by difficulty (gold loss, item loss)
- Player can immediately continue from death location

**Penalty Scaling:**
Death penalties increase with difficulty mode, ranging from no penalty (Casual) to significant resource loss (Hard).

#### Permadeath (Nightmare, Apocalypse Modes)

**Permanent Consequences:**
- Character is permanently deleted upon death
- No respawn or continue option
- Forces new character creation
- Adds high-stakes tension to gameplay

**Legacy System:**
- Deceased characters added to Hall of Fame leaderboard
- Items may be left behind for future characters
- Achievements and records preserved

#### Hall of Fame

Leaderboard tracking memorable characters:
- Character identity and progression
- Gameplay statistics and accomplishments
- Cause of death and final moments
- Difficulty mode and playtime
- Ranks top characters for posterity

### 11. Save/Load System

A robust save system preserves game state and supports multiple characters.

#### Save Features

**Auto-Save:**
Automatic saves trigger after significant events (combat victories, quest completion, location changes) to minimize progress loss.

**Manual Save:**
Players can manually save at any time outside of combat, providing control over save points.

**Multiple Save Slots:**
Unlimited save slots support multiple characters and playthroughs without overwriting existing saves.

**Save Data Scope:**
Save files preserve complete game state:
- Character attributes, level, and progression
- Full inventory and equipped items
- Quest progress and completion status
- Achievement unlocks and statistics
- Discovered locations and world state
- Difficulty settings and mode flags

### 12. New Game+ System

New Game+ mode allows players to replay the game with bonuses earned from completing the main quest.

**Starting Bonuses:**
- Enhanced starting attributes for increased power
- Improved starting health and mana pools
- Increased starting resources
- Retained achievements and unlocks
- Preserved Hall of Fame records

**Increased Challenge:**
- Enemies scale beyond normal difficulty
- Quest objectives require greater effort
- New difficulty options unlock
- Maintains engaging challenge despite player advantages

**Rewards and Incentives:**
- Earlier access to high-tier items
- Accelerated experience gain
- Unique titles and recognition
- Encourages experimentation with different builds
- Provides extended endgame content

### 13. Magic & Spell System

**Design Philosophy:**
A comprehensive spell-casting system providing magical combat options and utility beyond physical abilities.

**Spell Categories:**
- **Offensive Spells**: Direct damage, area effects, damage-over-time
- **Defensive Spells**: Shields, wards, damage mitigation
- **Healing Spells**: Health restoration, regeneration, cure effects
- **Utility Spells**: Teleportation, detection, illumination, utility
- **Buff/Debuff Spells**: Temporary stat modifications for allies or enemies

**Spell Mechanics:**
- Mana costs balance spell power
- Cooldowns prevent ability spam
- Spell learning through scrolls, trainers, quest rewards
- Spell schools categorize magic types
- Spell combinations create synergistic effects

### 14. Status Effects System

**Design Philosophy:**
Status effects add tactical depth through temporary conditions affecting combat and exploration.

**Effect Categories:**
- **Damage-Over-Time**: Periodic damage (poison, burning, bleeding)
- **Crowd Control**: Movement/action restriction (frozen, stunned, slowed)
- **Stat Modification**: Temporary stat changes (blessed, cursed, weakened)
- **Environmental**: Location-based effects (burning terrain, poison gas)

**Status Mechanics:**
- Duration-based effects with tick rates
- Resistances and immunities reduce/prevent effects
- Cure methods (items, spells, time)
- Stacking rules for multiple effects
- Visual indicators for active statuses

### 15. Party System

**Design Philosophy:**
Recruit and manage NPC allies for cooperative combat and enhanced strategic depth.

**Party Mechanics:**
- **Recruitment**: NPCs join party through quests, reputation, or payment
- **Party Combat**: AI-controlled allies fight alongside player
- **Party Management**: Equip allies, assign roles, manage resources
- **Party Progression**: Allies level up and gain abilities
- **Permadeath Option**: Party members can permanently die based on difficulty

**Strategic Depth:**
- Formation positioning affects combat effectiveness
- Party composition creates tactical variety
- Ally abilities synergize with player skills
- Resource management for entire party

### 16. Reputation & Faction System

**Design Philosophy:**
Player actions influence relationships with factions, unlocking or locking content based on choices.

**Reputation Mechanics:**
- **Multiple Factions**: Guilds, kingdoms, organizations with competing interests
- **Action Consequences**: Quest choices, combat decisions affect reputation
- **Reputation Levels**: Hostile to Exalted with gradual progression
- **Locked Content**: Quests, shops, areas restricted by reputation
- **Multiple Endings**: Story outcomes vary based on faction allegiances

**Faction Types:**
- Story factions affecting main narrative
- Guild factions providing specialized services
- Antagonist factions creating conflict

### 17. Audio System

**Design Philosophy:**
Immersive audio through background music and sound effects enhances atmosphere and player feedback.

**Music System:**
- **Location Music**: Unique themes per area type
- **Combat Music**: Dynamic intensity based on battle state
- **Boss Themes**: Memorable music for major encounters
- **Ambient Soundscapes**: Environmental audio for immersion
- **Victory Fanfares**: Celebration music for achievements

**Sound Effects:**
- **Combat Sounds**: Weapon impacts, ability effects, critical hits
- **UI Sounds**: Menu navigation, confirmations, errors
- **Environmental Sounds**: Ambient effects matching locations
- **Action Feedback**: Audio cues for player actions

### 18. Visual Enhancement System

**Design Philosophy:**
Visual polish through ASCII art, animations, and effects improves presentation without changing core gameplay.

**Visual Features:**
- **ASCII Art**: Location illustrations, title screens, boss portraits
- **Combat Animations**: Attack effects, damage indicators, status visuals
- **Transitions**: Screen transitions, fade effects, loading screens
- **Particle Effects**: Visual flourishes for abilities and criticals
- **UI Polish**: Refined menus, borders, decorative elements

### 19. Online & Community Features

**Design Philosophy:**
Community engagement through leaderboards, challenges, and shared content extends replayability.

**Online Features:**
- **Global Leaderboards**: Rank characters across difficulty modes and playstyles
- **Daily Challenges**: Pre-generated challenges with unique rewards
- **Save Sharing**: Export/import characters and builds
- **Community Events**: Limited-time content and seasonal activities
- **Achievement Showcases**: Display accomplishments and compare with others

**Integration:**
- Optional online connectivity
- Offline play remains fully functional
- Community-driven content discovery

### 20. Quality of Life Enhancements

**Design Philosophy:**
Convenience features reduce friction without compromising challenge or strategic depth.

**QoL Features:**
- **Undo Actions**: Reverse recent mistakes
- **Keybind Customization**: Rebind controls to preference
- **Quick-Save**: Instant save hotkey
- **Tutorial System**: Guided introduction to mechanics
- **Hint System**: Context-sensitive help
- **Difficulty Adjustment**: Change difficulty mid-playthrough
- **Fast Travel**: Quick navigation between discovered locations
- **Auto-Sort**: Automatic inventory organization
- **Batch Actions**: Perform multiple similar actions efficiently

### 21. Modding Support

**Design Philosophy:**
Enable community content creation through modding tools and APIs for extended longevity.

**Modding Features:**
- **Mod Loader**: System for loading custom content
- **Content Creation**: Tools for creating items, quests, locations
- **Scripting API**: Programming interface for custom mechanics
- **Asset Support**: Import custom data files, JSON content
- **Mod Validation**: Ensure mods meet quality standards
- **Community Sharing**: Platform for distributing mods

**Mod Categories:**
- Content mods (items, quests, NPCs)
- Balance mods (difficulty, progression)
- Visual mods (ASCII art, UI themes)
- System mods (new mechanics, features)

### 22. UI Technology Evolution

**Design Philosophy:**
Transition from console-based UI to graphical interface while preserving gameplay mechanics.

**Godot Integration:**
- **Graphical UI**: Rich visual interface replacing console
- **Mouse & Controller**: Multiple input method support
- **Animations**: Smooth transitions and visual feedback
- **Accessibility**: Screen reader support, colorblind modes, font scaling
- **Customization**: Player-adjustable UI layouts and themes

**Migration Strategy:**
- Preserve gameplay mechanics during transition
- Maintain console UI as legacy option
- Gradual feature migration ensuring stability

---

## Game Features

This section describes the **intended feature set** for the complete game. For the current implementation status of each feature, see [IMPLEMENTATION_STATUS.md](IMPLEMENTATION_STATUS.md).

### Core Gameplay

- **Character Creation**: Multiple unique classes with distinct playstyles and starting equipment
- **Turn-Based Combat**: Tactical combat with diverse action options each turn
- **Leveling System**: XP-based progression with flexible attribute allocation
- **Skills & Abilities**: Passive skills (stat bonuses) and active abilities (combat powers)
- **Save/Load**: Persistent game state with auto-save and multiple save slots

### Content Systems

- **Procedural Enemies**: Level-scaled enemies with traits and special behaviors
- **Item Generation**: Multiple rarity tiers with procedural generation
- **Trait System**: Custom properties for items and enemies affecting gameplay
- **Equipment System**: Dedicated equipment slots for weapons, armor, and accessories
- **Inventory Management**: Limited inventory with sorting and item management
- **Consumables**: Health and mana restoration items usable in and out of combat

### Progression

- **Main Quest Chain**: Linear story quests forming the core narrative
- **Achievement System**: Milestone tracking and rewards
- **Difficulty Modes**: Multiple difficulty levels with varying challenge and rewards
- **Permadeath**: Optional permanent death in harder difficulty modes
- **Hall of Fame**: Leaderboard tracking memorable characters
- **New Game+**: Replay with bonus stats and retained achievements

### UI/UX

- **Interactive Menus**: Keyboard navigation for all choices
- **Combat Log**: Detailed combat feedback with colored text
- **Progress Indicators**: Health bars, XP bars, and timers
- **Data Tables**: Inventory, stats, and leaderboard displays
- **Victory Sequence**: Dramatic celebration with statistics and rewards

### Technical Architecture

- **CQRS Pattern**: Command/Query separation for clean business logic
- **Vertical Slice**: Feature-focused organization reducing coupling
- **Event-Driven**: MediatR for decoupled event handling
- **Input Validation**: FluentValidation for robust user input handling
- **Structured Logging**: Serilog for debugging and diagnostics
- **Resilience**: Polly for retry logic and error handling
- **Comprehensive Testing**: Unit tests covering core gameplay systems

### Development Tools

A companion WPF application (**RealmForge**) provides visual editing of game data files with pattern composition, reference browsing, and real-time validation.

For details on RealmForge, see [RealmForge](realm-forge/README.md).  
For development roadmap and future features, see [ROADMAP.md](ROADMAP.md).

---

## Content & Progression

### Level Progression

The game features a comprehensive leveling system with steady progression from beginning to endgame.

**Progression Elements:**
- XP requirements increase as character level rises
- Cumulative XP scales exponentially to maintain challenge
- Health and Mana pools grow with each level
- Attribute points awarded consistently throughout progression
- Skills unlock at milestone levels
- Maximum level provides clear endgame goal

**Early Game** focuses on rapid progression and frequent rewards, **Mid Game** balances challenge and advancement, **Late Game** requires significant investment for each level, creating meaningful long-term goals.

### Enemy Scaling

Enemies scale dynamically with player level to maintain appropriate challenge throughout the game.

**Scaling Mechanics:**
- Enemy levels match player level with minor variance
- Health pools increase substantially with level
- Damage output scales proportionally
- XP rewards scale to match increased challenge
- Gold drops increase with enemy strength
- Boss encounters provide significantly higher difficulty and rewards

**Difficulty Balance:** Enemy strength grows faster than player power, requiring tactical play and character optimization at higher levels.

### Item Progression

Equipment and items follow distinct tiers matching character progression phases.

**Early Game:**
- Common and Uncommon items dominate
- Basic materials (Iron, Bronze)
- Standard consumables with modest effects
- Low gold values

**Mid Game:**
- Rare and Epic items become available
- Advanced materials (Steel, Mithril)
- Enhanced consumables with improved effects
- Moderate gold values

**Late Game:**
- Epic and Legendary items predominate
- Premium materials (Adamantine, Dragonscale)
- Superior consumables with powerful effects
- High gold values

**New Game+:**
- Legendary items more common
- Artifact-tier equipment unlocks
- Exclusive unique items available

### Location Progression

Exploration areas follow structured difficulty progression, guiding players through increasingly challenging content.

**Planned Locations (Future):**

Locations will range from safe starting zones through late-game challenges, each with appropriate enemy levels, loot quality, and quest content. Progression encourages thorough exploration while maintaining appropriate challenge curves.

---

## User Interface

The game features an interactive interface with menus, displays, and visual feedback for all player actions.

### Main Menu

The primary menu provides access to all major game functions:
- Start new adventures
- Load existing saves
- Configure settings
- View Hall of Fame leaderboard
- Exit game

Navigation uses keyboard arrows with visual selection feedback.

### Character Creation

**Name Entry:**
Players input custom character names with validation to ensure appropriate length and characters.

**Class Selection:**
Visual class cards display each class option with:
- Class icon and name
- Attribute bonuses overview
- Playstyle description
- Unique characteristics

Players navigate with arrow keys and confirm selection.

### In-Game Menu

Central hub for gameplay actions:
- Exploration and location travel
- Character sheet viewing
- Inventory management
- Quest tracking (future feature)
- Rest and recovery
- Save game functions
- Return to main menu

Special mode indicators (like Apocalypse countdown timer) display when active.

### Combat Screen

Dynamic combat interface showing:
- Enemy information (name, level, health bar)
- Player status (health, mana bars)
- Action menu (attack, defend, use item, flee)
- Combat log with colored damage feedback
- Critical hit and special effect notifications

Turn-based interaction with clear visual feedback for all actions.

### Character Screen

Comprehensive character information display:
- Basic identity (name, class, level)
- Experience progress bar
- Current resources (HP, MP, Gold)
- Attribute values with modifiers
- Active skills and their effects
- Equipped items with stats
- Overall character power summary

### Inventory Screen

Item management interface:
- Current inventory capacity indicator
- Sortable item list with details
- Item categories (weapons, armor, consumables)
- Equipment slots with equipped items marked
- Item commands (equip, use, drop, sort)
- Gold value display for all items

### Victory Screen

Celebration sequence displaying:
- Victory announcement with dramatic formatting
- Final character statistics summary
- Quest completion status
- Achievement unlocks during playthrough
- Earned rewards (XP, gold, items)
- Total playtime and difficulty completed
- New Game+ unlock notification

### UI Components

**Text Display:**
- Styled text and headers
- Status messages (success, error, warning, info)
- Contextual information displays

**User Input:**
- Text input with prompts
- Numeric input with validation
- Single-selection menus (keyboard navigation)
- Multi-selection menus
- Yes/No confirmations

**Layout:**
- Tables with headers and data rows
- Bordered panels and containers
- Progress indicators for loading/actions
- Pause for user acknowledgment

**Security:** All user input is validated and sanitized to prevent injection attacks.

For documentation on the RealmForge data editor tool, see [RealmForge](realm-forge/README.md).

For development roadmap and future features, see [ROADMAP.md](ROADMAP.md).

---

## Appendix

### Development Guidelines

1. **Adding a New Feature**
   - Create feature folder in `Game/Features/`
   - Add Commands and/or Queries
   - Implement handlers with MediatR
   - Add validators (FluentValidation)
   - Write unit tests (xUnit)
   - Update this GDD

2. **Testing Requirements**
   - All commands must have validators
   - All handlers must have unit tests
   - Integration tests for workflows
   - Minimum 90% code coverage

3. **Code Style**
   - Use record types for DTOs/commands/queries
   - Async/await for all I/O operations
   - Structured logging with Serilog
   - XML documentation for public APIs

4. **Documentation**
   - Update GDD for new features
   - Add guides for complex systems
   - Keep README.md current
   - Document breaking changes

### Glossary

- **CQRS**: Command Query Responsibility Segregation (separate read/write operations)
- **Vertical Slice**: Organizing code by feature instead of technical layer
- **MediatR**: Library for mediator pattern and event-driven architecture
- **LiteDB**: Embedded NoSQL database
- **Bogus**: Library for fake data generation
- **FluentValidation**: Library for building validation rules
- **Polly**: Library for resilience and transient-fault-handling

### External Resources

- **Project Repository**: [GitHub Link]
- **MediatR Docs**: https://github.com/jbogard/MediatR
- **LiteDB Docs**: https://www.litedb.org/
- **FluentValidation Docs**: https://docs.fluentvalidation.net/

### Credits

**Development Team**: Solo developer  
**Architecture Inspiration**: Jimmy Bogard (CQRS), Microsoft (Vertical Slice)  
**Libraries Used**: See Technology Stack section  
**Special Thanks**: .NET community, GitHub Copilot

---

**Document Version**: 1.6  
**Last Updated**: January 5, 2026  
**Status**: Complete - Core features implemented, gap analysis in progress  
**Next Update**: Priority features implementation (Q1 2026)  
**Latest Changes**:
- Updated project name from "Console RPG" to "RealmEngine"
- Removed detailed ContentBuilder/RealmForge sections (see REALMFORGE.md)
- Simplified all sections to remove implementation specifics (formulas, percentages, tables)
- Removed detailed JSON standards (see docs/standards/)
- Updated test counts and statistics (7,823 tests, 100% pass rate)
- Corrected Skills vs Abilities distinction
