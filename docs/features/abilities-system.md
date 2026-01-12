# Abilities System

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Abilities System provides **383 total abilities** organized by activation type and power tier. Abilities are special powers that characters acquire through class selection, progression, and discovery. Unlike skills (passive proficiencies), abilities are discrete powers with specific effects. Unlike spells (learnable magic), abilities are inherent character powers not requiring spell acquisition.

**Combat Integration Status** (January 7, 2026):
- ✅ **AbilityCatalogService** - Loads all 383 abilities from 4 JSON catalogs
- ✅ **LearnAbilityCommand** - Class/level validation and learning system
- ✅ **UseAbilityCommand** - Execution with damage, healing, cooldowns
- ✅ **PassiveBonusCalculator** - Passive ability bonuses applied in combat
- ✅ **ReactiveAbilityService** - Auto-triggered reactive abilities (4 events)
- ✅ **EnemyAbilityAIService** - Intelligent enemy ability usage
- ✅ **Combat Menu Integration** - Abilities shown with cooldowns/costs
- ✅ **Class Starting Abilities** - Auto-learn fully implemented

## Core Philosophy

**"Special Powers"**: Abilities are signature moves defining character identity. A Warrior's Charge, a Rogue's Backstab, a Mage's Fireball—these are memorable powers that feel impactful.

**Active Activation**: Abilities must be manually triggered during appropriate situations (combat turns, exploration moments).

**Class Identity**: Each class has a unique ability set reflecting their archetype. Warriors are melee-focused, Rogues are cunning, Mages are arcane.

**Distinct from Skills & Spells**: 
- **Skills** = Passive proficiencies practiced through use (54 skills, ranks 0-100)
- **Abilities** = Special powers with activation requirements (383 abilities, tiers 1-5)
- **Spells** = Learnable magic requiring tradition access (144 spells, ranks 0-10)

## Ability Organization

### By Activation Type (4 Types)

**Active Abilities (177 total)**: Require manual activation with mana cost and cooldown
- **Offensive (88)**: Direct damage powers
- **Defensive (34)**: Blocks, dodges, damage reduction
- **Support (27)**: Buffs, healing, utility
- **Utility (28)**: Non-combat benefits, exploration powers
- **Control (8)**: Crowd control, disables, movement impairment
- **Summon (4)**: Creature summoning
- **Mobility (2)**: Movement abilities

**Passive Abilities (131 total)**: Always-active effects requiring no activation
- **General (16)**: Broad passive bonuses
- **Offensive (38)**: Damage, critical, attack bonuses
- **Defensive (39)**: Armor, resistance, health bonuses
- **Leadership (24)**: Party/aura effects
- **Environmental (22)**: Situational bonuses
- **Mobility (7)**: Movement speed, terrain bonuses
- **Sensory (1)**: Perception and detection

**Reactive Abilities (36 total)**: Auto-trigger on specific conditions
- **Offensive (14)**: Counter-attacks, damage procs
- **Defensive (12)**: Auto-blocks, damage reflection
- **Utility (10)**: Situational auto-responses

**Ultimate Abilities (39 total)**: Powerful tier 5 abilities with major impact
- All ultimates are tier 5 (epic/legendary power level)
- Long cooldowns, high mana costs
- Game-changing effects

### By Power Tier (5 Tiers)

Abilities are tiered by power level (based on `selectionWeight` in data):

- **Tier 1 (Basic)**: selectionWeight < 50 — Starting abilities, common powers
- **Tier 2 (Common)**: selectionWeight 50-99 — Standard abilities
- **Tier 3 (Uncommon)**: selectionWeight 100-199 — Enhanced abilities
- **Tier 4 (Rare)**: selectionWeight 200-399 — Powerful abilities
- **Tier 5 (Epic/Legendary)**: selectionWeight 400+ or Ultimate type — Iconic abilities

**Note**: Ultimate abilities are always tier 5 regardless of selectionWeight.

## Ability Mechanics

### Resource Management

**Mana Costs** (stored in `manaCost` trait for active abilities):
- **Minor Abilities**: 10-25 mana (low-cost, frequent use)
- **Major Abilities**: 30-60 mana (moderate-cost, tactical use)
- **Ultimate Abilities**: 70-150 mana (high-cost, rare use)
- **Passive/Reactive**: No mana cost (always active or condition-triggered)

**Cooldown Timers** (stored in `cooldown` trait for active abilities):
- **Short Cooldown**: 2-5 seconds (frequent use)
- **Medium Cooldown**: 6-15 seconds (tactical spacing)
- **Long Cooldown**: 20-60 seconds (powerful abilities)
- **Ultimate Cooldown**: 60-300 seconds (game-changing moments)
- **Passive/Reactive**: No cooldown (always available)

**Trigger Conditions** (stored in `triggerCondition` trait for reactive abilities):
- **onHit**: Activates when character is hit
- **onCrit**: Activates on critical hit (dealing or receiving)
- **onKill**: Activates when enemy is killed
- **onLowHealth**: Activates when health drops below threshold
- **onSpellCast**: Activates when spell is cast
- **onBlock**: Activates when successful block occurs

### Requirements

**Equipment Requirements**:
- Some abilities require specific gear (staff for arcane abilities, shield for Shield Wall)
- Prevents ability abuse outside intended contexts
- Encourages equipment diversity

**Level Requirements**:
- Starting abilities: Available at level 1
- Unlocked abilities: Gate powerful abilities behind progression (levels 5, 10, 15, 20, etc.)
- Prevents low-level power spikes

**Skill Requirements** (future):
- High-level abilities may require skill thresholds (Two-Handed Rank 50 for Execute)
- Encourages specialization and mastery

### Ability Acquisition

**Starting Abilities**:
Each class begins with 2-3 tier 1 signature abilities defining their playstyle:
- **Warrior**: Charge (gap closer), Shield Bash (stun), Battle Cry (buff)
- **Rogue**: Backstab (high damage), Evasion (dodge), Poison Strike (DoT)
- **Mage**: Force Missile (ranged damage), Mana Shield (defense), Blink (teleport)
- **Cleric**: Smite (holy damage), Heal (restoration), Shield of Faith (protection)
- **Ranger**: Power Shot (ranged burst), Trap (utility), Hunter's Mark (debuff)
- **Paladin**: Holy Strike (damage+heal), Protective Aura (party defense)

**Level-Up Unlocks**:
Gain new abilities at specific level milestones based on tier:
- **Tier 1 Abilities**: Available at level 1 (starting abilities)
- **Tier 2 Abilities**: Unlocked at level 5+
- **Tier 3 Abilities**: Unlocked at level 10+
- **Tier 4 Abilities**: Unlocked at level 15+
- **Tier 5 Abilities**: Unlocked at level 20+ (including all ultimates)

**Ability Selection** (future):
Characters may choose abilities from a pool based on:
- Class ability lists
- Tier requirements (level-gated)
- Build focus (offensive, defensive, support)
- Specialization paths

## Class Ability Sets

### Warrior Abilities
Melee-focused powers for frontline combat:
- **Charge**: Rush at enemy, bonus damage, short stun
- **Shield Bash**: Interrupt attack, stun enemy
- **Whirlwind**: Damage all nearby enemies (AoE)
- **Execute**: Bonus damage against low-health targets
- **Battle Cry**: Temporary +damage/+defense buff
- **Last Stand** (Ultimate): Survive lethal damage, heal to 50%

### Rogue Abilities
Cunning powers for burst damage and evasion:
- **Backstab**: Massive damage from stealth/behind
- **Evasion**: Dodge next attack
- **Poison Strike**: Damage + DoT effect
- **Vanish**: Enter stealth, reset combat
- **Shadow Step**: Teleport behind enemy
- **Assassination** (Ultimate): Instant-kill low-health enemy

### Mage Abilities
Arcane powers for ranged magical damage:
- **Fireball**: High fire damage, small AoE
- **Mana Shield**: Absorb damage with mana
- **Frost Nova**: Freeze nearby enemies
- **Arcane Missiles**: Multiple weak hits
- **Spell Steal**: Copy enemy buff (future)
- **Meteor** (Ultimate): Massive fire AoE

### Cleric Abilities
Divine powers for healing and holy damage:
- **Smite**: Holy damage vs undead
- **Heal**: Restore health to self or ally
- **Divine Shield**: Temporary invulnerability
- **Cleanse**: Remove debuffs
- **Blessing**: Party-wide buff
- **Divine Intervention** (Ultimate): Full heal + resurrect (future)

### Ranger Abilities
Nature-focused powers for ranged combat and utility:
- **Power Shot**: High-damage bow attack
- **Trap**: Set trap at location
- **Hunter's Mark**: Increase damage vs target
- **Pet Summon**: Call animal companion (future)
- **Camouflage**: Stealth in wilderness
- **Arrow Storm** (Ultimate): Multiple arrow volley

### Paladin Abilities
Holy warrior powers balancing offense and defense:
- **Holy Strike**: Damage + self-heal
- **Protective Aura**: Reduce party damage
- **Divine Smite**: High holy damage
- **Lay on Hands**: Strong single-target heal
- **Consecration**: AoE holy damage zone
- **Judgment** (Ultimate): Execute + AoE holy explosion

## Species Abilities (Future)

Racial passive or active abilities based on character species:
- **Dwarf**: Stoneform (damage reduction), Magic Resistance
- **Elf**: Arcane Affinity (+magic damage), Keen Senses (+perception)
- **Orc**: Berserker Rage (+damage, -defense), Intimidation
- **Human**: Versatile (bonus skill XP), Diplomatic (+persuasion)

## Ability Integration

### Combat Integration
- **Turn-Based Usage**: Abilities replace Attack action during combat
- **Tactical Decisions**: Choose between normal attack, ability, item, or flee
- **Cooldown Tracking**: UI displays available vs on-cooldown abilities
- **Mana Management**: Balance resource spending across long fights

### Exploration Integration
- **Utility Abilities**: Some abilities usable outside combat (Detect Traps, Night Vision)
- **Stealth Abilities**: Rogues use abilities during exploration
- **Situational Powers**: Context-sensitive abilities (Leap across gap, Break door)

### Progression Integration
- **Level-Gated**: Abilities unlock as character levels
- **Skill-Synergy**: Skills improve ability effectiveness (high Destruction skill boosts Fireball)
- **Build Diversity**: Ability selection defines combat approach

## Ability UI

### Combat Interface
- **Ability Bar**: List available abilities with mana costs
- **Cooldown Display**: Show turns remaining until ready
- **Hotkeys**: Number keys (1-6) for quick activation
- **Tooltips**: Hover for ability details (damage, effect, cost)

### Ability Book/Page
- **All Abilities**: View learned and locked abilities
- **Descriptions**: Full ability details and requirements
- **Upgrade Paths**: See how abilities improve (future)

## Design Goals

### Impactful Power
- **Feel Powerful**: Abilities should feel significantly stronger than basic attacks
- **Distinct Effects**: Each ability has clear visual and mechanical identity
- **Memorable Moments**: Ultimate abilities create epic combat highlights

### Tactical Depth
- **Resource Management**: Balance mana and cooldowns
- **Situational Use**: Right ability at right time matters
- **Combo Potential**: Abilities synergize with skills and equipment

### Class Identity
- **Unique Kits**: Each class plays distinctly different
- **Archetype Clarity**: Warrior = melee, Rogue = burst, Mage = ranged magic
- **Build Variety**: Within class, different ability selections create sub-archetypes

### Simple Initially, Expandable Later
- **Core Set**: Start with 2-3 abilities per class
- **Gradual Unlocks**: Add abilities through leveling
- **Future Systems**: Support upgrades, combinations, customization

## Related Systems

- [Character System](character-system.md) - Class determines ability set
- [Skills System](skills-system.md) - Skills enhance ability effectiveness
- [Spells System](spells-system.md) - Distinct from learnable magic
- [Combat System](combat-system.md) - Abilities used in combat
- [Progression System](progression-system.md) - Leveling unlocks abilities
