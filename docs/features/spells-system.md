# Spells System

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Spells System provides spells organized into 4 magical traditions (Arcane, Divine, Occult, Primal) based on Pathfinder 2e. Spells are ranked from 0 (Cantrip) to 10. Any character can learn spells through spellbooks, scrolls, teachers, and quest rewards, but requires the appropriate tradition skill to cast effectively.

## Core Philosophy

**"Learnable Magic"**: Spells are not class-restricted—anyone can learn magic if they invest in magic skills. A Warrior can learn Fireball if they develop Destruction skill, though they won't be as effective as a dedicated Mage.

**Skill-Dependent**: Spell effectiveness scales with magic skills. Low skill = weak spells, high failure chance. High skill = powerful spells, reliable casting.

**Knowledge Acquisition**: Spells must be found, purchased, or taught—they're not automatic. This creates exploration incentives and progression goals.

**Distinct from Abilities & Skills**:
- **Skills** = Proficiency at something (passive progression)
- **Abilities** = Class-granted powers (active, always available)
- **Spells** = Learnable magic (active, must be acquired, skill-dependent)

## Magical Traditions (Pathfinder 2e)

### Arcane Tradition
**Governing Attribute**: Intelligence  
**Theme**: Study-based magic, raw power, manipulation of forces

**Magic Skills**:
- **Arcane** (core): Unlocks all Arcane spells
- **Force Magic**: Boosts force/kinetic spells
- **Chronomancy**: Boosts time manipulation spells
- **Conjuration**: Boosts summoning/creation spells

**Sample Spells**:
- **Cantrips**: Force Missile, Detect Magic, Prestidigitation, Light, Mage Hand, Read Magic, Ghost Sound, Dancing Lights
- **Rank 1-3**: Magic Missile, Shield, Invisibility, Haste, Teleport, Fireball, Lightning Bolt
- **Rank 4-7**: Dimension Door, Wall of Force, Telekinesis, Disintegrate, Delayed Blast Fireball
- **Rank 8-10**: Time Stop, Meteor Swarm, Wish

### Divine Tradition
**Governing Attribute**: Wisdom  
**Theme**: Faith-based magic, healing, protection, holy power

**Magic Skills**:
- **Divine** (core): Unlocks all Divine spells
- **Restoration**: Boosts healing spells
- **Smiting**: Boosts holy damage spells
- **Warding**: Boosts protection spells

**Sample Spells**:
- **Cantrips**: Stabilize, Sacred Flame, Guidance, Resistance, Virtue, Detect Alignment, Purify Food, Create Water
- **Rank 1-3**: Heal, Bless, Shield of Faith, Prayer, Cure Disease, Remove Curse, Restoration
- **Rank 4-7**: Holy Smite, Divine Power, Flame Strike, Greater Restoration, Regeneration
- **Rank 8-10**: Mass Heal, Holy Aura, Miracle

### Occult Tradition
**Governing Attribute**: Charisma  
**Theme**: Mental/psychic magic, mind control, fear, illusions

**Magic Skills**:
- **Occult** (core): Unlocks all Occult spells
- **Enchantment**: Boosts mind control spells
- **Illusion**: Boosts deception/illusion spells
- **Shadowcraft**: Boosts shadow magic spells

**Sample Spells**:
- **Cantrips**: Daze, Message, Ghost Sound, Mage Hand, Prestidigitation, Detect Thoughts, Read Aura, Telekinetic Projectile
- **Rank 1-3**: Charm Person, Sleep, Invisibility, Fear, Suggestion, Phantasmal Killer
- **Rank 4-7**: Confusion, Dominate Person, Greater Invisibility, Feeblemind, Shadow Conjuration
- **Rank 8-10**: Weird, Dominate Monster, Mass Charm

### Primal Tradition
**Governing Attribute**: Wisdom  
**Theme**: Nature-based magic, elements, beasts, plants, weather

**Magic Skills**:
- **Primal** (core): Unlocks all Primal spells
- **Elementalism**: Boosts elemental damage spells
- **Beast Mastery**: Boosts animal/beast spells
- **Verdancy**: Boosts plant/nature spells

**Sample Spells**:
- **Cantrips**: Produce Flame, Ray of Frost, Thorn Whip, Shillelagh, Druidcraft, Guidance, Detect Poison, Purify Water
- **Rank 1-3**: Burning Hands, Ice Storm, Call Lightning, Summon Animal, Barkskin, Entangle
- **Rank 4-7**: Flame Strike, Cone of Cold, Summon Elemental, Wall of Thorns, Earthquake
- **Rank 8-10**: Meteor Swarm, Tsunami, Storm of Vengeance

## Spell Mechanics

### Learning Spells

**Acquisition Methods**:
- **Spellbooks**: Find in loot, buy from merchants, quest rewards
  - Consumable item that teaches spell permanently
  - Rarity matches spell power (common to legendary)
- **Scrolls**: Single-use spell casting
  - Cast spell without knowing it
  - No skill check required (spell always succeeds)
  - Can learn spell by studying scroll (chance based on skill)
- **Trainers**: NPCs teach spells for gold
  - Must meet skill requirements
  - Training costs scale with spell level
- **Quest Rewards**: Special spells from story/side quests
  - Unique spells not available elsewhere
  - Often tied to spell school mastery quests

**Skill Requirements**:
Each spell requires the core tradition skill to cast:
- **Core Tradition Skill**: Must have rank in Arcane/Divine/Occult/Primal to access spells
- **Minimum Skill Rank**: Based on spell rank
  - **Rank 0-1**: Skill rank 0+ (anyone can attempt)
  - **Rank 2-3**: Skill rank 20+ (basic proficiency)
  - **Rank 4-5**: Skill rank 40+ (intermediate)
  - **Rank 6-7**: Skill rank 60+ (advanced)
  - **Rank 8-10**: Skill rank 80+ (master level)
- **Specialist Skills**: Force Magic, Restoration, Enchantment, etc. boost specific spell types
  - Do NOT unlock spells, only increase effectiveness
  - Example: High Elementalism boosts Fireball damage but doesn't unlock it

### Casting Mechanics

**Mana Costs**:
Spells consume mana based on rank (except cantrips):
- **Rank 0 (Cantrips)**: 0 mana (unlimited casting)
- **Rank 1-2**: 10-20 mana
- **Rank 3-4**: 25-40 mana
- **Rank 5-6**: 50-70 mana
- **Rank 7-8**: 80-120 mana
- **Rank 9-10**: 130-200 mana

**Tradition skill affects cost**: Higher skill = reduced mana cost (up to 50% reduction at rank 100)

**Casting Time**:
- **Instant**: Most spells cast immediately (1 action)
- **Cantrips**: Free action, can be cast alongside other actions
- **Channeled**: Some spells require concentration (future)
- **Ritual**: Powerful out-of-combat casting (future)

**Success/Failure**:
- **Success Rate**: Based on tradition skill rank vs spell minimum
  - Meeting requirement = 90% base success
  - 20 ranks above = 99% success (near-guaranteed)
  - 10 ranks below = 60% success (risky but possible)
- **Fizzle**: Failed cast consumes half mana, no effect
- **Backfire**: Critical failure damages caster (rare, <5% chance)

**Skill Scaling**:
Spell effectiveness increases with tradition and specialist skills:
- **Damage Spells**: +1% per tradition skill rank + specialist skill bonus
  - Example: 50 Arcane + 30 Force Magic = +80% Force Missile damage
- **Healing Spells**: +1% per tradition skill rank + Restoration bonus
  - Example: 60 Divine + 40 Restoration = +100% Heal effectiveness
- **Duration Spells**: +2% duration per tradition skill rank
- **Efficiency**: -0.5% mana cost per skill rank (max 50% reduction)

### Spell Rank System

Spells are organized by rank from 0 (Cantrips) to 10:

**Rank Distribution per Tradition** (36 spells each):
- **Rank 0 (Cantrips)**: 8 spells — Free to cast, unlimited use
- **Rank 1**: 6 spells — Basic spells
- **Rank 2**: 5 spells — Utility and combat
- **Rank 3**: 4 spells — Intermediate power
- **Rank 4**: 3 spells — Advanced magic
- **Rank 5**: 3 spells — Expert tier
- **Rank 6**: 2 spells — Master tier
- **Rank 7**: 2 spells — Powerful magic
- **Rank 8**: 1 spell — Legendary
- **Rank 9**: 1 spell — Epic
- **Rank 10**: 1 spell — Ultimate

**Sample Progression (Arcane Tradition)**:
- **Rank 0**: Force Missile, Detect Magic, Light, Prestidigitation (free cantrips)
- **Rank 1**: Magic Missile (1d4+1 force × 3), Shield (+4 AC, 5 turns)
- **Rank 3**: Fireball (8d6 fire, 20ft radius), Lightning Bolt (8d6 lightning, 100ft line)
- **Rank 5**: Teleport (instant travel), Wall of Force (impenetrable barrier)
- **Rank 7**: Delayed Blast Fireball (12d6+12 fire, delayed detonation)
- **Rank 10**: Time Stop (stop time, take 1d4+1 extra turns)

**Sample Progression (Divine Tradition)**:
- **Rank 0**: Stabilize, Sacred Flame, Guidance (free cantrips)
- **Rank 1**: Heal (1d8+WIS healing), Bless (+1 attack/saves to allies)
- **Rank 3**: Prayer (+1 all rolls to allies, -1 to enemies), Cure Disease
- **Rank 5**: Flame Strike (8d6 fire/radiant), Greater Restoration (remove major debuffs)
- **Rank 7**: Regeneration (restore limbs, heal over time)
- **Rank 10**: Miracle (duplicate any spell or custom effect)

**Mana Costs by Rank**:
- **Rank 0**: 0 mana (cantrips are free)
- **Rank 1-2**: 10-20 mana
- **Rank 3-4**: 25-40 mana
- **Rank 5-6**: 50-70 mana
- **Rank 7-8**: 80-120 mana
- **Rank 9-10**: 130-200 mana

## Spell Integration

### Combat Integration
- **Turn Action**: Casting a spell uses combat turn
- **Targeting**: Select single enemy, area, or ally
- **Spell Selection**: UI shows learned spells with mana costs
- **Cooldowns**: Some powerful spells have cooldowns (separate from mana)

### Skill Integration
- **Skill Checks**: Success rate based on magic skill rank
- **Damage/Healing Scaling**: Spell power increases with skill
- **Mana Efficiency**: Higher skill reduces costs
- **Fizzle Reduction**: Higher skill = fewer failures

### Exploration Integration
- **Utility Spells**: Some spells usable outside combat
  - Light (illuminate dark areas)
  - Detect Magic (find hidden items)
  - Unlock (open locked doors/chests)
  - Teleport (fast travel) *(future)*
- **Scroll Usage**: Cast spells without learning them
- **Strategic Preparation**: Pre-cast buffs before combat

### Progression Integration
- **Spell Collection**: Finding spells is progression goal
- **Skill Requirements**: Encourages magic skill investment
- **Build Enabling**: Spells allow non-mages to use magic
- **Hybrid Builds**: Warriors casting buffs, Rogues using invisibility

## Spellbook UI

### Spell Selection (Combat)
- **Spell List**: Show learned spells grouped by school
- **Mana Display**: Show cost and remaining mana
- **Cooldown Status**: Highlight ready vs on-cooldown spells
- **Quick Cast**: Number hotkeys for fast casting

### Spellbook (Inventory)
- **All Learned Spells**: View entire spell collection
- **School Organization**: Filter by Destruction, Restoration, etc.
- **Spell Details**: Full descriptions, costs, requirements
- **Spell Progress**: Show how skill rank affects spell power

### Learning Interface
- **Spellbook Item**: Use to learn spell permanently
- **Skill Check**: Confirm meeting requirements
- **Success Message**: Spell added to spellbook
- **Failure Message**: Not enough skill (show requirement)

## Design Goals

### Universal Magic Access
- **Not Class-Locked**: Anyone can learn spells
- **Skill-Gated**: Proficiency required for effectiveness
- **Investment Rewarded**: Dedicated mages outperform dabblers

### Knowledge Collection
- **Exploration Incentive**: Spells found in world
- **Merchant Economy**: Buy spellbooks from shops
- **Quest Rewards**: Unique spells from story
- **Replayability**: Different spells each playthrough

### Tactical Depth
- **School Diversity**: Different schools solve different problems
- **Mana Management**: Limited resource forces decisions
- **Success Risk**: Low-skill casting is risky but possible
- **Build Variety**: Spell selection defines playstyle

### Distinct Identity
- **Different from Abilities**: Abilities = always available, Spells = must learn
- **Different from Skills**: Skills = passive bonuses, Spells = active magic
- **Complements Both**: Works with skills (scaling) and abilities (tactical options)

### Simple Core, Expandable
- **Start Small**: 3-5 spells per school initially
- **Expand Schools**: Add more spells over time
- **Advanced Mechanics**: Spell combinations, metamagic (future)
- **Spell Crafting**: Create custom spells (future)

## Related Systems

- [Skills System](skills-system.md) - Magic skills determine spell effectiveness
- [Abilities System](abilities-system.md) - Distinct from class-granted powers
- [Combat System](combat-system.md) - Spells used in combat
- [Progression System](progression-system.md) - Spell learning is progression goal
- [Inventory System](inventory-system.md) - Spellbooks and scrolls as items
