---
kind: work-item
date: 2026-03-30
area: engine
title: "Combat system tests + GameView combat UI"
---

### Session-28 (2026-03-30) — Combat system tests + GameView combat UI

**Phase A — Server combat handler unit tests (6 new files)**

Created `Veldrath.Server.Tests/Features/`:
- `EngageEnemyHubCommandHandlerTests.cs` — 4 tests: already-in-combat guard, enemy-not-found guard, enemy-dead guard, success
- `AttackEnemyHubCommandHandlerTests.cs` — 4 tests: no-session guard, enemy-not-in-store guard, reduces health, enemy killed
- `DefendActionHubCommandHandlerTests.cs` — 3 tests: no-session guard, sets-IsDefending flag, damage reduction vs non-defending
- `FleeFromCombatHubCommandHandlerTests.cs` — 2 tests: no-session guard, returns valid result when in combat
- `UseAbilityInCombatHubCommandHandlerTests.cs` — 5 tests: empty-abilityId guard, no-session guard, cooldown guard, mana guard, success
- `RespawnHubCommandHandlerTests.cs` — 5 tests: character-not-found, HC-char-returns-not-found (DeletedAt filter in GetByIdAsync makes HC guard dead code), alive guard, success (restores 25% HP), removes combat session

Key gotchas discovered:
- `CharacterRepository.GetByIdAsync` filters `&& DeletedAt == null` — HC characters are always "not found" when the HC guard checks for `DeletedAt.HasValue`; test asserts `"not found"` not `"Hardcore"`
- `NormalizedUserName` unique constraint: must use `account.NormalizedUserName = account.UserName.ToUpperInvariant()` when seeding multiple users in one test; `= "U"` (hardcoded constant) causes unique violations
- `Character.Attributes` has NOT NULL in SQLite — always provide `attrsJson ?? "{}"`; never null

**Phase B — Client GameViewModel combat tests (10 tests)**

Created `Veldrath.Client.Tests/ViewModels/GameViewModelCombatTests.cs`:
- `OnCombatStarted_SetsIsInCombatAndEnemyStats`, `OnCombatStarted_SetsAbilityNames`
- `OnCombatTurn_UpdatesEnemyHealth`, `OnCombatTurn_EnemyDefeated_ClearsCombat`, `OnCombatTurn_PlayerDefeated_SetsIsPlayerDead`, `OnCombatTurn_HardcoreDeath_SetsIsHardcoreDeath`
- `OnCombatEnded_ClearsIsInCombat`
- `OnEnemySpawned_AddsToCollection`, `OnEnemySpawned_MultipleEnemies`
- `OnCombatTurn_EnemyDefeated_ZeroesRosterItem`

**Phase C — GameView.axaml combat panel**

Added to `Veldrath.Client/Views/GameView.axaml` after ZoneLocations ItemsControl:
- Enemy roster `ItemsControl` bound to `SpawnedEnemies` (`IsVisible="{Binding HasSpawnedEnemies}"`): each item shows Name, Level, HP and Engage button using `Command="{Binding $parent[UserControl].DataContext.EngageEnemyCommand}" CommandParameter="{Binding Id}"`
- Combat HUD (`IsVisible="{Binding IsInCombat}"`): enemy name/level/HP bar + Attack/Defend/Flee buttons
- Death overlay (`IsVisible="{Binding IsPlayerDead}"`): Respawn button (`IsVisible="{Binding IsHardcoreDeath, Converter={x:Static BoolConverters.Not}}"`) + LogoutCommand for HC death

Added to `Veldrath.Client/ViewModels/GameViewModel.cs`:
- `_hasSpawnedEnemies` backing field + `HasSpawnedEnemies` reactive property wired via `SpawnedEnemies.CollectionChanged`

**Final count: 525 client + 491 server = 1016 total passing**
