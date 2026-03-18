# Save/Load System

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Save/Load System preserves complete game state with auto-save, manual save, and support for multiple characters.

## Core Components

### Save Features
- **Auto-Save**: Automatic saves after significant events (combat, quests, location changes)
- **Manual Save**: Player-initiated saves outside combat
- **Multiple Save Slots**: Unlimited slots for multiple characters/playthroughs
- **Save Data Scope**: Complete game state preservation

### Save Data Includes
- Character attributes, level, progression
- Full inventory and equipped items
- Quest progress and completion status
- Achievement unlocks and statistics
- Discovered locations and world state
- Difficulty settings and mode flags
- Gameplay statistics and playtime

### Load Features
- **Character Selection**: Load any saved character
- **Save Information**: Display character details before loading
- **Persistence Validation**: Verify save data integrity

## Key Features

- **Progress Protection**: Auto-save minimizes progress loss
- **Multiple Characters**: Support for diverse playthroughs
- **Complete State**: All game aspects preserved
- **Reliable Persistence**: EF Core (PostgreSQL via Npgsql) in production; in-memory repository in tests/local development

## Related Systems

- [Character System](character-system.md) - Character state saved
- [Quest System](quest-system.md) - Quest progress preserved
- [Exploration System](exploration-system.md) - Location discovery saved
