---
kind: work-item
date: 2026-10-25
area: content
pr: 34
title: "D3: the respawn sweeper replaces the old timer (#34)"
---

### Session-75 (2026-10-25) — D3: the respawn sweeper replaces the old timer (#34)

**Added:**
- `EnemyRespawnService : BackgroundService` (`Features/Characters/Combat/`): a `PeriodicTimer` loop that calls
  `IEnemyInstanceStore.SweepExpiredRespawnsAsync`, broadcasts `EnemySpawned` per revived enemy to its zone group,
  then calls `ICombatSessionStore.PruneExpiredAsync`. `SweepAsync` is public so a test (or an operator tool) can
  trigger one sweep; `ExecuteAsync` never dies from an exception (logged, retried next tick).
- `EnemyWorldOptions` (`Settings/`): section `Enemies`, `ValidateOnStart` via DataAnnotations —
  `RespawnDelaySeconds` (60), `SweepIntervalSeconds` (5), `RespawnBatchSize` (50), `SessionGraceSeconds` (60).
  A bad value fails the host instead of silently defaulting.
- `EnemyRespawnedEvent` record carries what the broadcast needs.
- Store methods: `IEnemyInstanceStore.SweepExpiredRespawnsAsync(batchSize, ct)` — on PostgreSQL a raw
  `SELECT … FOR UPDATE SKIP LOCKED` inside a transaction, portable LINQ elsewhere, then revive + clear participants;
  `ICombatSessionStore.PruneExpiredAsync(grace, ct)`.
- `Program.cs`: `AddOptions<EnemyWorldOptions>().Bind(...).ValidateDataAnnotations().ValidateOnStart()` +
  `AddHostedService<EnemyRespawnService>()`.

**Changed:** `TryMarkDeadAsync` lost its `TimeSpan` parameter — the store applies the configured delay — so
`CombatHelpers.RespawnDelay` is gone.

**Gotcha:** `AddOptions<T>().Bind()` registers `IOptions<T>`, **not** `T`. Injecting `EnemyWorldOptions` directly into
the hosted service failed DI for every integration test (288 failures) until it took `IOptions<EnemyWorldOptions>`.
The stores take `IOptions<>` for the same reason.

**Tests:** `EnemyRespawnServiceTests` — sweep revives a due enemy with full health and cleared participants, ignores
an enemy whose timer has not passed, `PruneExpiredAsync` deletes only sessions disconnected past the grace, the hosted
service broadcasts `EnemySpawned` exactly once and prunes, and the options reject zero for every field
(4 theory cases). Server suite 706 → **714**, full solution **3,859**, format clean.
