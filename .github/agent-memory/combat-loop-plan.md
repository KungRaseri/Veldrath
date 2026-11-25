# Plan: Combat Loop + Multiplayer Architecture (2026-03-30)

> **Status**: the tick-based action-queue design below was never built (see the note under it). The ACTUAL
> IMPLEMENTATION section records what ships; it has been amended for the D2 world-state move (2026-10-25) and the
> session-68 deletion of the Avalonia client.

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

> **The flow below is the one that ships, but two of the files it lists are gone.** Live enemy and combat-session
> state has been database-backed since the D2 world-state move (2026-10-25, `plans/server-first-world-state.md` §2):
> `EfCoreEnemyInstanceStore` over the `EnemyInstances` table replaced `ZoneLocationEnemyStore`, and
> `EfCoreCombatSessionStore` over `CombatSessions` replaced `CombatSessionStore`. `SpawnedEnemy.cs`,
> `ActiveCombatSession.cs` and `CombatCharacterHydrator.cs` are still in the tree, and the hub methods and
> `…HubCommand` handlers below still exist. "Client Build Status" is history as well: the Avalonia client was
> deleted in session-68 (2026-10-25) and the web build is `cd web && npm run build`.

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
Injectable (Scoped). Depends on `EnemyGenerator` (required); `ILogger<ActorPoolResolver>`, `IRandomSource` (defaults to `SystemRandomSource.Shared`) and `IActorInstanceRepository` are optional.
- `SpawnRosterAsync(IReadOnlyList<ActorPoolEntry>? pool, IReadOnlyList<PlacedActorInstance>? placements = null)` → every **hostile** placement once, then weighted pool draws filling out to a rolled **2–4** (a weight `<= 0` counts as 1). The roll is a fill target, not a cap: five hostile placements give five actors, placements alone are never topped up, and neither pool nor placements means an empty roster (a safe town) rather than a default archetype
- Hostile filter: a placement or a pool entry whose archetype is explicitly non-hostile is logged and skipped, never turned into a target and never replaced (a missing `hostile` trait still counts as hostile)
- Registered in `Program.cs`: `builder.Services.AddScoped<ActorPoolResolver>();`

#### `CombatHelpers.cs`
Static helpers shared by all hub command handlers.
- `ParseAttrs` / `SerializeAttrs` — read/write character attributes blob
- `DecrementCooldowns` — ticks all `AbilityCooldown_*` and `SpellCooldown_*` keys down by 1
- `CalculatePlayerDamage(attrs, level)` → `max(1, (Str-10)/2 + Level/2 + 5)`
- `CalculateEnemyDamage(enemy, attrs, isDefending)` — CON-based defense; damage halved when defending
- `RollEnemyAbility(enemy, player)` — 30% chance; **`Power.BaseDamage` is `string?`** → uses `int.TryParse(ability.BaseDamage, out var d)`
- `DistributeRewardsAsync` — locks `enemy.SyncRoot`, distributes proportional XP/gold based on `DamageContributions`; updates `character.Experience` (long entity field, NOT attrs blob) and `attrs["Gold"]`
- ~~`ScheduleRespawn`~~ — **deleted with the world-state move (D2, 2026-10-25); this class respawns nothing.** See "Enemy respawn — what ships" below.
- `HandleDeathIfNeededAsync` — HC: `entity.DeletedAt = DateTimeOffset.UtcNow`; Normal: `entity.Experience -= xpPenalty` (long), 10% gold penalty, `CurrentHealth = 1`

### Enemy respawn — what ships (no timer)

The `ScheduleRespawn` fire-and-forget timer is gone; the pending respawn is a column, so a restart mid-countdown
continues it instead of losing it.

- **Death is one conditional UPDATE.** `IEnemyInstanceStore.TryMarkDeadAsync(enemyId)` runs
  `UPDATE EnemyInstances SET IsAlive = false, RespawnAt = now + <respawn delay>, Version = Version + 1
  WHERE Id = … AND IsAlive`, and returns true to exactly the one caller that flipped the row — the flag
  `AttackEnemyHubCommand` uses to decide who distributes rewards. The delay is `EnemyWorldOptions.RespawnDelay`,
  bound from the `Enemies` section (`RespawnDelaySeconds`, default 60), not a hardcoded 60 s.
- **A background sweeper brings them back.** `EnemyRespawnService : BackgroundService`
  (`Veldrath.Server/Features/Characters/Combat/EnemyRespawnService.cs`, registered
  `builder.Services.AddHostedService<EnemyRespawnService>()` at `Veldrath.Server/Program.cs:197`) ticks on a
  `PeriodicTimer` every `Enemies:SweepIntervalSeconds` (default 5). Each tick calls
  `SweepExpiredRespawnsAsync(RespawnBatchSize, ct)` — on PostgreSQL the batch is claimed with
  `FOR UPDATE SKIP LOCKED`, so several instances never revive the same row — which sets `IsAlive = true`,
  `CurrentHealth = MaxHealth`, `RespawnAt = null` and deletes the row's `EnemyParticipants`. After the commit the
  service broadcasts `EnemySpawned` with `{ Id, Name, Level, CurrentHealth, MaxHealth }` to `enemy.ZoneGroup`, so a
  missed broadcast is a cosmetic delay, not lost state. The same tick prunes expired combat sessions and sweeps
  zone-instance runs.
- **Reads self-heal too.** `GetAliveOrSpawnAsync` calls `SettleLocationAsync` before loading, which marks
  zero-health-but-alive rows dead with a fresh `RespawnAt` and revives rows whose `RespawnAt` has passed — the lazy
  path that keeps a location correct without waiting for a tick.
- **A cleared room waits longer.** When `CountAliveAsync` reaches zero, `AttackEnemyHubCommand` calls
  `ExtendClearedRespawnsAsync`, which pushes each row's `RespawnAt` out to at least
  `now + ClearedRoomRespawnDelay` (`ClearedRoomRespawnMultiplier`, default 3 × the normal window).
- **No `SpawnRosterAsync` in the respawn path.** The revived row already carries its archetype, level and stats;
  `ActorPoolResolver` is used when a location has no rows yet, in `GetAliveOrSpawnAsync`'s caller-supplied
  `spawnRoster`. Recorded in
  [`work-items/2026-10-25-d2-the-live-world-moved-into-postgres-33.md`](work-items/2026-10-25-d2-the-live-world-moved-into-postgres-33.md)
  and `plans/server-first-world-state.md` §2.

### New Server Hub Command Files

#### `EngageEnemyHubCommand.cs`
`record EngageEnemyHubCommand(Guid CharacterId, string ZoneGroup, string LocationSlug, Guid EnemyId)`
- Guard: `IsInCombat` → `TryGetEnemy` (nullable pattern) → `!IsAlive` → `lock(enemy.SyncRoot)` add to Participants → `CombatSessionStore.Set`
- Returns: enemy stats + `Template.Abilities` list for client display

#### `AttackEnemyHubCommand.cs`
Guard: has session + enemy exists (nullable) + alive → load entity+attrs → hydrate → calc damage → `lock(enemy.SyncRoot)` apply → if dead: `TryMarkDeadAsync` (the conditional UPDATE that stamps `RespawnAt`; only the caller that won it calls `DistributeRewardsAsync`) → early return with victory result → counter-attack via `RollEnemyAbility` → persist → `HandleDeathIfNeededAsync`

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
- Hub broadcast events: `CombatStarted` (caller), `EnemyEngaged` (OthersInGroup), `CombatTurn` (caller), `EnemyDefeated` (OthersInGroup), `CombatEnded` (the caller; and, when a kill releases the combat sessions of everyone engaged with the same enemy row, each of those characters on their own connection, with reason `shared kill`), `CharacterRespawned` (caller)
- Hub request DTOs (at bottom of GameHub.cs in `Veldrath.Server.Hubs` namespace): `EngageEnemyHubRequest(string LocationSlug, Guid EnemyId)`, `UseAbilityInCombatHubRequest(string AbilityId)`

#### `Program.cs`
- Added `builder.Services.AddScoped<Veldrath.Server.Features.Characters.Combat.ActorPoolResolver>();`

### Client files — the SvelteKit shell, not a view model

> `GameViewModel.cs`, `CharacterSelectViewModel.cs` and `GameView.axaml`, which this section used to document,
> were deleted with the Avalonia client in session-68 (2026-10-25) — see
> [`work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md`](work-items/2026-10-25-desktop-client-deleted-veldrath-is-web-first-only.md).

#### `web/client/src/lib/game/session.svelte.ts`
- Combat state: `combat` (`active`, `enemy`, `log`, `participants`, `outcome`), the current location's `enemies` roster, and `dead` for the death banner
- `combat.enemy` carries `id`, `name`, `level`, `currentHealth`, `maxHealth`, `abilities`, `archetypeSlug`, `typeKey`, `tier`
- Commands: `engageEnemy(enemyId)` → `EngageEnemy` (sent with the current `locationSlug`), `attack()` → `AttackEnemy`, `defend()` → `DefendAction`, `flee()` → `FleeFromCombat`, `useCombatAbility(abilityId)` → `UseAbilityInCombat`, `respawn()` → `Respawn`
- Subscribers: `CombatStarted` (opens the panel and drops that enemy from `enemies`), `CombatParticipants` (the shared enemy-health and participant snapshot a party member's turn updates), `CombatTurn` (`applyCombatTurnToStats` + `describeCombatTurn`; on `playerDefeated` sets `dead` and applies the penalty, on `enemyDefeated` closes the fight and re-reads the bag when loot dropped), `CombatEnded` (keeps the summary the player's own turns earned; a `shared kill` closes cleanly), `EnemyDefeated` (log line), `CharacterRespawned` (clears `dead`, restores health/mana, moves the location and re-reads the map graph)
- `LocationEntered` fills `enemies` from `spawnedEnemies` (`SpawnedEnemy { id, name, level, currentHealth, maxHealth }`), filtered so a fight resumed after a reconnect is not listed twice

#### `web/client/src/lib/game/CombatPanel.svelte`
- The combat UI: enemy portrait addressed by `archetypeSlug`, name/level/health, participants with their share, the enemy's ability list, the running log, the outcome summary, and the attack / defend / flee / ability buttons behind a `waiting` guard so a turn cannot be submitted twice

#### `web/client/src/lib/game/GameScreen.svelte`
- The location's enemy roster with an Engage button per enemy (`session.engageEnemy(enemy.id)`), the death banner with Respawn, and the disconnected panel — a kick is a dead session, not a dismissible banner

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
| 8 | Tests — web combat state (`session.test.ts`, `combat-panel.test.ts`) | ✅ |
| 9 | `CombatPanel.svelte`, plus the roster / death banner / disconnected panel in `GameScreen.svelte` | ✅ |
| Final | `dotnet test Realm.Full.slnx` and `cd web && npm run test` | ✅ (the `Veldrath.slnx --filter "Category!=UI"` run retired with the client) |

#### Phase 8 — Test Priorities
- `EngageEnemyHubCommandHandlerTests`: guard (already in combat), enemy not found, enemy dead, success
- `AttackEnemyHubCommandHandlerTests`: no session guard, no enemy guard, enemy killed → rewards distributed, player killed → death handling
- `DefendActionHubCommandHandlerTests`: damage reduction when defending
- `FleeFromCombatHubCommandHandlerTests`: 50% flee logic (seed RNG for determinism)
- `UseAbilityInCombatHubCommandHandlerTests`: cooldown guard, mana guard, heal vs damage path
- `RespawnHubCommandHandlerTests`: HC guard, alive guard, HP/mana restore, session cleared
- `web/client/src/lib/game/session.test.ts` combat cases: the fight opens on `CombatStarted`, a turn updates the enemy's health and keeps the whole log, `enemyDefeated` closes the fight, `playerDefeated` sets `dead`, and a reconnect does not duplicate the enemy being fought. The panel's own rendering is `combat-panel.test.ts`.

#### Phase 9 — Combat panel (web)
Implemented as [`CombatPanel.svelte`](../../web/client/src/lib/game/CombatPanel.svelte), with the roster and the death banner in [`GameScreen.svelte`](../../web/client/src/lib/game/GameScreen.svelte):
- The location's spawned enemies are listed with name, level and health, each with an Engage button
- The panel (open while `combat.active`): enemy portrait and name/level/health, participants with their share, the enemy's ability list, the running log, the outcome summary — including the hardcore note on a permadeath — and the Attack / Defend / Flee buttons
- A defeated player gets the death banner with a Respawn button; the server moves them to the zone's safe location and the shell re-reads the map graph

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
- Scope: Engine (Core/Shared/Data) + Veldrath (Server/Contracts) ONLY — `Veldrath.Client` was deleted in session-68 (2026-10-25); the client is the SvelteKit app in `web/`.

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

