# Skills System

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Skills System provides practice-based progression where using skills improves proficiency over time. With **54 total skills** organized into 5 categories (Attribute, Weapon, Armor, Magic, Profession), the system offers deep specialization and diverse character builds. Skills are passive bonuses that rank up through repeated use, creating organic character development distinct from level-based progression and separate from the Abilities and Spells systems.

## Core Philosophy

**"Learning by Doing"**: Skills improve naturally through gameplay actions. Use weapons to improve weapon skills, cast spells to improve magic skills, pick locks to improve Lockpicking.

**Passive Bonuses**: Skills provide always-active benefits requiring no manual activation. Effects automatically apply during relevant actions (increased damage, better success rates, reduced costs).

**Attribute Synergy**: Each skill is governed by a related attribute. Skill checks combine skill rank + attribute modifier. A character with high DEX and high Acrobatics gets bonuses from both.

**Distinct from Abilities**: Skills are proficiencies (how good you are at something), abilities are powers (special moves requiring activation). High Light Blades skill makes sword attacks stronger; Whirlwind Attack is a special ability requiring mana/cooldown.

**Distinct from Spells**: Skills are practiced through use; spells are learned through acquisition. Magic skills boost spell effectiveness but don't grant spells. Core tradition skills (Arcane, Divine, Occult, Primal) unlock spell access; specialist skills (Force Magic, Restoration, etc.) boost specific spell types.

## Skill Categories

**Total Skills: 54** organized into 5 major categories:

### Attribute Skills (24 Skills)
Skills governed by and complementing each attribute. Skill checks combine skill rank + attribute modifier, creating meaningful synergy.

**Strength-Based (4)**:
- **Athletics**: Running, jumping, general physical activity (+movement speed, +jump height)
- **Swimming**: Aquatic movement and underwater endurance (+swim speed, +underwater duration)
- **Climbing**: Scaling walls, cliffs, and obstacles (+climb speed, +grip strength)
- **Carrying**: Load capacity and burden management (+carry weight, +reduced encumbrance)

**Dexterity-Based (4)**:
- **Acrobatics**: Tumbling, rolling, aerial maneuvers (+dodge chance, +fall damage reduction)
- **Stealth**: Silent movement and concealment (+detection avoidance, +backstab damage)
- **Sleight of Hand**: Quick finger work and manual dexterity (+pickpocket success, +trap disarm)
- **Lockpicking**: Opening locks and mechanisms (+unlock speed, +break chance reduction)

**Constitution-Based (4)**:
- **Endurance**: Physical stamina and resistance to exhaustion (+stamina pool, +recovery rate)
- **Concentration**: Mental focus during interruptions (+spell focus, +casting interruption resistance)
- **Disease Resistance**: Immunity to illnesses (+disease resist chance, +recovery speed)
- **Poison Resistance**: Protection from toxins (+poison resist chance, +reduced duration)

**Intelligence-Based (4)**:
- **Arcana**: Knowledge of magical theory (+magic damage, +spell critical chance)
- **Investigation**: Problem solving and clue finding (+search speed, +hidden object detection)
- **Nature**: Understanding of flora, fauna, and ecosystems (+herbalism yield, +beast handling)
- **History**: Knowledge of lore, legends, and civilizations (+lore discoveries, +ancient artifact identification)

**Wisdom-Based (4)**:
- **Perception**: Visual awareness and sensory acuity (+detection range, +ambush avoidance)
- **Insight**: Reading emotions and detecting lies (+NPC disposition accuracy, +lie detection)
- **Medicine**: Healing wounds and treating ailments (+healing effectiveness, +stabilization success)
- **Survival**: Wilderness navigation and resource finding (+tracking, +resource yield)

**Charisma-Based (4)**:
- **Persuasion**: Convincing others through logic and charm (+dialogue success, +vendor discounts)
- **Intimidation**: Coercing through threats and presence (+fear effectiveness, +surrender chance)
- **Deception**: Lying and misdirection (+disguise effectiveness, +lie success)
- **Performance**: Entertaining and captivating audiences (+bard song effects, +crowd influence)

### Weapon Skills (10 Skills)
Granular weapon proficiencies allowing specialized combat builds:
- **Light Blades**: Daggers, short swords, rapiers (DEX-focused, fast, criticals)
- **Heavy Blades**: Longswords, greatswords, bastard swords (STR-focused, versatile)
- **Axes**: Hand axes, battle axes, great axes (STR-focused, high damage, armor penetration)
- **Blunt**: Maces, hammers, flails (STR-focused, armor penetration, stagger)
- **Polearms**: Spears, halberds, glaives (reach, defensive, mounted combat)
- **Bows**: Shortbows, longbows, composite bows (DEX-focused, range, precision)
- **Crossbows**: Light, heavy, repeating crossbows (STR-focused, armor penetration, reload)
- **Throwing**: Throwing knives, javelins, throwing axes (DEX or STR, range)
- **Unarmed**: Fists, claws, martial arts (DEX or STR, always available)
- **Shield**: Blocking and shield bashing (defensive, utility)

### Armor Skills (4 Skills)
Proficiency with protective gear, each offering distinct trade-offs:
- **Light Armor**: Leather, hide (high mobility, low protection, DEX-friendly)
- **Medium Armor**: Chainmail, scale (balanced, moderate penalties)
- **Heavy Armor**: Plate, full armor (maximum protection, high penalties, STR requirement)
- **Unarmored Defense**: Fighting without armor (mobility, specific class bonuses)

### Magic Skills (16 Skills)
Four core traditions, each with three specialized paths for focused spellcasting mastery:

**Arcane Tradition (INT-based)**:
- **Arcane**: Core tradition unlocking all Arcane spells
- **Force Magic**: Specialization in pure magical force (Force Missile, Force Blade, Arcane Shield)
- **Chronomancy**: Specialization in time manipulation (Haste, Slow, Time Stop)
- **Conjuration**: Specialization in summoning and creation (Summon Creature, Conjure Weapon)

**Divine Tradition (WIS-based)**:
- **Divine**: Core tradition unlocking all Divine spells
- **Restoration**: Specialization in healing and curing (Heal, Cure, Regeneration)
- **Smiting**: Specialization in holy damage (Smite, Holy Light, Divine Strike)
- **Warding**: Specialization in protection magic (Shield of Faith, Sanctuary, Holy Ward)

**Occult Tradition (CHA-based)**:
- **Occult**: Core tradition unlocking all Occult spells
- **Enchantment**: Specialization in mind control (Charm, Dominate, Mass Suggestion)
- **Illusion**: Specialization in deception (Invisibility, Mirror Image, Phantasmal Killer)
- **Shadowcraft**: Specialization in shadow magic (Shadow Bolt, Shadow Form, Umbral Strike)

**Primal Tradition (WIS-based)**:
- **Primal**: Core tradition unlocking all Primal spells
- **Elementalism**: Specialization in elemental forces (Fireball, Lightning Bolt, Ice Storm)
- **Beast Mastery**: Specialization in animal magic (Summon Beast, Animal Form, Pack Tactics)
- **Verdancy**: Specialization in plant/nature magic (Entangle, Barkskin, Thorn Whip)

### Profession Skills (12 Skills)
Crafting, gathering, and utility skills for economic gameplay:

**Crafting**:
- **Blacksmithing**: Forging weapons and metal armor (+item quality, +repair effectiveness)
- **Leatherworking**: Creating leather armor and goods (+armor stats, +durability)
- **Tailoring**: Sewing cloth armor and garments (+armor stats, +enchantability)
- **Woodworking**: Crafting bows, arrows, staves (+weapon stats, +ammo quantity)
- **Jewelcrafting**: Creating rings, amulets, gems (+socketing, +stat bonuses)
- **Alchemy**: Brewing potions and elixirs (+potion strength, +duration)
- **Enchanting**: Imbuing items with magic (+enchantment power, +slot count)
- **Runecrafting**: Carving magical runes (+rune effects, +combination options)

**Gathering**:
- **Mining**: Extracting ore and gems (+yield, +rare find chance)
- **Herbalism**: Harvesting plants and reagents (+yield, +rare herb chance)

**Utility**:
- **Cooking**: Preparing food buffs (+buff strength, +duration)
- **Fishing**: Catching fish for food/materials (+catch rate, +rare fish chance)

## Skill Mechanics

### Ranking System
- **Initial Rank**: Skills start at Rank 0 (untrained)
- **Max Rank**: Cap at Rank 100 (master level)
- **Rank-Up Cost**: XP required increases each rank (simple scaling initially)
  - Early ranks: Fast progression (1-20 ranks quickly)
  - Mid ranks: Moderate progression (20-60 steady growth)
  - High ranks: Slow progression (60-100 significant investment)

### Experience Gain
- **Use-Based**: Skills gain XP from relevant actions defined in skill traits
  - **Combat skills**: Successful attacks, blocks, damage dealt, kills
  - **Magic skills**: Spell casting, successful spell effects, spell damage/healing
  - **Attribute skills**: Specific actions (Athletics from sprinting/jumping, Stealth from sneaking, etc.)
  - **Profession skills**: Crafting items, gathering materials, successful skill checks
- **XP Actions**: Each skill defines specific actions granting XP (stored in `xpActions` trait)
  - Example: Athletics gains 2 XP per sprint, 5 XP per long jump, 15 XP per physical challenge
  - Example: Blacksmithing gains 10 XP per weapon forged, 15 XP per armor piece, 25 XP per masterwork
- **Scaling**: Higher-level challenges grant more skill XP
- **Natural Progression**: No grinding required, skills improve through normal gameplay

### Rank Effects
- **Per-Rank Modifiers**: Each skill has specific bonuses per rank (defined in `effects` trait)
  - **Light Blades Rank 1-100**: +0.5% damage, +0.2% critical chance per rank
  - **Arcane Rank 1-100**: +0.4% spell damage, +0.3% spell critical chance per rank
  - **Restoration Rank 1-100**: +0.6% healing effectiveness per rank
  - **Shield Rank 1-100**: +0.3% block chance, +0.2% damage reduction per rank
  - **Alchemy Rank 1-100**: +1% potion effectiveness per rank
  - **Athletics Rank 1-100**: +0.2% movement speed, +1% jump height per rank
- **Cumulative Bonuses**: Effects stack with attributes, abilities, equipment, and spells
- **Governing Attribute Synergy**: Skill checks use d20 + skill rank + attribute modifier
- **Multiple Effect Types**: Skills can grant various bonuses (damage, defense, movement, costs, etc.)

### Cost Scaling
- **Simple Formula** (initial implementation): `cost = base_cost * (1 + rank * cost_multiplier)`
  - Example: Rank 1→2 costs 100 XP, Rank 50→51 costs 600 XP, Rank 99→100 costs 1,000 XP
- **Prevents Instant Mastery**: High ranks require significant time investment
- **Rewards Specialization**: Easier to master a few skills than max everything

## Skill Integration

### Combat Integration
- **Damage Calculation**: Weapon skills multiply base damage
- **Success Rates**: Armor skills improve defense, dodge, block
- **Efficiency**: Skills reduce stamina/mana costs (future)

### Magic Integration
- **Spell Power**: Magic skills increase spell damage/healing
- **Mana Efficiency**: Higher ranks reduce mana costs
- **Spell Success**: Low skill = chance to fizzle/fail

### Crafting Integration
- **Quality**: Crafting skills improve item stats
- **Success Rates**: Higher rank = less chance of failure
- **Unlock Recipes**: Advanced crafting requires skill thresholds

### Exploration Integration
- **Lockpicking**: Higher rank = better chance on difficult locks
- **Sneaking**: Higher rank = harder to detect
- **Speech**: Better prices, more dialogue options

## Skill Display

### Character Sheet
- **Skill List**: All skills with current rank (0-100)
- **Progress Bars**: Visual XP progress to next rank
- **Bonuses Shown**: Current effects (+25% damage, +15% block, etc.)
- **Sorting Options**: By category, by rank, by progress

### Rank-Up Notifications
- **In-Game Feedback**: Alert when skill ranks up
- **Bonus Display**: Show new rank and improved bonuses
- **Milestone Celebrations**: Special notifications at major ranks (25, 50, 75, 100)

## Design Goals

### Simplicity First
- **Straightforward Progression**: Use skill → gain XP → rank up → get stronger
- **No Complex Trees**: Avoid skill tree complexity initially
- **Clear Benefits**: Obvious what each rank does

### Future Expansion
System designed to support later additions:
- **Perk Trees**: Unlock special abilities at milestones
- **Skill Synergies**: Bonuses when multiple skills reach thresholds
- **Training**: Pay trainers to boost skill XP gain
- **Skill Books**: Find/buy books granting instant skill XP
- **Specialization Bonuses**: Extra benefits for mastering skill categories

## Related Systems

- [Character System](character-system.md) - Attributes complement skills
- [Progression System](progression-system.md) - Leveling and XP sources
- [Abilities System](abilities-system.md) - Active powers unlocked by skills
- [Combat System](combat-system.md) - Skills affect combat calculations
- [Crafting System](crafting-system.md) - Crafting skills enable item creation
