# Server-First World State (Milestone D)

> **Position in the roadmap:** D → C → A → B.
> D is first because party state (C), death consequences (A) and content validation (B) all assume the
> server is the single source of truth and that world state survives a restart. Building them on the
> current in-process stores would mean building them twice.
> Tracker and coverage work is explicitly deferred until functional systems are in place.

---

## 1. The problem, stated precisely

The database already owns the things that matter most: characters, attributes, inventory, equipment,
ground items, quests, sessions, zones, the map graph and all content. What is **not** in the database is
the *live world*: the enemies standing in a location, their health, who is fighting them, and when they
come back. All of that lives in `static ConcurrentDictionary` instances and a `Task.Delay` timer inside
one process.

Consequences today:

1. **A restart wipes the world.** Every enemy returns via the spawn path, every fight is lost, every
   pending respawn timer vanishes.
2. **A second instance would fork the world.** Two processes spawn two different sets of enemies for the
   same location, and each only broadcasts to the players connected to it.
3. **Nothing recovers deterministically.** A `Task.Run` + `Task.Delay(60s)` respawn either fires in the
   process that scheduled it or never fires at all; there is no record that a respawn was owed.

## 2. Inventory of process-local state (verified)

| # | State | Where | Lifetime today | Target |
|---|---|---|---|---|
| 1 | Live enemy instances: id, archetype, HP, level, participants, damage contributions, last attacker | [`ZoneLocationEnemyStore`](../Veldrath.Server/Features/Characters/Combat/ZoneLocationEnemyStore.cs:9), static `ConcurrentDictionary<string, List<SpawnedEnemy>>` keyed `zoneGroup/locationSlug` | Process | Postgres rows + read-through cache |
| 2 | Enemy respawn timers | [`CombatHelpers.ScheduleRespawn`](../Veldrath.Server/Features/Characters/Combat/CombatHelpers.cs:141), `Task.Run` → `Task.Delay(60s)` | Fire-and-forget task | `RespawnAt` column + hosted sweeper |
| 3 | Active combat sessions: zone group, location, enemy id, turn number, defending flag | [`CombatSessionStore`](../Veldrath.Server/Features/Characters/Combat/CombatSessionStore.cs:6), static `ConcurrentDictionary<Guid, ActiveCombatSession>` | Process | Postgres rows with a short TTL |
| 4 | AFK flags and messages | `GameHub._afkCharacters`, static `ConcurrentDictionary<Guid, string?>` | Process | Decide in D4 (see §7) |
| 5 | Character claims and the 30 s reconnect grace window | [`ActiveCharacterTracker`](../Veldrath.Server/Services/ActiveCharacterTracker.cs:53) | Process | Keep as a cache, back the invariant with a DB uniqueness constraint |
| 6 | Player presence (which character is in which zone/region) | `PlayerSession` entity | **DB already** | Unchanged |
| 7 | Dropped items | `GroundItem` entity | **DB already** | Unchanged, TTL decision in §7 |
| 8 | Zones, locations, map graph, actors, items, quests | Content tables | **DB already** | Unchanged |

Only items 1–5 need work. Items 6–8 are the proof that the pattern already exists in this codebase and
works: `PlayerSession` is exactly what an enemy instance row should look like.

## 3. Target architecture

**Rule: Postgres is the source of truth for world state. Any in-process structure is a cache that can be
dropped at any instant without losing information.**

### 3.1 New tables

```text
EnemyInstances
  Id                uuid    PK
  ZoneId            text    -- FK Zones
  LocationSlug      text
  ArchetypeSlug     text    -- references actor content; validated at spawn
  Name              text    -- snapshot, so a content rename does not rename a live enemy
  Level             int
  CurrentHealth     int
  MaxHealth         int
  IsAlive           bool
  SpawnedAt         timestamptz
  RespawnAt         timestamptz NULL   -- set when killed; swept when <= now()
  Version           bigint             -- optimistic concurrency
  UNIQUE (ZoneId, LocationSlug, ArchetypeSlug, SpawnedAt)

EnemyParticipants
  EnemyInstanceId   uuid    FK EnemyInstances ON DELETE CASCADE
  CharacterId       uuid    FK Characters
  Damage            int
  LastAttackedAt    timestamptz
  PK (EnemyInstanceId, CharacterId)

CombatSessions
  CharacterId       uuid    PK     -- one fight per character, deliberately
  EnemyInstanceId   uuid    FK EnemyInstances ON DELETE CASCADE
  ZoneGroup         text
  LocationSlug      text
  TurnNumber        int
  PlayerDefending   bool
  StartedAt         timestamptz
  UpdatedAt         timestamptz
```

Notes that matter:

- `RespawnAt` replaces the timer entirely. A killed enemy is a row with `IsAlive = false` and a future
  `RespawnAt`; the sweeper flips it back, so a restart mid-timer simply continues the countdown.
- `CombatSessions` keyed by `CharacterId` keeps the current "one fight per character" invariant as a
  primary key instead of an implicit dictionary behaviour.
- `Version` exists so a second instance can detect that its cache entry is stale instead of serving
  enemy HP from five seconds ago.

### 3.2 Replacements for the static stores

Keep the call surface but move it behind interfaces, so the ~8 combat handlers change one line each:

- `IEnemyInstanceStore` — `GetAliveAsync(zoneId, locationSlug)`, `TryGetAsync(enemyId)`,
  `ApplyDamageAsync(enemyId, characterId, damage)`, `KillAsync(enemyId, respawnDelay)`,
  `SpawnRosterAsync(...)`.
- `ICombatSessionStore` — `TryGetAsync(characterId)`, `SetAsync(session)`, `RemoveAsync(characterId)`,
  `RemoveByEnemyIdAsync(enemyId)`.

The static classes are deleted, not kept as a wrapper, so no handler can accidentally take the in-memory
path. A cache layer sits *inside* the implementation, keyed by enemy id, invalidated on write and
versioned on read.

### 3.3 The respawn sweeper

`EnemyRespawnService : BackgroundService`, one loop:

```sql
-- claim work atomically; SKIP LOCKED makes N instances safe
SELECT * FROM "EnemyInstances"
WHERE "IsAlive" = false AND "RespawnAt" <= now()
ORDER BY "RespawnAt"
LIMIT 50
FOR UPDATE SKIP LOCKED
```

For each row: re-apply the archetype's spawn template, reset HP, clear participants, set `RespawnAt = NULL`,
commit, then broadcast `EnemySpawned` to the zone group. Because the broadcast happens after commit and a
reconnect re-reads the roster, a missed broadcast is a cosmetic delay, not a lost enemy.

### 3.4 Multi-instance safety

With SignalR in a single instance, "multi-instance" first means **surviving restarts and rolling
deploys**, which is the actual requirement. True horizontal scale additionally needs a SignalR backplane;
that is a separate, later decision and is *not* required for D to pay off.

What D must guarantee:

- Only one process can own a character claim: a partial unique index on active `PlayerSessions`
  (`UNIQUE ("CharacterId") WHERE "DisconnectedAt" IS NULL`), or an advisory lock taken in the claim path.
  A refused claim already has a user-visible path ("character is active elsewhere").
- Only one process can respawn a given enemy: `FOR UPDATE SKIP LOCKED` in the sweeper.
- No handler reads world state without going through the store, so a stale cache is a latency problem,
  never a correctness one.

## 4. Failsafes, not fallbacks (project-wide rule)

This milestone sets the rule the user asked for, and every later milestone inherits it:

> When referenced content is missing, the operation **fails loudly**: an error log that names the missing
> slug and the referencing table, plus a client-visible message. It never substitutes a placeholder, a
> default archetype, or a silently empty roster.

Concretely:

- `SpawnRosterAsync` with an unknown `ArchetypeSlug` → `LogError` + failed hub command with
  "Content error: unknown actor 'x'" — not an empty enemy list.
- Entering a location whose spawn pool references deleted content → the location loads empty **and**
  logs one error per dangling reference, so the player is not stuck but the operator sees it.
- A startup validator walks content references (zone → location → actor pool, item → power, quest →
  item/kill target) and logs every dangling reference at `Error` as one consolidated report.
- No new `??` content defaults in the server. The existing "no content" paths get a test that asserts the
  error is logged.

## 5. Phases

| Phase | Work | Done when |
|---|---|---|
| **D1** | Schema, EF entities, migration for the three tables and their indexes | Migration applies to a fresh DB and to the current schema; `dotnet build Realm.Full.slnx` clean |
| **D2** | `IEnemyInstanceStore` / `ICombatSessionStore` + implementations; move every combat handler onto them; delete the static classes | No `static ConcurrentDictionary` left in `Features/Characters/Combat`; all combat tests pass |
| **D3** | `EnemyRespawnService` sweeper replaces `CombatHelpers.ScheduleRespawn`; delete the timer | Killing an enemy leaves a row with `RespawnAt`; the enemy returns after a restart |
| **D4** | Claim uniqueness in the DB; cache invalidation on write; AFK decision; boot-time pruning of stale sessions | Two hosts against one DB: same enemy HP, one respawn, a second claim of the same character is refused |
| **D5** | Startup content validator + failsafe tests; boot recovery test | Deleting an actor row produces a logged error and a user-visible message, never a placeholder |

Each phase is independently shippable and leaves the game playable.

## 6. Acceptance criteria

1. **Restart mid-fight:** kill the server while a player is fighting; on restart the enemy still has the
   HP it had, the player's session is either resumed or closed with a system message, and no enemy
   duplicates exist.
2. **Restart mid-timer:** kill an enemy, restart before `RespawnAt`, confirm the enemy returns at roughly
   the original time — not immediately, not never.
3. **Two instances, one DB:** the same zone shows the same enemies with the same HP to clients of both
   processes, and a killed enemy produces exactly one respawn.
4. **No world state in statics:** `grep -r "static readonly ConcurrentDictionary" Veldrath.Server` returns
   only the claim tracker and the provisioning cache (both documented caches).
5. **Failsafe proof:** a test deletes an `ActorArchetype` referenced by a zone's spawn pool and asserts an
   `Error` log entry plus a client message.

## 7. Open decisions for the user

1. **Combat across a reconnect** — should a dropped player rejoin the fight (session survives) or should
   the fight end for them (current behaviour, session deleted)? Rejoining is nicer and the schema above
   supports it, but it needs a client-side resume path.
2. **Ground item lifetime** — items on the ground currently live forever. Keep that, or expire them
   (e.g. 24 h) so the table does not grow without bound?
3. **AFK state** — persist it (visible across instances) or keep it ephemeral per connection and drop the
   static dictionary in D4?
4. **Hardcore death** — hardcore characters are soft-deleted on death. Is that final, or should a hardcore
   death instead move the character to a memorial roster the player can still see?

## 8. Deliberately out of scope

- Party, trade, duel (Milestone C — they will *use* this storage layer).
- Death penalty tuning (Milestone A — the penalty exists today, 10% XP and 10% gold).
- Content authoring and validation UX in Foundry (Milestone B).
- SignalR backplane / horizontal scale beyond restart safety.
- Coverage gates, Codecov policy, label/tracker hygiene (explicitly deferred).
