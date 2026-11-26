---
kind: work-item
date: 2026-11-25
area: map
card: t_851f4bfd
title: "A resumed character reads the room's roster, so a dungeon room is not shown empty until the next move"
---

### Session t_851f4bfd (2026-11-25) — A resume reads the roster at the node it wakes on

Follow-up to [`2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md`](2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md)
and [`2026-11-25-t_025183f2-a-swept-run-wakes-in-the-lobby.md`](2026-11-25-t_025183f2-a-swept-run-wakes-in-the-lobby.md).
Those put a reconnecting character back on their saved node, inside their run, or in the lobby when the run was swept.
None of them delivered the **roster**: enemies only ever travelled inside `TravelToNodeHubResult`, so a resumed
session rendered an empty room until the player moved. Chests, floor loot and NPCs came back because the client asks
for them on resume; the roster had no such call.

**What changed**

- `GameHub.GetLocationRoster` (zero-arg, after `EnterLocation`) sends `LocationRoster` to the caller with
  `{ LocationSlug, SpawnedEnemies }` — the same `SpawnedEnemySummary` shape `NodeTraveled` carries. It passes the
  scope the connection renders in (`CurrentZoneGroupName` and `CurrentZoneInstanceId` from `Context.Items`) as a
  `GetLocationRosterHubCommand`; it never takes a position from the client.
- `GetLocationRosterHubCommandHandler` reads the position from the **character row** (`CurrentZoneLocationSlug`),
  which `EnterZone` has just written — including the lobby when a swept run moved the resume point. No slug (an entry
  point, a zone exit, a region node) is an empty answer, not a refusal. A slug whose `ZoneLocationEntry.ZoneId` no
  longer matches the character's zone is ignored with a warning instead of keying a roster under a scope nobody
  standing there loads.
- The read itself moved into **`LocationRosterReader`** (`Veldrath.Server/Features/Zones`), scoped and registered
  next to `ActorPoolResolver`. `TravelToNodeHubCommandHandler.EnsureRosterAsync` now only gates (zone map + location
  node) and delegates; the new handler calls the same method. That is deliberate: a second read path would be a
  second spawn path, and two arrivals at one instant would each see an empty room and insert their own cohort.
  `IEnemyInstanceStore.GetAliveOrSpawnAsync` still owns "spawn on first contact, revive on respawn, return what is
  stored", and the reader keeps the travel path's `CancellationToken.None` for that write.
- Client: `requestLocationRoster()` invokes `GetLocationRoster`; `GameSession.handleCharacterSelected` calls it right
  after `await this.enterZone(payload.currentZoneId)` and before the map graph, only when the character is in a zone.
  The `LocationRoster` handler applies the same engaged-enemy filter `LocationEntered` applies (a resumed fight must
  not appear twice in the room list) and ignores a roster whose `locationSlug` does not match the location the shell
  already stands in — the read can outlive a move.
- `types.ts` gained `LocationRosterPayload`; `payload-contract.test.ts` mirrors the new broadcast without an
  allow-list entry, and `hub-methods.test.ts` needed none either: the client invokes the new method.
- [`plans/dungeons.md`](../../../plans/dungeons.md) F2.4's reconnect row now names the roster read.

**Tests** — 12 new server cases.

- `GetLocationRosterHubCommandHandlerTests`: the roster at the slug the row names (keyed on the run's group), spawn
  on first read with the same ids and the same row count on a second read, an empty answer off a location (no store
  request), a slug missing from the catalog as content debt, a slug that belongs to another zone, and bad arguments.
- `GameHubLocationRosterTests`: the two guards, the command carrying the connection's run group and run id, the
  handler error relayed as `Error`, content warnings relayed as `SystemMessage`, and one end-to-end case — real
  `EnterZone` resume, real `EfCoreEnemyInstanceStore`, real handler — that reads the same cohort twice and asserts
  the `EnemyInstances` row count does not move.
- web: four session cases (asks on resume inside a zone, asks nothing on a region node, applies the roster, drops the
  engaged enemy, ignores a roster for a room already left) plus the resume invocation list in `session.test.ts`.

**Negative controls, one at a time on the committed tree (each reverted with `git checkout --`).**

- Hub passes `instanceId = null` instead of `CurrentZoneInstanceId`: exactly 1 failed — the run-scope case.
- `LocationRosterReader` spawns from the resolver directly instead of going through the store (the "second spawn path"
  this card forbids): 3 failed — stored roster, spawn-once, and the end-to-end resume — 9 passed.
- The client's resume call removed: exactly 2 failed — the new resume case and `session.test.ts`'s invocation list.
- The stale-slug guard removed from the `LocationRoster` handler: exactly 1 failed — the already-left-room case.
- A non-location node turned into `Fail("The character is not standing on a location")`: exactly 1 failed.
- The `LocationRoster` broadcast removed: exactly 2 failed (the run-scope case and the end-to-end read).

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors), `dotnet test Veldrath.Server.slnx`
(6623 passed / 0 failed: Shared 1158, Core 2748, Data 697, Server 2020 — 2008 before, the 12 new cases are the
difference), `dotnet format Veldrath.Server.slnx --verify-no-changes` (exit 0). From `web/`: `npm run typecheck`
(svelte-check 0/0), `npm run lint` (prettier + eslint clean after one `prettier --write` of `session.svelte.ts`),
`npm run test` (107 unit files / 1598 tests, then the 30 stack-free Playwright specs), `npm run build` (exit 0).
`node scripts/memory/memory-index.mjs --check` and `node scripts/check-markdown-links.mjs` were run at the root.

**Reachability — unlike its parents, this one is live.** #316/#318/#321 need a location with `IsInstanced`, which no
seeder sets. This card only needs a room with an actor pool, and the seeded zones have those, so the empty-room bug it
fixes was reachable on shipped content.

**Noticed and deliberately not changed.**

- `TravelToNodeHubCommandHandler`'s "already at this node" branch still returns before the roster read, so a travel
  call targeting the node the character stands on reports no enemies. Nothing in the client issues that call (it is
  the party action), and the resume now has its own read, so this stayed out of scope.
- `NavigateToLocationHubCommand` keeps its own copy of the roster read. It is the legacy bridge the hub-methods parity
  suite allow-lists as unused by the client (`NavigateToLocation`), so folding it into `LocationRosterReader` would
  have changed a dead path's constructor and tests for no behavioural gain. The two live paths (walk, resume) share
  the reader.
- The end-to-end test's roster lives under `zone:crestfall` (a shared room). The instance half — the group and run id
  the command carries — is pinned by the hub case and the handler case rather than by a third full-stack setup.
