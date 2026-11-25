---
kind: work-item
date: 2026-11-25
area: engine
card: t_1708ed7c
title: "A ZoneEntered roster names only the caller's own scope, never the whole zone"
---

### Session t_1708ed7c (2026-11-25) — The arrival roster is a scope, not a population

Follow-up to [`2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md`](2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md)
(PR #316). That card made `EnterZone`'s resume branch run-scoped through `GetByZoneInstanceAsync` and documented
"inside a run the run is the boundary" in the method. Two `ZoneEntered` senders still read
`IPlayerSessionRepository.GetByZoneIdAsync`, which is the whole zone and therefore includes characters inside an
instance run — a different SignalR group the arriving connection can neither see nor chat with. The client keeps
that list (`session.svelte.ts` assigns `this.occupants`) until the next `PlayerEntered`/`PlayerLeft` in the group,
so the leak persisted until someone else walked past.

- `GameHub.EnterZone`, shared branch: `resumedRun is null ? GetByZoneIdAsync : GetByZoneInstanceAsync`.
- `GameHub.SyncArrivedZoneAsync`: no scoping at all. It is the snapshot a walk across a zone border sends, so a
  character entering a zone with three parties mid-delve was told the whole zone's population was standing there.

**What changed**

- `IPlayerSessionRepository.GetOnSharedScopeAsync(string zoneId)` — `ZoneId == zoneId && ZoneInstanceId == null`.
  The rule lives in the repository next to `GetByZoneInstanceAsync`, not as a filter at each caller: the two
  reads are the two halves of one rule (run scope / shared scope), and a call site cannot get "shared" as "the
  whole zone" by forgetting the predicate.
- `EnterZone`'s shared branch uses it; the run branch keeps `GetByZoneInstanceAsync` (#316's half).
- `SyncArrivedZoneAsync` reads `Context.Items["CurrentZoneInstanceId"]` — `ApplyArrivalGroupAsync` has just
  written it for this arrival — and sends `GetByZoneInstanceAsync(run)` or
  `GetOnSharedScopeAsync(result.ZoneId)`. Using the connection's own scope item (as `GetLocationRoster` does)
  means the snapshot cannot describe a run the arrival did not join.
- `GetByZoneIdAsync` is untouched and still the whole zone: **`GET /api/zones` deliberately publishes that
  population**, and the card says not to change it. Its XML doc now says so, and points at the two scoped reads.

**Tests** (5 new; Server 2008 → 2013)

- `GameHubZoneTests.EnterZone_Roster_In_The_Shared_Scope_Excludes_Characters_Inside_A_Run` — a shared resident and
  a delver stand in `crestfall`; the entering character's `Occupants` is itself plus the resident.
- `GameHubZoneTests.TravelToNode_Crossing_A_Border_Lists_Only_The_Shared_Scope` — the same pair in `the-droveway`;
  the walk arrives shared (`ZoneGroup = "zone:the-droveway"`, `ZoneInstanceId = null`).
- `GameHubZoneTests.TravelToNode_Crossing_A_Border_Into_A_Run_Lists_Only_The_Run` — the arrival carries a run's
  group and id; `Occupants` is the run's member only, and `Context.Items["CurrentZoneInstanceId"]` is the run.
- `PlayerSessionRepositoryTests.GetOnSharedScopeAsync_ReadsTheSharedScopeWhileTheZoneCountStillSeesRunMembers` —
  the query itself, plus the same zone read through `GetByZoneIdAsync` still returning the delver.
- `ZoneEndpointTests.GetZoneById_Counts_Characters_Inside_An_Instance_Run` — HTTP level: `GET /api/zones/crestfall`
  rises by one for a session inside a run. It measures `before + 1` rather than an absolute count and removes the
  seeded run, session and character in a `finally`, because the Integration collection shares one database with
  `GetZones_Should_Return_Zero_Online_Players_Initially`.

**Negative controls, run one at a time; the tree was restored with `git checkout --` after each.**

- `EnterZone` shared branch back to `GetByZoneIdAsync`: exactly `EnterZone_Roster_In_The_Shared_Scope_…` fails
  (1 failed / 2 passed for the new hub trio).
- `SyncArrivedZoneAsync` back to `GetByZoneIdAsync`: exactly the two border-walk tests fail, the `EnterZone` one
  passes (2 failed / 1 passed).
- The `ZoneInstanceId == null` predicate dropped: exactly the three shared-scope cases fail (repository,
  `EnterZone`, shared border walk); the run-scope walk and the HTTP count pass (3 failed / 63 passed across the
  three classes).
- The endpoint count switched to `GetOnSharedScopeAsync`: exactly
  `GetZoneById_Counts_Characters_Inside_An_Instance_Run` fails (1 failed / 9 passed).

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors), `dotnet test Veldrath.Server.slnx`
(6616 passed / 0 failed: Shared 1158, Core 2748, Data 697, Server 2013), `dotnet format Veldrath.Server.slnx
--verify-no-changes` (exit 0), `node scripts/memory/memory-index.mjs --check`, `node scripts/check-markdown-links.mjs`.

**Noticed and deliberately not changed.** The `/who` chat command still reads `GetByZoneIdAsync`, so a
shared-scope character can still learn the names of players inside runs — the same class of leak, but the card
names the two `ZoneEntered` senders and `GET /api/zones` only; it is recorded here rather than widened into this
diff. `GET /api/zones` itself is untouched on purpose: it is a population count, not a roster.

**Sequencing.** #322 (`t_851f4bfd`) was still open when this was written but no longer gated: the comment's
"both edit `session.svelte.ts`" only applied if this card changed the client, and it does not. `git merge-tree`
reports the two heads compose cleanly, so merge order is free.
