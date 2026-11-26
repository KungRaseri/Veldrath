---
kind: work-item
date: 2026-11-25
area: web
card: t_d206ff56
pr: 322
title: "A resumed room is a room the shell can use: the roster builds the location, not just the enemy list"
---

### Session t_d206ff56 (2026-11-25) — The resumed room is built from its roster

Corrects the client half of [`2026-11-25-t_851f4bfd-a-resume-reads-the-room-roster.md`](2026-11-25-t_851f4bfd-a-resume-reads-the-room-roster.md)
(review of PR #322, Forgejo review 348). That note says the `LocationRoster` handler "ignores a roster whose
`locationSlug` does not match the location the shell already stands in"; both halves of that sentence were wrong on
the resume path, which is the only path that receives the event.

**What was broken.** `LocationRoster` set `this.enemies` and never `this.location`, and nothing on the resume path
does (`handleCharacterSelected`, `ZoneEntered`). So a resumed character saw the room's enemies with a live **Engage**
button while `engageEnemy` returned at its `!this.location` guard — the room name was missing from the map header and
`canRest` stayed false. The roster arrived but the room did not.

**What changed** (client only; the server half of #322 is untouched).

- `session.svelte.ts` `LocationRoster`: when there is no location yet, it is built the way `LocationEntered` builds
  it — `{ locationSlug, locationDisplayName, typeKey, spawnedEnemies }` — with the name and type read from the map
  node the shell stands on (`currentNode.displayName` / `.nodeType`; `MapNode.RefId` is the zone-location slug for a
  `Location` node). The slug is only a fallback for a graph that failed to load. No new field on the broadcast, so
  `payload-contract.test.ts` is untouched.
- The stale-room guard now keys on **the node the shell stands on** (`location?.locationSlug ?? currentNode?.refId`),
  not on `this.location` being set, and drops any slugged roster while the character stands on a node that is not a
  `Location` (entry point, zone exit, region). This closes the non-blocking note in the review: a roster landing after
  a move to a location-less node used to be applied, because the old guard only fired when a location existed.
- `handleCharacterSelected` now asks for the **map graph before the roster** (`GetMapGraph` then
  `GetLocationRoster`), still after `EnterZone`. The roster carries only a slug, so the node that names the room has
  to be on hand when it arrives; per-connection serialisation means the graph's broadcast is already queued before
  the roster is sent. `session.test.ts`'s whole-invocation assertion moved the `GetMapGraph` entry to match.

**Tests.** Two new `session-events.test.ts` cases: the resume burst (`CharacterSelected` → `MapGraphReceived` →
`LocationRoster`) asserts the built location (name `Rootbound Cellars`, type `Location`, enemies) **and** that
`engageEnemy('warden')` issues `EngageEnemy` with `{ locationSlug: 'rootbound-cellars', enemyId: 'warden' }`; and a
roster that arrives after walking out onto the entry point leaves the location null and the list empty.

**Negative controls, one at a time (each reverted by restoring the file).** Dropping the location assignment fails
`makes the room a resumed session woke in usable` at `expect(session.location).toEqual(...)` with `Received: null`.
Restoring the old guard (`if (this.location && …)`) fails `does not rebuild a room from a roster that arrives after
walking out of it` at `expect(session.location).toBeNull()`.

**Gates** (from `web/`): `npm run typecheck` (svelte-check 0 errors / 0 warnings), `npm run lint` (prettier + eslint
clean after one `prettier --write` of the new test), `npm run test` — 1600 unit tests in 107 files (1598 before; the
two new cases are the difference) and the 30 stack-free Playwright specs. No server file changed, so no .NET gate.

**Not done on purpose.** No `LocationDisplayName` was added to the `LocationRoster` broadcast: the graph is already in
the same burst, and a contract change would have touched `types.ts`, the payload-contract suite and the server for a
name the client can read locally.
