---
kind: work-item
date: 2026-11-25
area: engine
card: t_375207b7
title: "A scope change inside one zone is an arrival: it owes a roster and a membership event to both sides"
---

### Session t_375207b7 (2026-11-25) — Changing group without changing zone is still an arrival

Follow-up to [`2026-11-25-t_1708ed7c-a-roster-is-the-callers-scope.md`](2026-11-25-t_1708ed7c-a-roster-is-the-callers-scope.md)
and its review card `t_51346d5b`. #323 made both `ZoneEntered` senders read the caller's own scope, which fixed the
roster whenever one was sent. It did not touch the path that sends nothing: an arrival that changes the SignalR
group while staying inside one zone.

**The defect.** Walking from a shared room of `the-droveway` into its instanced `rootbound-cellars` changes two
things and says neither: `SyncArrivedZoneAsync` returned early on an unchanged zone id, and
`ApplyArrivalGroupAsync` did its `RemoveFromGroupAsync` / `AddToGroupAsync` in silence. The client rewrites
`session.occupants` only from `ZoneEntered`, `PlayerEntered` and `PlayerLeft`, so the walker kept the shared
players it could no longer reach, the shared players kept the walker until their own next zone entry, and the
party inside the cellars never learned anybody joined. Walking back out did the same in reverse. Pre-existing on
both counts — the early return and the silent group move predate #323.

**The fix.**

- `ApplyArrivalGroupAsync` now returns `Task<bool>` — did the group or run change — and announces the move it
  makes through a new `AnnounceScopeMoveAsync`: `PlayerLeft` to the group the connection leaves, `PlayerEntered`
  to the group it joins (`OthersInGroup`, so the caller is not told about itself), each naming its own zone. It
  takes the arrival's zone as an optional third parameter because the zone it records is still the old one when
  the group move happens; the departure names the zone left, the entry names the zone joined.
- `SyncArrivedZoneAsync` takes that flag and sends its snapshot when the zone changed **or** the scope did. The
  roster rule is the #323 one, unchanged: `GetByZoneInstanceAsync` for a run, `GetOnSharedZoneAsync(zoneId,
  SharedScopeDifficulty(...))` for the shared scope (the card body said `GetOnSharedScopeAsync`; #325 had already
  renamed it `GetOnSharedZoneAsync` and added the difficulty argument).
- Both arrival paths that move a group call it: `TravelToNode` (the walk) and `EnterLocation` (the click and the
  dungeon command — `EnterZoneLocationHubCommandHandler` delegates to `TravelToNodeHubCommand`, so a room can be
  entered without walking and the defect was reachable there too). The respawn path through
  `LeaveExpiredInstanceAsync` now passes its zone, so a purged run's departure is announced as well.
- Nothing is sent when neither changed: the same-zone, same-scope walk still returns before the snapshot, and
  the existing test that asserts it now also asserts no membership event.

**Tests** (`Veldrath.Server.Tests/Features/GameHubZoneTests.cs`, new section "a scope change inside one zone",
plus a `SeedRunAsync` helper that seeds an open run with its members' sessions already pointing at it):

- `TravelToNode_Entering_A_Run_Inside_One_Zone_Snapshots_The_Run_And_Announces_The_Move` — one shared resident,
  one party member inside the run: the snapshot names the run's members (the caller's own row moves into the run
  before the list is read, so it appears too, exactly as `EnterZone` does on a resume), `PlayerLeft` goes to the
  shared group, `PlayerEntered` to the run's, and neither group receives the opposite event.
- `TravelToNode_Leaving_A_Run_Inside_One_Zone_Snapshots_The_Shared_Roster_And_Announces_The_Move` — the reverse,
  including the session row's run pointer being cleared.
- `EnterLocation_Into_A_Run_Inside_The_Zone_Snapshots_The_Run_And_Announces_The_Move` — the same arrival through
  the location path, which previously sent nothing at all (it never called `SyncArrivedZoneAsync`).

**Negative controls, run in a detached worktree at the fix commit (`git worktree remove`d afterwards).**

- Whole `GameHub.cs` reverted to the parent commit: all three new tests fail at
  `Single(m => m.Method == "ZoneEntered")` — `Sequence contains no matching element`.
- Snapshot change reverted but announcements kept (`if (!crossedZone)`): the same three failures on the same
  line, so the snapshot assertion is load-bearing on its own.
- Announcements removed but snapshot kept: the same three fail on
  `Expected clients.GroupNamed(...).SentMessages to contain a single item matching (m.Item1 == "PlayerLeft"), but
  the collection is empty`, so the membership assertions are load-bearing too.

**Gates.** `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors); `dotnet test Veldrath.Server.slnx`
(Shared 1158, Core 2748, Data 697, Server 2042 — 6645 passed, 0 failed); `dotnet format Veldrath.Server.slnx
--verify-no-changes` (exit 0); `node scripts/memory/memory-index.mjs --check`; `node
scripts/check-markdown-links.mjs`.

**No web payload change.** The three events and their shapes are the ones the client already listens for; the
server only sends them on a path that used to be silent, so no `types.ts` drift and no parity allow-list entry.

**Noticed and deliberately not changed.**

- The client's `ZoneEntered` handler writes "Entered {name}." to the log every time it fires, so a descent now
  logs the arrival's own "Travelled to …" *and* an "Entered The Droveway." line for the zone the player never
  left. It is a client-side wording fix (announce only when the zone id changed), out of this card's Scope, and
  filed as its own card.
- `EnterZone` still sets its group and announces `PlayerEntered` itself rather than going through
  `ApplyArrivalGroupAsync`; it always starts from a clean connection state, so it has no move to announce, and
  folding it in would be a refactor with no behaviour to gain.
- Walking across a zone border now announces the departure and the entry too (it runs the same group move).
  That is the same defect by a different road, not a new behaviour to explain away.
