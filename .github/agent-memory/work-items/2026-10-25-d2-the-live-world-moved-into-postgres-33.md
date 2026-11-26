---
kind: work-item
date: 2026-10-25
area: engine
pr: 33
title: "D2: the live world moved into Postgres (#33)"
---

### Session-74 (2026-10-25) — D2: the live world moved into Postgres (#33)

**Deleted:** `ZoneLocationEnemyStore` and `CombatSessionStore` (both `static ConcurrentDictionary`
singletons). `grep -rn "static readonly ConcurrentDictionary" Veldrath.Server/Features/Characters/Combat`
now returns nothing.

**Added:**
- `IEnemyInstanceStore` / `ICombatSessionStore` in `Veldrath.Server/Features/Characters/Combat/`.
- `EfCoreEnemyInstanceStore`: `GetAliveOrSpawnAsync`, `TryGetAsync`, `SaveAsync`, `TryMarkDeadAsync`,
  `RemoveAsync`, plus a private `SettleLocationAsync` maintenance pass.
- `EfCoreCombatSessionStore`: `TryGetAsync`, `IsInCombatAsync`, `SetAsync`, `RemoveAsync`,
  `RemoveByEnemyIdAsync`.
- Both registered scoped in `Program.cs` next to the repositories.
- `ActorPoolResolver.ResolveArchetypeAsync(slug)` so a materialised enemy can rebuild its content template.
- `SpawnedEnemy.Id` became `init` (was get-only) so rows can be materialised with their real id.
- `CombatHelpers.RespawnDelay` (60 s) replaced the deleted `ScheduleRespawn` timer.

**Invariants that now live in the database:**
- **Death is one conditional UPDATE.** `SaveAsync` persists health + participants only; `TryMarkDeadAsync`
  runs `UPDATE … WHERE IsAlive` and returns true for exactly one caller. Only that caller distributes
  rewards, so the old in-memory `WasRewarded` guard is unnecessary (and would not have survived per-request
  materialisation).
- **Respawns are lazy.** `GetAliveOrSpawnAsync` first runs `SettleLocationAsync`: it kills zero-health rows
  still flagged alive (crash between save and death), then revives rows whose `RespawnAt` has passed,
  restoring full health and deleting their participants. D3 (#34) replaces this with a proactive sweeper
  plus the `EnemySpawned` broadcast.
- **`ExecuteUpdate`/`ExecuteDelete` bypass the change tracker.** The store detaches affected rows
  (`DetachTracked`) and reads with `AsNoTracking`. Missing this masked a death transition inside one scope
  and cost two failing tests — keep it in mind for D3/D4.
- **No cache.** Materialising from indexed lookups is fast enough; if a cache is ever added it goes inside
  the store (invalidate on write, check `Version` on read).

**Handler changes:** `Engage`, `Attack`, `DefendAction`, `FleeFromCombat`, `UseAbilityInCombat` and
`Respawn` now take the two stores; `Attack`/`UseAbility` lost their `IServiceScopeFactory`/`IHubContext`
dependencies with the timer. `TravelToNodeHubCommandHandler` and `NavigateToLocationHubCommandHandler`
call `GetAliveOrSpawnAsync`, which also makes first arrival the place a wiped world comes back.

**Tests:** new `EfCoreEnemyInstanceStoreTests` (6) and `EfCoreCombatSessionStoreTests` (4) against the
SQLite harness; the six combat handler test classes now use `FakeEnemyInstanceStore` /
`FakeCombatSessionStore` from `Veldrath.Server.Tests/Infrastructure/` instead of the statics.
Server suite 696 → **706**, full solution **3,851 passing**, format clean.
