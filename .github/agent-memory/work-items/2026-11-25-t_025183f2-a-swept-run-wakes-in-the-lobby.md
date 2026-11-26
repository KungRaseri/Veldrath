---
kind: work-item
date: 2026-11-25
area: engine
card: t_025183f2
title: "A swept run wakes the reconnecting character in the zone's lobby, with the sentence the plan promises"
---

### Session t_025183f2 (2026-11-25) — A swept run wakes you in the lobby, and says so

Follow-up to [`2026-11-25-t_c31b96fb-walking-out-of-a-run-starts-the-grace.md`](2026-11-25-t_c31b96fb-walking-out-of-a-run-starts-the-grace.md)
and [`2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md`](2026-11-24-t_87a63cd7-the-run-pointer-resumes-on-reconnect.md).
Those two made a reconnect re-enter the run the character was inside. Neither answered the other half of
[`plans/dungeons.md`](../../../plans/dungeons.md) F2.4's reconnect paragraph: **if the instance was swept, they wake in
the dungeon's lobby with a message.** What shipped said nothing and left the character standing on the room node they
logged out on — in the shared scope, next to a roster that is not theirs, in a room with no run behind it.

**What changed**

- `GameHub.EnterZone`'s resume branch, after `FindResumableAsync` returns no open run, asks
  `ResolveSweptInstanceLobbyAsync` for the zone's lobby and moves the resume point there. Four things have to hold,
  each of which is a reason to leave the save alone instead: the location the character stood on must be
  `IsInstanced` and in this zone (the same test `EfCoreZoneInstanceService.FindResumableAsync` applies before it looks
  for a run), the zone must have a location with `IsSafeRespawn`, and that location must have a `Location` node on the
  zone's own map. The persisted pair moves with it: the existing
  `ICharacterRepository.UpdateMapPositionAsync` call at the end of `EnterZone` writes the lobby's node and slug, so the
  character row and the `ZoneEntered` snapshot cannot disagree.
- The player is told once, after `ZoneEntered` (the client logs "Entered <zone>." from that payload and appends the
  system line after it), with `GameHub.ExpiredInstanceMessage` — a new private const that
  `LeaveExpiredInstanceAsync` now also reads, so the respawn path and the reconnect path cannot word one event two
  ways.
- `GameHub` gained an optional `IZoneLocationRepository` (`_locations`), trailing like `zoneInstances` so every
  positional `new GameHub(...)` in the test projects still compiles; `HubHarness.Create` passes its `locations` mock to
  both the hub and the run service.
- Deliberately not moved: a room that was never instanced, a zone with no safe location, and a lobby with no node all
  keep the saved node and stay silent. A login must not fail, and it must not announce a move it did not make.

**Tests** — four new `GameHubZoneTests` cases beside the two existing `EnterZone_Reconnect_*` ones:
`..._Wakes_In_The_Zones_Lobby_When_The_Run_Emptied` (node, character row, one `SystemMessage`, no `ZoneInstanceId`,
shared group), `..._Wakes_In_The_Lobby_When_The_Run_Was_Purged` (no run row at all, so the path cannot depend on
reading one), `..._Keeps_The_Saved_Node_When_The_Zone_Has_No_Lobby`, and `..._Stays_In_A_Room_That_Was_Never_Instanced`.
The pre-existing `..._Keeps_The_Shared_Zone_When_The_Run_Has_Closed` now states that its zone has no lobby, which is
why it still asserts the group only.

**Negative controls, one at a time on the committed tree.**

- The lobby swap replaced by `_ = wake;` (lobby resolved, position untouched): 2 failed / 4 passed — exactly the two
  wake-in-lobby cases, both on `CurrentNodeId`.
- The `SystemMessage` send removed: 2 failed / 4 passed — the same two, on the message assertion only, so the node and
  the wording are pinned separately.
- The `IsInstanced` guard dropped from the helper: exactly `..._Stays_In_A_Room_That_Was_Never_Instanced` failed.
- The missing-lobby `return null` replaced by the map's entry node: exactly
  `..._Keeps_The_Saved_Node_When_The_Zone_Has_No_Lobby` failed.

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors), `dotnet test Veldrath.Server.Tests`
(2008 passed, 0 failed; 2004 before), `dotnet format Veldrath.Server.slnx --verify-no-changes` (exit 0),
`node scripts/memory/memory-index.mjs --check`, `node scripts/check-markdown-links.mjs`. No web file was touched: the
client already renders `ZoneEntered`'s `CurrentNodeId` and appends `SystemMessage` to the log
(`session.svelte.ts:774`, `:1274`), so the wake-up needs no client change.

**Reachability, stated plainly.** No seeder or content pack sets a location `IsInstanced`, so on shipped content no
room has a run and this path cannot fire — same as its two parents. This is code with tests until content marks a room
instanced; `docs/design/content/wound-spine.md` still records that gap.

**Noticed and deliberately not changed.** The lobby lookup is two catalog reads (`GetBySlugAsync` for the room, then
`GetByZoneIdAsync` for the safe location) because `GetByZoneIdAsync` filters hidden locations and a hidden dungeon
would not be in it — the same reason `FindResumableAsync` reads the room by slug. A hidden *lobby* would therefore not
be found, which is the fallback that keeps the saved node rather than an error.
