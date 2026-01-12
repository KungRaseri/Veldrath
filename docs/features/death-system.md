# Death System

**Status**: ✅ 100% Complete (See [COMPLETED_WORK.md](../COMPLETED_WORK.md))

## Overview

The Death System handles character defeat with consequences varying by difficulty mode, including standard death, permadeath, and legacy tracking.

## Core Components

### Standard Death (Casual, Normal, Hard)
- **Death Sequence**: Statistics display and final moments
- **Respawn**: Character restored with full health/mana
- **Penalties**: Gold loss, item loss scaled by difficulty
- **Continuation**: Resume from death location

### Permadeath (Nightmare, Apocalypse)
- **Permanent Deletion**: Character permanently removed on death
- **No Respawn**: Forces new character creation
- **High Stakes**: Adds tension to hardcore gameplay

### Hall of Fame
Leaderboard tracking memorable characters:
- Character identity and progression
- Gameplay statistics and accomplishments
- Cause of death and final moments
- Difficulty mode and playtime
- Ranking of top characters

### Legacy System
- Deceased characters preserved in Hall of Fame
- Items potentially left for future characters
- Achievements and records preserved

## Key Features

- **Difficulty-Scaled**: Consequences match chosen challenge level
- **Permanent Records**: Hall of Fame immortalizes characters
- **Risk Management**: Players balance risk with difficulty rewards
- **Narrative Closure**: Death provides statistics and reflection

## Related Systems

- [Difficulty System](difficulty-system.md) - Death penalties vary by mode
- [Save/Load System](save-load-system.md) - Character deletion on permadeath
- [New Game+ System](new-game-plus-system.md) - Hall of Fame persists
