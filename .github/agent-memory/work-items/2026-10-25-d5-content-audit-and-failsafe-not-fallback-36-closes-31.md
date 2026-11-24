---
kind: work-item
date: 2026-10-25
area: content
pr: 36
title: "D5: content audit and failsafe-not-fallback (#36, closes #31)"
---

### Session-77 (2026-10-25) — D5: content audit and failsafe-not-fallback (#36, closes #31)

**The rule, in code.** `ContentReferenceRules` holds three pure checks so they can be tested with plain data and
reused by Milestone B's authoring validators:
- `AuditSpawnPools` — a `ZoneLocation`'s `ActorPool[].ArchetypeSlug` must exist in `ActorArchetypes`.
- `AuditQuestObjectives` — `kill` → archetypes, `collect` → items, `reach`/`visit` → zone locations; other objective
  types carry no content reference and are skipped on purpose.
- `AuditMapEdges` — both ends of a `MapEdge` must be nodes of its own `Map`.
Each returns `ContentIssue(Rule, Message)` naming both sides; none can throw.

**Startup audit.** `ContentAuditService : BackgroundService` runs once (`Task.Yield` first so seeding can finish),
reads **`ContentDbContext` via `IDbContextFactory`** — note `ApplicationDbContext` does *not* map content entities
despite what `ef-core-patterns.md` says — and logs one consolidated summary plus one entry per finding at that
finding's own severity (`ContentAuditService.Report`). The summary is `Error` only when an error-level finding
exists; a warning-only run logs it at `Warning` ("found N warning(s) worth a review") and the catalog still
serves. (This paragraph said "one `Error` per finding" until 2026-11-22 — that was the pre-PR-#178 behaviour.)
It swallows every exception: a broken audit must never be a broken server. `RunAsync` is public for tests/tooling.

**Play-path failsafes.**
- `ActorPoolResolver` lost its `"common"` fallback: a null/empty pool spawns nothing (a safe town is authored
  content), and an unknown archetype logs `Error` naming the slug and spawns nothing for it. It now takes an optional
  logger (`NullLogger` default) so existing test constructions still work.
- `NavigateToLocationHubResult` and `TravelToNodeHubResult` gained `ContentWarnings`, populated when a location has a
  pool but produced no enemies (or when a map node's `RefId` points at a location that does not exist). `GameHub`
  forwards each one as `SystemMessage` after `LocationEntered` / `NodeTraveled`.
- `EnsureRosterAsync` now fetches the location up front instead of inside the spawn lambda, so a dangling `RefId` is
  reported rather than silently producing no roster.

**Tests:** `ContentReferenceAuditTests` (8: clean pools, dangling pool message, per-type quest resolution, dangling
quest target, ignored objective types, dangling edge ends, unknown map, all-empty content) and
`ActorPoolResolverTests` (4: no pool → empty with no repository calls, configured archetype spawns, unknown archetype
spawns nothing, `ResolveArchetypeAsync` returns null). Server 721 → **733**, full solution **3,878**, web 156,
format clean.

**Milestone D is complete:** #32–#36 closed, #31 (epic) closed. The world, its respawns, its fights and its
reconnect grace all live in Postgres; the only remaining process-local state is the claim tracker and AFK, both
documented as caches.
