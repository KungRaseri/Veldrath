---
kind: work-item
date: 2026-10-25
area: content
title: "Milestone D review: runtime smoke and two content findings"
---

### Session-78 (2026-10-25) — Milestone D review: runtime smoke and two content findings

**Smoke test (the part tests cannot cover):** `dotnet run --project Veldrath.Server -- --urls http://localhost:9110`
reaches `Now listening` and `Application started` with both hosted services live and no exceptions. Note the dev
config binds `0.0.0.0:9000`, so the `--urls` argument is required when another server instance is already running.

**The audit immediately found real debt in the dev database** (exactly what D5 was for):
- `the-lost-shipment` objective `collect:lost-cargo` → no such item exists.
- `gather-mushrooms` objective `collect:glowcap-mushroom` → no such item exists (and no item slug matching
  `cargo`, `glowcap` or `mushroom` exists at all).

Both quests are un-completable today. This is content work — either author the items or retarget the objectives —
not code.

**Compounding bug the audit cannot see:** the *kill* path advances objectives with `result.EnemyName` (display name)
while objectives are authored as slugs (`kill:bandit-ruffian` in `the-lost-shipment`, and the audit therefore reports
it as valid). So a correctly authored `kill:<slug>` objective never progresses at runtime. Fixing that is Milestone A's
A5 item; worth doing before any content pass relies on kill objectives.
