# Quest System

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Quest System provides structured objectives for players through main story quests, side quests, and procedurally generated content with varied objective types and rewards.

## Core Components

### Quest Categories
- **Main Quest Chain**: Linear story quests (6 quests) culminating in final confrontation
- **Side Quests**: Optional procedurally generated quests
- **Daily/Repeatable**: Refresh daily for consistent engagement *(Future)*

### Quest Types
- **Investigation**: Gather clues across multiple locations
- **Fetch**: Retrieve specific items
- **Kill**: Eliminate enemy types
- **Escort**: Protect NPCs traveling between locations
- **Delivery**: Transport items/messages with time limits

### Quest Structure
- **Prerequisites**: Level, previous quests, reputation, owned items
- **Objective Tracking**: Primary, secondary, and hidden objectives
- **Quest States**: Available, Active, Completed, Failed
- **Difficulty Tiers**: Easy to Epic/Legendary with scaled rewards

### Reward System
- **Gold Rewards**: 9 tiers scaling with player level
- **Experience Rewards**: Level and difficulty scaling
- **Item Rewards**: Equipment and consumables matching difficulty
- **Special Rewards**: Time bonuses (Apocalypse), achievements, reputation

### Quest Integration
- **Quest Givers**: NPCs in towns matching quest types to backgrounds
- **Location Integration**: Objectives reference specific location types
- **Combat Integration**: Kill objectives track enemy types
- **Inventory Integration**: Fetch/delivery quests use inventory

### Quest UI
- **Quest Journal**: View active quests with progress *(Future)*
- **Quest Markers**: Show relevant locations *(Future)*
- **Completion Notifications**: Real-time rewards display

## Key Features

- **Narrative Progression**: Main quest chain drives story
- **Procedural Variety**: Generated side quests for replayability
- **Diverse Objectives**: Multiple quest types prevent repetition
- **Meaningful Rewards**: Progression and loot scaled to difficulty

## Related Systems

- [Exploration System](exploration-system.md) - Quest objectives tied to locations
- [Combat System](combat-system.md) - Kill quests and combat encounters
- [Progression System](progression-system.md) - XP rewards
