---
kind: work-item
date: 2026-11-25
area: web
card: t_1af378cd
title: "A snapshot for the zone you are still in repaints the roster but is not an arrival"
---

### Session t_1af378cd (2026-11-25) — The same-zone snapshot is not an arrival

Follow-up to [`2026-11-25-t_375207b7-a-scope-change-inside-one-zone-is-an-arrival.md`](2026-11-25-t_375207b7-a-scope-change-inside-one-zone-is-an-arrival.md),
which made the server send a `ZoneEntered` snapshot when the SignalR group changes inside one zone — the client
rewrites `session.occupants` only from `ZoneEntered`, `PlayerEntered` and `PlayerLeft`, so a room-to-room walk
needed one.

**The defect.** `web/client/src/lib/game/session.svelte.ts`'s `hub.on('ZoneEntered', …)` wrote
`` `Entered ${payload.name}.` `` unconditionally. With the snapshot now arriving on a scope change, descending
from a shared room of `the-droveway` into its instanced `rootbound-cellars` logged the walk's own "Travelled to
Rootbound Cellars." (from `NodeTraveled`) **and** "Entered The Droveway." — a zone the player never left. The
same line came back walking out, and once per instanced room entered through the dungeon command's
`EnterLocation` path.

**The fix.** The handler reads the id it already holds before overwriting it:
`const arrived = this.zone?.id !== payload.id;` then always assigns `this.zone` and `this.occupants`, and only
calls `addSystemMessage` when `arrived`. The snapshot is what carries the roster, so it is never dropped; only
the announcement is gated. A null `zone` (the first entry, or an entry after `ZoneLeft` / a region-map walk
cleared it) counts as an arrival, so a real border crossing still announces — including one that follows a
same-zone snapshot.

**Tests** (`web/client/src/lib/game/session.test.ts`, beside "enters a zone with its occupants"):
"announces a zone entry once, then repaints a scope change inside the zone silently" emits two snapshots with
the same zone id, the second carrying a new occupant list, and asserts the zone and occupants were replaced
while the log still holds exactly one "Entered The Droveway." line; a third snapshot with a different zone id
then adds the second line. Negative control: with only `session.svelte.ts` reverted
(`git stash push -- web/client/src/lib/game/session.svelte.ts`) the new test fails on the duplicate line —
`Received [... "Entered The Droveway.", "Entered The Droveway."]`.

**Gates** (this worktree, from `web/`): `npm run typecheck` 0 errors / 0 warnings; `npm run lint`
(prettier `--check` + eslint) clean; unit suite 107 files / 1601 tests passed; `npm run build` completed
(adapter-node); `node tests/run-e2e.mjs stack-free` 30 specs passed.

**Left alone.** No server change: the event, its payload and its senders are correct and reviewed in
`t_375207b7`, and `PlayerEntered` / `PlayerLeft` take no message at all. Any `ZoneEntered` for the zone already
held is now silent wherever it comes from — that is the intent, since the line belongs to the walk (or the
respawn, or whatever path caused it), not to a zone the player is still standing in.
