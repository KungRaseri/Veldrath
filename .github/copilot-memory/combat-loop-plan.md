# Plan: Combat Loop + Multiplayer Architecture (2026-03-30)

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
All server code lives in `RealmUnbound.Server/Features/Characters/Combat/`.

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
- **`ServerCharacter` alias**: `CombatHelpers.cs` needs `using ServerCharacter = RealmUnbound.Server.Data.Entities.Character;` to avoid ambiguity with engine's `Character` model

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
- Added `using RealmUnbound.Server.Features.Characters.Combat;`
- Added 6 new hub methods: `EngageEnemy(EngageEnemyHubRequest)`, `AttackEnemy()`, `DefendAction()`, `FleeFromCombat()`, `UseAbilityInCombat(UseAbilityInCombatHubRequest)`, `Respawn()`
- Hub broadcast events: `CombatStarted` (caller), `EnemyEngaged` (OthersInGroup), `CombatTurn` (caller), `EnemyDefeated` (OthersInGroup), `CombatEnded` (caller), `CharacterRespawned` (caller)
- Hub request DTOs (at bottom of GameHub.cs in `RealmUnbound.Server.Hubs` namespace): `EngageEnemyHubRequest(string LocationSlug, Guid EnemyId)`, `UseAbilityInCombatHubRequest(string AbilityId)`

#### `Program.cs`
- Added `builder.Services.AddScoped<RealmUnbound.Server.Features.Characters.Combat.ActorPoolResolver>();`

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
| Final | `dotnet test RealmUnbound.slnx --filter "Category!=UI"` | ✅ 525+491=1016 |

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
- Scope: Engine (Core/Shared/Data) + Unbound (Server/Client/Contracts) ONLY.

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

## Phase 1 — Character Creation + Zone Session (parallel with Phase 2)

- Extend `CreateCharacterRequest` with `DifficultyMode` ("normal"|"hardcore", validated); store on Character entity
- Add `DifficultyMode` + `IsHardcore` to `CharacterDto` in RealmUnbound.Contracts; update endpoint + tests
- Zone group naming in `GameHub.EnterZoneAsync`: load Zone.Type → Town/Tutorial = `{zoneId}`, Wilderness = `{zoneId}_{difficultyMode}`, Dungeon = per-instance (existing flow)
- Client: [HC] badge on character select card when IsHardcore

---

## Phase 2 — Core Engine Cleanup (parallel with Phase 1)

- Fix double item-drop: remove HandleItemDropping() from RespawnCommandHandler
- Fix GetEnemyInfoQuery.Attack → use BasePhysicalDamage not Strength
- Add zero-coverage handler tests: HandlePlayerDeathCommandHandlerTests, RespawnCommandHandlerTests, GetDroppedItemsHandlerTests, GetRespawnLocationQueryHandlerTests

---

## Phase 3 — Server Combat Architecture (depends on Phase 0)

### Domain model

**`QueuedActionType` enum** (new, in Features/Combat/): `Attack`, `UsePower`, `Defend`

**`QueuedAction` record** (new, value object): `(QueuedActionType Type, string? PowerSlug)`

**`CombatParticipant` record:**
- `CharacterId`, `CharacterName`, `ThreatScore: int`
- `IsDefending: bool` (cleared after enemy evaluates the tick that benefited from it)
- `QueuedAction: QueuedAction? (Type: QueuedActionType, PowerSlug: string?)` (null = nothing queued)
- `NextActionAvailableAt: DateTimeOffset` (set to `DateTimeOffset.MinValue` initially = ready immediately)
- `ActionSpeed: double` (resolved once at join from `1.0 + (Dex - 10) * 0.02`)
- `PowerCooldowns: Dictionary<string, DateTimeOffset>` (powerSlug → DateTimeOffset when recharge expires)

**`CombatEncounter` record:**
- `EncounterId`, `ZoneId`, `LocationSlug`, `GroupName`, `EnemySnapshot`
- `List<CombatParticipant> Participants`
- `IsActive: bool`
- `JoinWindowEndsAt: DateTimeOffset` (10s after creation)
- `EnemyNextAttackAt: DateTimeOffset` (set at creation = now + EnemyAttackIntervalMs)
- `EnemyAttackIntervalMs: int` (derived from enemy stats at creation; e.g. 1500ms default)

### Action Speed Formula

`ActionSpeed = 1.0 + (character.Dexterity - 10) * 0.02`
- Dex 10 (base): ActionSpeed = 1.0 — no change
- Dex 15: ActionSpeed = 1.10 — 10% faster
- Dex 20: ActionSpeed = 1.20 — 20% faster
- Floor: 0.5 (prevents degenerate low-Dex stalls)

**Universal — applies to all characters regardless of weapon type or role.** `ActionSpeedResolver` takes only `character.Dexterity`, no weapon or school lookup. Resolved once at encounter join; stored as `CombatParticipant.ActionSpeed: double`. Weapon swaps mid-combat have no effect until next encounter.

**Effective GCD per action** (the single `NextActionAvailableAt` cooldown):
| Action | Base Interval | Effective |
|---|---|---|
| Basic Attack | `1000ms / weapon.attackSpeed` (e.g. sword=1000ms, bow=1250ms; fallback=1000ms) | `base / ActionSpeed` |
| Use Power | `max(800ms, Power.Stats.CastTime * 1000ms)` | `base / ActionSpeed` |
| Defend | 800ms fixed | `800ms / ActionSpeed` |

Floor of **800ms** on the GCD. Prevents sub-second spam on fast powers (Bite=200ms → 800ms, Power Strike=500ms → 800ms). Dex still visibly speeds up melee (sword: 1000ms at Dex 10 → ~833ms at Dex 20, just touching the floor). Max effective throughput: ~1.25 actions/second at maximum Dex.

**Per-power recharge cooldown** (separate from GCD — governs how soon the *same power* can be queued again):
- Stored in `CombatParticipant.PowerCooldowns: Dictionary<string, DateTimeOffset>`
- Sourced from `Power.Stats.Cooldown` (float seconds, e.g. Fireball=8.0s, Power Strike=6.0s)
- Set when the power *fires* (not when queued): `PowerCooldowns[powerSlug] = now + TimeSpan.FromSeconds(power.Stats.Cooldown)`
- Powers with no cooldown entry (or entry <= now) are available

**Fallback rule (Option B)**: When GCD clears and fired action is `UsePower` but `PowerCooldowns[powerSlug] > now` → execute basic attack instead. Emit `CombatActionFired` event with:
```
ActualAction: "basic_attack"
RequestedAction: "fireball"
FallbackReason: "on_cooldown"
PowerCooldownRemainingMs: 6200
```
Client displays in combat log: *"Fireball on cooldown (6.2s) — basic attack used instead."*

### Services

**`ActiveCombatStore`**: `ConcurrentDictionary<string, CombatEncounter>` keyed by `"{zoneId}_{locationSlug}"`
- Exposes `EvaluateAsync(string key, Func<CombatEncounter, CombatEncounter> mutate)` which acquires a per-encounter `SemaphoreSlim(1,1)` to serialize all concurrent mutations (prevents races when two players QueueAction simultaneously)

**`ActionSpeedResolver` service**: computes effective GCD from action type, power slug (if any), and character Dex.
- Reads `attackSpeed` from equipped weapon's traits via `TraitSystem.AttackSpeed`; fallback 1000ms
- Reads `Power.Stats.CastTime` from power catalog for `UsePower` actions; floor 500ms
- Applies `ActionSpeed` divisor to base interval

**`ActorPoolResolver` service**: pick archetype slug from ZoneLocationEntry.ActorPool (weighted random); call EnemyGenerator.GenerateEnemyByNameAsync; procedural fallback if pool empty.

**`ICombatSettingsResolver` service**: maps character.DifficultyMode → ICombatSettings.

### EvaluateCombatTicks

Pure static method `CombatTickEvaluator.Evaluate(CombatEncounter encounter, DateTimeOffset now)`:

```
1. For each participant where QueuedAction != null && NextActionAvailableAt <= now:
   a. If action is UsePower AND PowerCooldowns[powerSlug] > now:
      - Fallback to basic attack
      - Emit CombatActionFired { ActualAction="basic_attack", RequestedAction=powerSlug,
        FallbackReason="on_cooldown", PowerCooldownRemainingMs }
   b. Else:
      - Fire queued action (Attack/UsePower: compute damage, update ThreatScore;
        Defend: ThreatScore += 50, IsDefending = true)
      - If UsePower: set PowerCooldowns[powerSlug] = now + rechargeInterval
      - Emit CombatActionFired { ActualAction, RequestedAction=ActualAction }
   - Set NextActionAvailableAt = now + effectiveGcd (from ActionSpeedResolver)
   - Set QueuedAction = null

2. while EnemyNextAttackAt <= now && encounter.IsActive:
   - Pick target = participant with highest ThreatScore (CharacterId tiebreak)
   - Apply enemy damage (modified by target.IsDefending: -20% if defending)
   - Clear target.IsDefending
   - Set EnemyNextAttackAt += EnemyAttackIntervalMs
   - If target HP <= 0 AND zone is NOT safe → flag for death cascade
   - Collect CombatEvent for broadcast

3. If enemy HP <= 0 → IsActive = false, flag CombatVictory
```

Returns `TickResult` value object containing: `CombatEvent[]`, `CombatVictory?`, `DeathCascade[]`.

---

## Phase 4 — Hub Bridges: Combat Loop (depends on Phase 3)

New files in `RealmUnbound.Server/Features/Combat/`:

### StartCombatHubCommand / GameHub.StartCombat(locationSlug)
- Resolve enemy via ActorPoolResolver
- Resolve initial participant's AttackIntervalMs via AttackSpeedResolver
- Create CombatEncounter: EnemyNextAttackAt = now + EnemyAttackIntervalMs; initial participant NextAttackAvailableAt = DateTimeOffset.MinValue; ThreatScore = 1
- Store in ActiveCombatStore
- Broadcast CombatStarted { EncounterId, EnemyName, EnemyLevel, EnemyHp, LocationSlug, JoinWindowSeconds } to zone group

### JoinCombatHubCommand / GameHub.JoinCombat(encounterId)
- Validate: encounter exists, join window open, player at same LocationSlug, not already participant
- Resolve AttackIntervalMs via AttackSpeedResolver for this player
- Add participant (ThreatScore = 0, NextAttackAvailableAt = DateTimeOffset.MinValue)
- Scale enemy HP +50%
- Broadcast CombatPlayerJoined { EncounterId, CharacterName, NewEnemyMaxHp }

### QueueActionHubCommand / GameHub.QueueAction(encounterId, actionType, powerSlug?)
- Validate: encounter exists, player is participant, encounter IsActive
- Validate: if actionType == "use_power", powerSlug must be non-null and power must exist in catalog
- Acquire encounter lock via ActiveCombatStore.EvaluateAsync
- Set participant.QueuedAction = new QueuedAction(actionType, powerSlug) (replaces any existing)
- Call CombatTickEvaluator.Evaluate(encounter, DateTimeOffset.UtcNow)
- Apply HP mutations to character blobs for any ticks that fired
- If death cascade flagged → invoke Phase 5 death cascade
- If CombatVictory → award XP/gold, remove encounter, broadcast CombatVictory { EncounterId, XpEach, GoldEach }
- Else broadcast CombatTickResult { EncounterId, Events: CombatActionFired[], EnemyCurrentHp, PlayerHpMap, PlayerCooldownMap{CharacterId→NextActionAvailableAt}, PowerCooldownMap{CharacterId→{powerSlug→remainingMs}} }

### PollCombatHubCommand / GameHub.PollCombat(encounterId)
- Same as QueueAction but without setting QueuedAction first
- Purely pumps pending enemy ticks if client has been idle
- Returns same CombatTickResult shape (may be empty Events if nothing fired)

---

## Phase 5 — Death & Respawn (depends on Phase 3)

### Death cascade
- deathGoldCost = character.Level * 15
- Deduct from blob Gold key (floor 0)
- Via EF: zone.RescueFundTotal += deathGoldCost; globalStats["rescue_fund_total"] += deathGoldCost
- Remove participant from encounter
- Normal: broadcast PlayerDied { CharacterId, CharacterName, GoldCost, RemainingGold, RespawnZoneId }
- Hardcore: character.DeletedAt = DateTimeOffset.UtcNow; broadcast CharacterDeleted { CharacterName, FinalLevel }

### RespawnHubCommand / GameHub.Respawn()
- Validate: Normal mode only
- Restore CurrentHealth = MaxHealth, CurrentMana = MaxMana in blob
- Set character.CurrentZoneId to nearest starter zone (Zone where IsStarter == true)
- Broadcast PlayerRespawned { CurrentHp, MaxHp, RespawnZoneId } to caller

---

## Phase 6 — Client Updates (depends on Phase 4-5)

- Character creation: DifficultyMode choice in CreateCharacterView.axaml + CreateCharacterViewModel
- Character select: [HC] badge when IsHardcore
- GameViewModel new commands: `StartCombatCommand(locationSlug)`, `JoinCombatCommand(encounterId)`, `QueueAttackCommand(encounterId)`, `QueueDefendCommand(encounterId)`, `QueuePowerCommand(encounterId, powerSlug)`, `RespawnCommand`
- GameViewModel new callbacks: `OnCombatStarted`, `OnCombatPlayerJoined`, `OnCombatTickResult`, `OnCombatVictory`, `OnPlayerDied`, `OnPlayerRespawned`, `OnCharacterDeleted`
- **Cooldown bar**: `CombatTickResult.PlayerCooldownMap{CharacterId→NextActionAvailableAt}` drives GCD bar; `PowerCooldownMap` drives per-power button greying
- **Fallback log**: when `CombatActionFired.FallbackReason == "on_cooldown"`, combat log shows *"{PowerName} on cooldown ({N}s) — basic attack used instead."*
- **Poll timer**: `Observable.Interval(TimeSpan.FromSeconds(1))` in GameViewModel while `IsInCombat`; calls `PollCombat` → pumps enemy ticks for idle periods
- Auto-trigger join prompt in OnCombatStarted when player is at same LocationSlug
- Respawn prompt in OnPlayerDied (Normal only)
- CharacterSelectViewModel: hub subscriptions for all combat events + CharacterDeleted cleanup

---

## Phase 7 — Tests (after each phase, not deferred)

- Core: HandlePlayerDeathCommandHandlerTests, RespawnCommandHandlerTests, GetDroppedItemsHandlerTests, GetRespawnLocationQueryHandlerTests
- Server: CombatTickEvaluator unit tests — controllable DateTimeOffset, verify:
  - Basic attack fires when GCD clear
  - Basic attack queues silently when GCD not clear
  - UsePower fires when GCD clear AND power not on recharge
  - UsePower falls back to basic attack when GCD clear but power still on recharge; fallback event emitted with correct RemainingMs
  - UsePower sets PowerCooldowns[slug] on fire
  - Enemy fires on interval; multiple enemy ticks consumed in one pass
  - Defend reduces incoming damage (-20%) and clears IsDefending after enemy tick that benefited
- Server: ActiveCombatStore tests (add/remove/cleanup, EvaluateAsync serialization)
- Server: AttackSpeedResolver tests (weapon trait present, absent, fallback)
- Server: hub command handler tests (no-character guard, not-participant guard, inactive encounter guard)
- Server: threat tests (correct targeting, Defend boost, initial aggro seed)
- Server: death cascade (Normal gold/fund, Hardcore delete, safe-zone guard)
- Client: GameViewModel QueueAttack/QueueDefend/PollCombat commands + cooldown bar binding tests

---

## Relevant Files

Engine:
- `RealmEngine.Shared/Abstractions/ICombatSettings.cs` ✅
- `RealmEngine.Shared/Models/DifficultySettings.cs` ✅
- `RealmEngine.Core/Features/Combat/Services/CombatService.cs` ✅
- `RealmEngine.Core/Features/Combat/AttackEnemy/AttackEnemyCommandHandler.cs` ✅
- `RealmEngine.Core/Features/Death/` — handler tests + double-drop fix (Phase 2)
- `RealmEngine.Data/Entities/Content/ZoneLocation.cs` ✅
- `RealmEngine.Data/Repositories/EfCoreZoneLocationRepository.cs` ✅
- `RealmEngine.Data/Migrations/Content/20260330000004_AddZoneLocationActorPool.cs` ✅

Server:
- `RealmUnbound.Server/Data/Entities/Character.cs` ✅ (DifficultyMode)
- `RealmUnbound.Server/Data/Entities/Zone.cs` ✅ (RescueFundTotal)
- `RealmUnbound.Server/Data/Entities/GlobalStat.cs` ✅
- `RealmUnbound.Server/Data/ApplicationDbContext.cs` ✅
- `RealmUnbound.Server/Data/Migrations/Application/` ✅ (3 migrations)
- `RealmUnbound.Server/Hubs/GameHub.cs` — zone group naming + new hub methods (Phase 1+4)
- `RealmUnbound.Server/Features/Combat/` — new folder (Phase 3): QueueActionHubCommand, PollCombatHubCommand, StartCombatHubCommand, JoinCombatHubCommand, CombatEncounter, CombatParticipant, CombatTickEvaluator, ActiveCombatStore, AttackSpeedResolver, ActorPoolResolver, ICombatSettingsResolver
- `RealmUnbound.Server/Program.cs` — DI registrations (Phase 3)

Client:
- `RealmUnbound.Client/ViewModels/GameViewModel.cs` — new commands + callbacks + poll timer (Phase 6)
- `RealmUnbound.Client/ViewModels/CharacterSelectViewModel.cs` — subscriptions + HC badge (Phase 6)
- `RealmUnbound.Client/Views/CreateCharacterView.axaml` — difficulty mode selection (Phase 6)
- `RealmUnbound.Client/ViewModels/CreateCharacterViewModel.cs` — DifficultyMode property (Phase 6)

Contracts:
- `RealmUnbound.Contracts/` — CharacterDto (DifficultyMode, IsHardcore), new combat payload records, QueuedActionType enum (Phase 1+4)

---

## Verification

1. `dotnet test RealmEngine.slnx` — zero regressions after Phase 0+2
2. `dotnet build RealmUnbound.Server` — clean after each Phase 3-5 step
3. `dotnet test RealmUnbound.slnx --filter Category!=UI` — zero regressions after Phase 4-6
4. Manual: character queues attack → cooldown bar fills → attack fires automatically → enemy HP updates
5. Manual: second player joins → both queue attacks → actions fire independently on their own cooldowns → enemy attacks highest-threat player
6. Manual: player idles → PollCombat timer fires → pending enemy ticks consumed → HP updates arrive
7. Manual: Normal character dies in wilderness → gold deducted (Level*15) → respawn at starter zone
8. Manual: Hardcore character dies → character soft-deleted, disappears from select
9. Manual: any character in town → death cascade does not fire
