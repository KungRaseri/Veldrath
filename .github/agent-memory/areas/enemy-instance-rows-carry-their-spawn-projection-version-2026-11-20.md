---
kind: area
date: 2026-11-20
area: content
title: "Enemy Instance Rows Carry Their Spawn-Projection Version (2026-11-20)"
---

## Enemy Instance Rows Carry Their Spawn-Projection Version (2026-11-20)

`EnemyInstances` rows snapshot `MaxHealth`, `BaseXp` and `GoldReward` at spawn (`EfCoreEnemyInstanceStore.ToEntity`),
and every read, save and respawn takes those numbers from the row. That is deliberate — a mid-fight enemy must not
change because content was retuned — but it is also why rows written before E1 (PR #103, the archetype→template stat
projection) kept the flat 50 HP / 25 XP / 10 gold forever: nothing in production deletes an enemy row.

- `EnemyInstance.StatsVersion` (`CurrentStatsVersion = 1`) is the marker. The migration
  `AddEnemyInstanceStatsVersion` adds the column with `DEFAULT 0`, so every row that already existed reads 0
  (pre-projection) while every spawn from now on stamps 1 in `ToEntity`. That is how "created before the deploy" is
  known without guessing from timestamps or values.
- `EfCoreEnemyInstanceStore.ReconcileStaleStatsAsync` runs from `SettleLocationAsync` (a location read, before any
  respawn) and from `SweepExpiredRespawnsAsync` (before the batch is revived). It refreshes max health and base XP for
  stale rows whether alive or dead, clamping `CurrentHealth` to the new maximum, but re-rolls `GoldReward` only for
  **dead** rows — that column is what the kill path awards, so a living row keeps it and is left un-stamped so the
  reward is refreshed at its first death. The deterministic update only matches rows whose values differ, so a
  converged row costs no write. `TryMarkDeadAsync`'s single conditional UPDATE is untouched.
- Tests: `Veldrath.Server.Tests/Features/EnemyInstanceStatsReconciliationTests.cs` (8 facts, one of them the real seeded
  catalog through the real projection). Seven of them fail on the old store; the eighth pins that a missing archetype
  leaves the row alone.
