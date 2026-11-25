---
kind: work-item
date: 2026-11-25
area: engine
card: t_c31b96fb
title: "Walking out of a run is a departure: participation closes and the grace starts at the walk-out"
---

### Session t_c31b96fb (2026-11-25) — The walk out of a run is a departure too

Follow-up to [`2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md`](2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md),
which taught `EfCoreZoneInstanceService.LeaveAsync` to start the emptiness grace at the departure. That fixed the
disconnect path — the only caller was `GameHub.LeaveCurrentZoneAsync`. The other way out of a run is a walk, and it
went through `GameHub.ApplyArrivalGroupAsync`, which wrote the new `ZoneInstanceId` (usually `null`, because the
destination is a shared room) onto the session row and did nothing else. Two things were therefore wrong for a
character who walked out of an instanced room into the shared rooms of the same zone:

- their `ZoneInstanceParticipant.LeftAt` stayed `null` — a live participation row for someone who had left, where
  the disconnect path closes it;
- `ZoneInstance.LastOccupiedAt` was not refreshed, so a run held longer than `SessionGraceSeconds` (60 s) emptied on
  the first 5 s sweep tick after the walk-out, and the walk back opened a **fresh** run instead of resuming the
  attempt — the same race round 1 of `t_87a63cd7` found on the disconnect path.

**What changed**

- `GameHub.ApplyArrivalGroupAsync` reads `Context.Items["CurrentZoneInstanceId"]` **before** overwriting it, and when
  the previous value is a `Guid` that differs from the arriving `instanceId` (a different run, or no run at all) and
  `_zoneInstances` is non-null, it calls `_zoneInstances.LeaveAsync(previous, characterId)` before clearing the
  session pointer. A walk into the zone's shared rooms and a walk out onto the region map both take this branch.
- Nothing else moved: the group swap, `SetZoneInstanceAsync`, occupant scoping and group composition are as they
  were. `LeaveCurrentZoneAsync` is untouched.
- [`plans/dungeons.md`](../../../plans/dungeons.md) F2.4's "Leave the last instanced room" row now says the row's
  whole rule: the run stays `Open` while any session points at it **and** a walk out starts the grace from the
  walk-out.

**The guard the respawn path depends on.** `LeaveAsync` only refreshes `LastOccupiedAt` for a `ZoneInstanceState.Open`
run, and that stays true. `LeaveExpiredInstanceAsync` calls this same method with `null` for a run that is already
closed (the resurrector wakes in the shared zone), so the leave it now triggers closes participation and reports
nothing else — it cannot put the purge back off for a run nobody can enter. `Leave_DoesNotRestartTheGraceOfAClosedRun`
holds that line: `Empty` run, hour-old `LastOccupiedAt`, one leave, the timestamp is unchanged and `LeftAt` is set.

**Tests** — `GameHubZoneTests` (three, using the real `EfCoreZoneInstanceService` over the test's SQLite context):

- `TravelToNode_WalkingOutOfARun_ClosesParticipationAndStartsTheGrace` — the run was held for an hour; the last member
  walks from the run's room to the zone's shared market. `LeftAt` set, session pointer and context item null, group
  swapped from the run's scope to `zone:crestfall`, then **one** `SweepAsync(1 min)` and the run is still `Open`,
  `FindResumableAsync` answers with the same run id, `FindOrCreateAsync` returns it, and participation is live again.
- `TravelToNode_WalkingBetweenTwoRoomsOfTheSameRun_ReportsNoDeparture` — both rooms are instanced, so the arrival
  carries the same run id: `LeftAt` stays null and `LastOccupiedAt` is untouched.
- `TravelToNode_FromTheSharedZone_ReportsNoDeparture` — a character who was never in a run, checked against a
  `Mock<IZoneInstanceService>`: `LeaveAsync` is never called.

`ZoneInstanceServiceTests`: `Leave_DoesNotRestartTheGraceOfAClosedRun` (the guard above). The harness grew one
optional parameter, `HubHarness.Create(..., zoneInstances:)`, so a test can hand the hub a mocked run service instead
of the real one; it defaults to the real `EfCoreZoneInstanceService` over the test context, which is what the other
hub tests already used.

**Negative controls, run one at a time in a detached copy.**

- `GameHub.cs` restored to its pre-fix body: `TravelToNode_WalkingOutOfARun_ClosesParticipationAndStartsTheGrace`
  fails on its first assertion (`LeftAt` is null); 1 failed / 42 passed.
- The fix with only the `previousInstanceId != instanceId` check dropped: exactly
  `TravelToNode_WalkingBetweenTwoRoomsOfTheSameRun_ReportsNoDeparture` fails (a move inside the run reported a
  departure and restarted the grace).
- `LeaveAsync`'s `run.State == ZoneInstanceState.Open` guard dropped: exactly
  `Leave_DoesNotRestartTheGraceOfAClosedRun` fails, `LastOccupiedAt` off by 59 m 59 s.

**Reachability, stated plainly.** Same as the parent card: no seeder or content pack sets a location `IsInstanced`, so
on shipped content every room is in the shared zone and neither the resume nor this departure fires. This is code
with tests until content marks a room instanced, and
[`docs/design/content/wound-spine.md`](../../../docs/design/content/wound-spine.md) still records that gap.

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors), `dotnet test Veldrath.Server.Tests`
(all green), `dotnet format Veldrath.Server.slnx --verify-no-changes`, `node scripts/memory/memory-index.mjs --check`,
`node scripts/check-markdown-links.mjs`.

**Noticed and deliberately not changed.** `ApplyArrivalGroupAsync` handles the departure for a walk; a *logout*
inside a run still reaches `LeaveCurrentZoneAsync`, which deletes the session row and calls `LeaveAsync` — the same
close, so the two paths now agree. `LeaveAsync`'s early return on an already-departed participant still skips the
`LastOccupiedAt` refresh (noted as non-blocking in `t_87a63cd7`), which is now unreachable from both callers: this one
only fires when the previous run differs from the arriving one, and a participant who already left cannot be inside
the previous run.
