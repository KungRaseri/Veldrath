# Progression System

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Progression System handles character advancement through experience, leveling, attribute allocation, skills, abilities, and spells—providing multiple paths for character development.

**All subsystems 100% complete**: Skills, Abilities, and Spells fully integrated into combat and progression.

## Core Components

### Experience & Leveling
- **XP Sources**: Combat, quests, exploration, achievements
- **Leveling Curve**: Exponential scaling to level cap (50)
- **Level Rewards**: Stat increases, attribute points, ability unlocks

### Attribute Allocation
Flexible point distribution each level for specialization, hybrid builds, or balanced approaches.

### Skills System (Practice-Based Progression) ✅ COMPLETE
Learned proficiencies that improve through use—"learning by doing":
- **Skill Categories**: Attribute (24), Weapon (10), Armor (4), Magic (16), Profession (12) = 54 total
- **Ranking System**: Rank 0 (untrained) to 100 (master)
- **Use-Based XP**: Gain skill XP by using the skill (via `AwardSkillXPCommand`)
- **Scaling Costs**: Each rank-up costs more XP (baseXPCost × (1 + currentRank × costMultiplier))
- **Per-Rank Bonuses**: Passive bonuses that automatically apply (+0.5% weapon damage, +0.4% magic power)
- **Build Differentiation**: Organic specialization based on playstyle
- **Combat Integration**: All skill bonuses applied via `SkillEffectCalculator`

**Implementation**: ✅ `SkillDataService`, `SkillProgressionService`, `AwardSkillXPCommand`, `InitializeCharacterSkillsCommand`

See [Skills System](skills-system.md) for full details.

### Abilities System (Class-Specific Powers) ✅ 95% COMPLETE
Class-granted active powers requiring conscious activation:
- **Ability Categories**: Active (177), Passive (131), Reactive (36), Ultimate (39) = 383 total
- **Resource Management**: Mana costs and cooldown timers tracked per character
- **Class Identity**: Each class has unique ability set (via `GetAvailableAbilitiesQuery`)
- **Level Unlocks**: Tier-based unlocking (Tier 1-5, levels 1/5/10/15/20)
- **Tactical Depth**: Growing toolkit of combat options
- **Passive Bonuses**: +5 damage, +2% crit, +3% dodge, +5 defense per ability type
- **Reactive Triggers**: Auto-execute on onCrit, onDodge, onBlock, onDamageTaken
- **Enemy AI**: Intelligent ability usage based on health, combat phase, player status

**Implementation**: ✅ `AbilityDataService`, `LearnAbilityCommand`, `UseAbilityCommand`, `PassiveBonusCalculator`, `ReactiveAbilityService`, `EnemyAbilityAIService`

See [Abilities System](abilities-system.md) for full details.

### Magic & Spell System (Learnable Magic) ✅ 90% COMPLETE
Universal magical knowledge accessible to all with sufficient skill:
- **Spell Traditions**: Arcane (36), Divine (36), Occult (36), Primal (36) = 144 total
- **Acquisition**: Learn via `LearnSpellCommand` from spellbooks/trainers
- **Skill Requirements**: Minimum magic skill rank per spell rank (0-10)
- **Spell Ranks**: 0 (Cantrip) to 10 (Legendary)
- **Skill Scaling**: Tradition + specialist skills affect power/success rate
- **Build Enabling**: Allows hybrid builds (warrior-mage, rogue-mage, etc.)
- **Combat Integration**: Cast via `CastSpellCommand` with mana costs and success checks

**Implementation**: ✅ `SpellDataService`, `SpellCastingService`, `LearnSpellCommand`, `CastSpellCommand`, `GetLearnableSpellsQuery`

See [Spells System](spells-system.md) for full details.

## Key Features

- **Triple Progression**: Levels (core stats), Skills (practice-based), Abilities/Spells (active powers)
- **Flexible Growth**: Multiple valid build paths per class
- **Organic Development**: Skills improve naturally through gameplay
- **Knowledge Collection**: Finding spells creates exploration goals
- **Strategic Investment**: Balance between specialization and versatility
- **Incremental Power**: Steady growth throughout level range

## System Distinctions

**Skills vs Abilities vs Spells:**
- **Skills** = Passive proficiency that improves with use (how good you are at something)
- **Abilities** = Class-granted active powers (special moves always available)
- **Spells** = Learnable magic requiring acquisition (universal access, skill-dependent)

All three systems work together:
- Skills enhance ability/spell effectiveness
- Abilities provide immediate tactical options
- Spells offer build variety and collection goals

## Related Systems

- [Character System](character-system.md) - Base attributes and classes
- [Skills System](skills-system.md) - Practice-based progression details
- [Abilities System](abilities-system.md) - Class power details
- [Spells System](spells-system.md) - Magic system details
- [Combat System](combat-system.md) - Skills/abilities/spells affect combat
- [Quest System](quest-system.md) - XP rewards from quests

