---
kind: work-item
date: 2026-11-24
area: map
card: t_62992aa3
title: "A location's enemy roster is keyed by the destination zone, never by the caller's group"
---

### Session-2026-11-24-t_62992aa3 — A location's enemy roster is keyed by the destination zone, never by the caller's group

`EnemyInstances` held the same location's roster under two zone groups at once (`mire-crossing` under
`zone:crestfall` and `zone:grevenmire_normal`; `drove-road` under `zone:crestfall` and
`zone:the-droveway_normal`). A client renders only the group it joined, so one copy was invisible debris and the
other live. The cause was a scope chosen from the caller rather than the destination:
`GameHub.TravelToNode` forwards `Context.Items["CurrentZoneGroupName"]`, that item is only ever written by
`EnterZone` and `ApplyArrivalGroupAsync`, and `ApplyArrivalGroupAsync` compares the *result's* group with it — so
`ResolveScopeAsync` returning the caller's group unchanged for a public location made the comparison see no change
and let `EfCoreEnemyInstanceStore.GetAliveOrSpawnAsync` store the destination roster under the previous zone.

- **The scope is a function of the destination.** `TravelToNodeHubCommandHandler.ResolveScopeAsync` composes it
  from the destination zone and `Character.DifficultyMode` through `ZoneGroupName.ForZone`; wilderness scopes carry
  the mode (`zone:grevenmire_normal`), town and dungeon scopes do not (`zone:crestfall`). The caller's group is
  used in exactly one case: the destination zone row could not be read, where a group that *names* that zone
  (`ZoneGroupName.BelongsToZone`) is the best answer available and a foreign one is still rejected. Reusing the
  caller's group when it merely names the zone is deliberately not done — it would keep a bare wilderness scope or
  a leftover run scope, which splits the roster just as surely.
- **The hub cannot know a walk crosses a border, so it uses the map it does know.** `TravelToNode` forwards the
  context group only when `request.MapId` equals `Context.Items["CurrentMapId"]` (`EnterZone` writes the group and
  the map together), and `EnterLocation` only when `request.ZoneId` equals `Context.Items["CurrentZoneId"]`. The
  border case that stays the handler's job is the exit node: its `TargetMapId` is not in the request.
- **Existing rows are swept once at startup.** `IEnemyInstanceStore.RemoveForeignZoneGroupRowsAsync` deletes rows
  whose group names no zone that owns their location, called from `Program.cs` beside `WorldCanonSweep`. It never
  deletes on absence: a slug the content catalog no longer holds cannot be judged, so the row is left alone but
  **reported** — one `LogWarning` naming each distinct `{group}/{slug}` pair it could not judge. A deactivated
  location lands in the same bucket, because the map is built from active locations only. The
  zone-per-slug map comes from `IZoneLocationRepository`, because the location → zone mapping is content and the
  store only sees the application DB. Instance-scoped rows survive, since `zone:{zoneId}_{mode}#i1:{run}` still
  names its zone.
- **`ZoneGroupName.BelongsToZone` is the only reader of the zone part** (shared, difficulty-scoped, or instance
  form). It splits the name at `#i1:` and accepts an exact zone id or the zone id followed by `_`; a name that does
  not parse belongs to no zone. Zone ids in the seeded world are hyphenated (`crestfall`, `the-droveway`,
  `grevenmire`), so the underscore rule is unambiguous today.
- **Left untouched on purpose:** `NavigateToLocationHubCommand` still hands its caller-supplied group straight to
  the store, but nothing in the web client calls it (the parity suite records locations unlocking server-side), so
  it is a legacy path rather than a second source of debris. The a1-grevenmire journey and
  `tests/encounters.e2e.ts` still read the roster as the union of both groups; do not "fix" them back to one.
