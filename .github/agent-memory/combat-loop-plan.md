# Plan: Combat Loop + Multiplayer Architecture (2026-03-30)

> **Status**: Reviewed and current as of Session-39 (2026-06-27).

## ⚠️ IMPORTANT — ACTUAL IMPLEMENTATION NOTE

**The tick-based action-queue architecture below was the original design but was NOT built.**
A simpler **free-for-all turn-based** system was implemented instead (see "ACTUAL IMPLEMENTATION" section below).
When resuming, read the ACTUAL IMPLEMENTATION section first — it supersedes all Phase 3–6 detail below.

---

## Progress Status (Original Tick-Based Plan)
- ✅ Phase 0a — ICombatSettings extraction (complete)
- ✅ Phase 0b — Server schema migrations: DifficultyMode, RescueFundTotal, GlobalStat (complete)
- ✅ Phase 0c — ZoneLocation ActorPool jsonb column + Content migration (complete)
- ⬜ Phase 1 — Character Creation + Zone Session (not started)
- ⬜ Phase 2 — Core Engine Cleanup (not started)
- ✅ Phase 3-6 superseded by ACTUAL IMPLEMENTATION (see below)
- ⬜ Phase 7/8 — Tests (pending — see ACTUAL IMPLEMENTATION section)

**Test baseline at Phase 0 completion**: Engine 2,866 + Server 468 = **3,334 all passing**

---

## ACTUAL IMPLEMENTATION — Free-For-All Turn-Based Combat (2026-03-30)

Built as a simplified system where any player can engage any live enemy; each hub call = one combat turn.
All server code lives in `Veldrath.Server/Features/Characters/Combat/`.

### Server Build Status: ✅ CLEAN (0 warnings, 0 errors)
### Client Build Status: ✅ CLEAN (0 warnings, 0 errors)

### New Server Infrastructure Files

#### `SpawnedEnemy.cs`
Shared mutable live enemy instance in zone/location store.
- Key properties: `Id Guid`, `Name string`, `Level int`, `CurrentHealth int` (mutable), `MaxHealth int`, `BaseXp int`, `GoldReward int`, `ArchetypeSlug string`, `IsAlive => CurrentHealth > 0`
- Multiplayer tracking: `Participants HashSet<Guid>`, `DamageContributions Dictionary<Guid,int>`, `WasRewarded bool`
- Thread safety: `SyncRoot object` property — **named `SyncRoot` not `Lock`** (avoids conflict with `System.Threading.Lock` in .NET 9+)
- Full enemy data: `Template Enemy` (the underlying engine Enemy model)

#### `ZoneLocationEnemyStore.cs`
Static store: `ConcurrentDictionary<string, List<SpawnedEnemy>>` keyed by `"{zoneGroup}/{locationSlug}"`.
- **`TryGetEnemy(string key, Guid enemyId)` → `SpawnedEnemy?`** (nullable return, NOT bool+out)
- `MakeKey(zoneGroup, locationSlug)` → `"{zoneGroup}/{locationSlug}"`
- Other: `HasRoster`, `GetOrEmpty`, `GetAlive`, `TryAddRoster`, `AddEnemy`, `RemoveEnemy`

#### `ActiveCombatSession.cs`
Per-player combat state: `record(string ZoneGroup, string LocationSlug, Guid EnemyId, bool IsPlayerDefending, int TurnCount, DateTimeOffset StartedAt)`
- Helpers: `WithDefending(bool)`, `NextTurn()`

#### `CombatSessionStore.cs`
Static store: `ConcurrentDictionary<Guid, ActiveCombatSession>` keyed by `CharacterId`.
- `TryGet`, `Set`, `Remove`, `RemoveByEnemyId(Guid) → IReadOnlyList<Guid>` (returns affected CharacterIds), `IsInCombat`

#### `CombatCharacterHydrator.cs`
`static Hydrate(ServerCharacter entity, Dictionary<string,int> attrs)` → engine `Character`
- Populates HP, mana, 6 core stats, `AbilityCooldowns` (strips `"AbilityCooldown_"` prefix), `SpellCooldowns` (strips `"SpellCooldown_"` prefix)
- **`ServerCharacter` alias**: `CombatHelpers.cs` needs `using ServerCharacter = Veldrath.Server.Data.Entities.Character;` to avoid ambiguity with engine's `Character` model

#### `ActorPoolResolver.cs`
Injectable (Scoped). Depends on `EnemyGenerator`.
- `SpawnRosterAsync(IReadOnlyList<ActorPoolEntry>?)` → 2–4 enemies via weighted random selection from the pool
- Registered in `Program.cs`: `builder.Services.AddScoped<ActorPoolResolver>();`

#### `CombatHelpers.cs`
Static helpers shared by all hub command handlers.
- `ParseAttrs` / `SerializeAttrs` — read/write character attributes blob
- `DecrementCooldowns` — ticks all `AbilityCooldown_*` and `SpellCooldown_*` keys down by 1
- `CalculatePlayerDamage(attrs, level)` → `max(1, (Str-10)/2 + Level/2 + 5)`
- `CalculateEnemyDamage(enemy, attrs, isDefending)` — CON-based defense; damage halved when defending
- `RollEnemyAbility(enemy, player)` — 30% chance; **`Power.BaseDamage` is `string?`** → uses `int.TryParse(ability.BaseDamage, out var d)`
- `DistributeRewardsAsync` — locks `enemy.SyncRoot`, distributes proportional XP/gold based on `DamageContributions`; updates `character.Experience` (long entity field, NOT attrs blob) and `attrs["Gold"]`
- `ScheduleRespawn` — fire-and-forget `Task.Delay(60s)` → creates scope, calls `ActorPoolResolver.SpawnRosterAsync`, adds to store, broadcasts `EnemySpawned` to zone group
- `HandleDeathIfNeededAsync` — HC: `entity.DeletedAt = DateTimeOffset.UtcNow`; Normal: `entity.Experience -= xpPenalty` (long), 10% gold penalty, `CurrentHealth = 1`

### New Server Hub Command Files

#### `EngageEnemyHubCommand.cs`
`record EngageEnemyHubCommand(Guid CharacterId, string ZoneGroup, string LocationSlug, Guid EnemyId)`
- Guard: `IsInCombat` → `TryGetEnemy` (nullable pattern) → `!IsAlive` → `lock(enemy.SyncRoot)` add to Participants → `CombatSessionStore.Set`
- Returns: enemy stats + `Template.Abilities` list for client display

#### `AttackEnemyHubCommand.cs`
Guard: has session + enemy exists (nullable) + alive → load entity+attrs → hydrate → calc damage → `lock(enemy.SyncRoot)` apply → if dead: `DistributeRewardsAsync` + `ScheduleRespawn` + early return with victory result → counter-attack via `RollEnemyAbility` → persist → `HandleDeathIfNeededAsync`

#### `DefendActionHubCommand.cs`
Same flow as Attack but `session.WithDefending(true)`, no player attack, enemy damage halved.

#### `FleeFromCombatHubCommand.cs`
50% flee success. On success: `lock(enemy.SyncRoot)` removes from `Participants`/`DamageContributions`, `CombatSessionStore.Remove`. On fail: enemy counter-attacks.

#### `UseAbilityInCombatHubCommand.cs`
Checks `attrs[$"AbilityCooldown_{AbilityId}"]` for cooldown; deducts 10 mana; heal abilities (+20 HP) or damage (Int-based); `lock(enemy.SyncRoot)` for damage; sets `CooldownTurns = 3`.

#### `RespawnHubCommand.cs`
Guards: entity exists → if `DeletedAt.HasValue` → error (HC can't respawn) → if `currentHp > 0` → error. Restores 25% MaxHP + full mana; `CombatSessionStore.Remove`.

### Modified Server Files

#### `NavigateToLocationHubCommand.cs`
- Added `string ZoneGroup = ""` (4th param with default)
- Added `SpawnedEnemySummary(Guid Id, string Name, int Level, int CurrentHealth, int MaxHealth)` record
- Added `SpawnedEnemies IReadOnlyList<SpawnedEnemySummary>` to result
- Handler: injects `ActorPoolResolver`; if `ZoneLocationEnemyStore.HasRoster` is false → `SpawnRosterAsync` → `TryAddRoster`; projects using `e.Name`, `e.Level` (not `e.Template.Name/Level`)

#### `GameHub.cs`
- `NavigateToLocation`: extracts `zoneGroupForNav` via `TryGetCurrentZoneGroup()`; passes as 4th arg to command; adds `SpawnedEnemies` to `LocationEntered` payload
- Added `using Veldrath.Server.Features.Characters.Combat;`
- Added 6 new hub methods: `EngageEnemy(EngageEnemyHubRequest)`, `AttackEnemy()`, `DefendAction()`, `FleeFromCombat()`, `UseAbilityInCombat(UseAbilityInCombatHubRequest)`, `Respawn()`
- Hub broadcast events: `CombatStarted` (caller), `EnemyEngaged` (OthersInGroup), `CombatTurn` (caller), `EnemyDefeated` (OthersInGroup), `CombatEnded` (caller), `CharacterRespawned` (caller)
- Hub request DTOs (at bottom of GameHub.cs in `Veldrath.Server.Hubs` namespace): `EngageEnemyHubRequest(string LocationSlug, Guid EnemyId)`, `UseAbilityInCombatHubRequest(string AbilityId)`

#### `Program.cs`
- Added `builder.Services.AddScoped<Veldrath.Server.Features.Characters.Combat.ActorPoolResolver>();`

### New/Modified Client Files

#### `GameViewModel.cs`
- 8 combat state backing fields + public properties: `IsInCombat`, `IsPlayerDead`, `IsHardcoreDeath`, `CombatEnemyId`, `CombatEnemyName`, `CombatEnemyLevel`, `CombatEnemyCurrentHealth`, `CombatEnemyMaxHealth`
- `ObservableCollection<string> EnemyAbilityNames`
- `ObservableCollection<SpawnedEnemyItemViewModel> SpawnedEnemies`
- 6 `ReactiveCommand` declarations + constructor initializations: `EngageEnemyCommand<Guid>`, `AttackEnemyCommand`, `DefendActionCommand`, `FleeFromCombatCommand`, `UseAbilityInCombatCommand<string>` (abilityId), `RespawnCommand`
- Hub event handlers: `OnCombatStarted`, `OnCombatTurn`, `OnCombatEnded`, `OnEnemyDefeated(Guid)`, `OnEnemyEngaged(Guid, Guid, string)`, `OnEnemySpawned(Guid, string, int, int, int)`, `OnCharacterRespawned(int, int)`
- Updated `OnLocationEntered(string, string, string, IReadOnlyList<SpawnedEnemyItemViewModel>? = null)` — populates `SpawnedEnemies`
- Private `DoXxx` methods for each command (try/catch → `AppendLog` on error)
- `SpawnedEnemyItemViewModel : ReactiveObject` class at end of file (Id, Name, Level, MaxHealth, CurrentHealth with `RaisePropertyChanged(IsAlive)`)

#### `CharacterSelectViewModel.cs`
- 7 new subscription fields: `_combatStartedSub`, `_combatTurnSub`, `_combatEndedSub`, `_enemyDefeatedSub`, `_enemyEngagedSub`, `_enemySpawnedSub`, `_characterRespawnedSub`
- Disposal for all 7 in the dispose block
- 7 new event subscriptions (mapping payloads to `GameViewModel` handler methods)
- Updated `LocationEnteredPayload` → now has `IReadOnlyList<SpawnedEnemyEntry> SpawnedEnemies`; maps to `SpawnedEnemyItemViewModel` in the `LocationEntered` callback
- New payload records: `SpawnedEnemyEntry`, `CombatStartedPayload`, `CombatTurnPayload`, `CombatEndedPayload`, `EnemyDefeatedPayload(Guid CharacterId)`, `EnemyEngagedPayload(Guid CharacterId, Guid EnemyId, string EnemyName)`, `EnemySpawnedPayload`, `CharacterRespawnedPayload`

### Critical Implementation Notes

- **`Power.BaseDamage` is `string?`** — always use `int.TryParse(ability.BaseDamage, out var d)` before arithmetic
- **`entity.Experience` is `long`** — XP lives on the EF entity, NOT in the attrs blob; Gold IS in `attrs["Gold"]` (int)
- **`entity.DeletedAt`** is `DateTimeOffset?` — set for HC death (not `IsDeleted`)
- **`SpawnedEnemy.SyncRoot`** — renamed from `Lock` to avoid .NET 9 `System.Threading.Lock` conflict
- **`TryGetEnemy` returns `SpawnedEnemy?`** — nullable, NOT bool+out; callers pattern: `var enemy = store.TryGetEnemy(key, id); if (enemy is null) return error;`
- **`SpawnedEnemySummary.Name/Level`** come from `SpawnedEnemy` direct properties, NOT `e.Template.Name/Level`

### Remaining Work

| Phase | Task | Status |
|---|---|---|
| 8 | Tests — server combat handler unit tests | ✅ |
| 8 | Tests — client GameViewModel combat command tests | ✅ |
| 9 | `GameView.axaml` — combat panel UI (enemy roster, action buttons, death/respawn overlay) | ✅ |
| Final | `dotnet test Veldrath.slnx --filter "Category!=UI"` | ✅ 525+491=1016 |

#### Phase 8 — Test Priorities
- `EngageEnemyHubCommandHandlerTests`: guard (already in combat), enemy not found, enemy dead, success
- `AttackEnemyHubCommandHandlerTests`: no session guard, no enemy guard, enemy killed → rewards distributed, player killed → death handling
- `DefendActionHubCommandHandlerTests`: damage reduction when defending
- `FleeFromCombatHubCommandHandlerTests`: 50% flee logic (seed RNG for determinism)
- `UseAbilityInCombatHubCommandHandlerTests`: cooldown guard, mana guard, heal vs damage path
- `RespawnHubCommandHandlerTests`: HC guard, alive guard, HP/mana restore, session cleared
- `GameViewModelCombatTests`: `OnCombatStarted` sets state, `OnCombatTurn` updates HP + sets `IsPlayerDead`, `OnEnemySpawned` adds to collection

#### Phase 9 — GameView.axaml Combat Panel
Add below zone-location content:
- Enemy roster `ItemsControl` bound to `SpawnedEnemies` — each item shows Name, Level, HP bar, Engage button (binds `EngageEnemyCommand` with enemy Id)
- Combat HUD (visible when `IsInCombat`): enemy name+level+HP bar; Attack / Defend / Flee buttons; ability list (ItemsControl over `EnemyAbilityNames` with `UseAbilityInCombatCommand`)
- Death overlay (visible when `IsPlayerDead && !IsHardcoreDeath`): "You have been defeated" + Respawn button
- HC death overlay (visible when `IsHardcoreDeath`): "Your character has been permanently lost" + Return to menu button

---

## Final Decisions

- Combat model: **lazy-tick, action-queue**. No background service; ticks are evaluated on demand when any hub method fires (QueueAction or PollCombat). Multiple missed enemy ticks are consumed in one pass via `while` loop.
- Action queuing: `QueueAction(encounterId, actionType)` — always succeeds immediately. Queuing while cooldown is active replaces the previous queued action (last-write-wins, OSRS model). Action fires automatically during next `EvaluateCombatTicks` call once `NextAttackAvailableAt <= now`.
- Attack speed: derived from equipped weapon's `attackSpeed` trait (float, attacks/sec; e.g. 1.0 = 1000ms cooldown, 0.8 = 1250ms). Fallback: 1000ms. Enemy has fixed `EnemyAttackIntervalMs` set at encounter creation from enemy stats.
- Defend: **instant, no cooldown cost**. Sets `IsDefending = true` for the duration of the participant's current attack interval, clears after enemy evaluates that tick. Also bumps ThreatScore +50.
- Group join: starts per-player, others join during 10s window; enemy HP scales +50% per extra participant.
- Threat: damage dealt = +ThreatScore; Defend = +50 flat; initial attacker seeded ThreatScore=1; enemy targets highest ThreatScore; ties broken by CharacterId (deterministic).
- Flee: DEFERRED.
- Death penalty: Level*15 gold; Normal = gold deducted + respawn; Hardcore = soft-delete.
- Towns/Tutorial = safe zones.
- Difficulty modes: Normal + Hardcore only, permanent at character creation, [HC] badge.
- Zone instancing: Town = `{zoneId}`, Wilderness = `{zoneId}_{difficultyMode}`, Dungeon = per-instance.
- ZoneLocation: `ActorPool` JSON column (list of {ArchetypeSlug, Weight}); procedural fallback if empty.
- ICombatSettings extracted from ISaveGameService; DifficultySettings implements it.
- Rescue fund: per-zone (RescueFundTotal on Zone) + global (GlobalStat table).
- Scope: Engine (Core/Shared/Data) + Veldrath (Server/Client/Contracts) ONLY.

---

## Phase 0 — Schema & Engine Foundation ✅ COMPLETE

### 0a. Extract ICombatSettings (RealmEngine.Shared + Core) ✅
- New `ICombatSettings` in `RealmEngine.Shared/Abstractions/`: PlayerDamageMultiplier, EnemyDamageMultiplier, EnemyHealthMultiplier, GoldXPMultiplier, IsPermadeath
- `DifficultySettings` implements `ICombatSettings`
- Add `NormalCombatSettings` + `HardcoreCombatSettings` static impls in Shared/Models/
- Refactor `CombatService` primary ctor: `(ICombatSettings, IMediator, PowerDataService, ILogger, ILoggerFactory, ItemGenerator? = null)`
- Keep adapter ctor `(ISaveGameService, ...)` for single-player path
- Update `AttackEnemyHandler` to read multipliers from `ICombatSettings`

### 0b. Server migrations (3 migrations in Application/) ✅
- `Character`: add `DifficultyMode string` (default "Normal") → `20260330000001_AddCharacterDifficultyMode`
- `Zone`: add `RescueFundTotal long` (default 0) → `20260330000002_AddZoneRescueFund`
- New `GlobalStat` entity: `Key string PK, Value long` → `20260330000003_AddGlobalStats`; registered in ApplicationDbContext

### 0c. ZoneLocation ActorPool (Content migration in RealmEngine.Data) ✅
- `ActorPool jsonb` column (default `'[]'`) on ZoneLocation entity → `20260330000004_AddZoneLocationActorPool`
- `ActorPoolEntry` embedded type: `string ArchetypeSlug, int Weight`
- Exposed as `IReadOnlyList<ActorPoolEntry>? ActorPool` on `ZoneLocationEntry`
- Updated `EfCoreZoneLocationRepository.MapToModel` + 3 new ActorPool tests

---

