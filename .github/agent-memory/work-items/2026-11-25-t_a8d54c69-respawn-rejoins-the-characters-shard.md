---
kind: work-item
date: 2026-11-25
area: engine
card: t_a8d54c69
title: "A respawn out of a gone run rejoins the character's difficulty shard, and sends the snapshot the walk sends"
---

### Session t_a8d54c69 (2026-11-25) — Waking out of a gone run is an arrival like any other

Follow-up to [`2026-11-25-t_a0369099-a-roster-is-the-shard.md`](2026-11-25-t_a0369099-a-roster-is-the-shard.md),
which noticed this path while fixing the wilderness roster, and to
[`2026-11-25-t_375207b7-a-scope-change-inside-one-zone-is-an-arrival.md`](2026-11-25-t_375207b7-a-scope-change-inside-one-zone-is-an-arrival.md),
which decided that a scope change inside one zone owes a snapshot and a membership event.

**The defect.** `GameHub.LeaveExpiredInstanceAsync` runs on the respawn path (`Respawn`) when the character's run was
purged or swept while they were dead. It cleared the run pointer and put the connection back into the shared zone with
`ZoneGroupName.ForZone(zoneId)` — no difficulty argument. A wilderness shared scope is
`zone:{zoneId}_{difficulty}` (`ZoneGroupName.ForZone(zoneId, difficultyMode)`, composed by `GameHub.ComputeZoneGroup`
and by `TravelToNodeHubCommand.SharedZoneGroup`), so a character resurrected out of a swept run in `the-droveway`
joined `zone:the-droveway`: a group nobody else is in. Their `PlayerEntered` never reached their shard, the shard's
chat and roster never reached them, and `Context.Items["CurrentZoneGroupName"]` named a scope the character did not
share with anybody until the next `EnterZone`. The method also sent only a `SystemMessage`, so the client kept the
run's occupant list — players it could no longer see, chat with or fight beside.

**The fix.**

- The group is composed through `ComputeZoneGroup(zoneEntity?.Type, zoneId, CurrentDifficultyMode())` — the same
  pair `EnterZone` composes its group from and `ReadOwnScopeAsync` reads its roster with. The zone row supplies the
  type; a zone the catalog does not know resolves to a bare scope, which is the same convention the roster read
  already used, so nothing throws on a stale `CurrentZoneId`.
- `SharedScopeDifficulty` and `ComputeZoneGroup` take `ZoneType?` now. That is what made the bug writable: the roster
  read had an inline `zoneType is { } type ? … : null` that a caller without a resolved zone could not use, so
  `LeaveExpiredInstanceAsync` wrote its own rule instead of calling the helper. `ReadOwnScopeAsync` collapses to the
  plain call.
- The snapshot is now sent. This answers the card's open question explicitly: yes, the respawn wake carries one. The
  wake is a scope change inside one zone, which is exactly the event the parent card decided is an arrival — the
  client rewrites its occupant list only from `ZoneEntered`, and the list it kept was the run's. The node comes from
  the character row, where `RespawnHubCommandHandler.MoveToLocationAsync` has already written the safe location the
  character woke at.
- The `ZoneEntered` payload moved into one `SendZoneSnapshotAsync(Zone, mapId, nodeId)`. All three senders — the
  `EnterZone` arrival, the walk in `SyncArrivedZoneAsync`, and the respawn wake — now describe a zone the same way,
  which is the structural link the review of the parent card asked for instead of a third spelling of the same
  anonymous object. `EnterZone` also stopped reading the roster against the requested zone id and reads against
  `zone.Id`, the row's canonical spelling.

**Tests** (`Veldrath.Server.Tests/Features/GameHubZoneTests.cs`, new section "Respawn: waking out of a run that is
gone", plus a `SeedRespawnOutOfARunAsync` helper that seeds the dead character, its session and run pointer, and one
neighbour on each side of the split):

- `Respawn_Out_Of_A_Gone_Run_Rejoins_The_Characters_Wilderness_Shard` — the card's reproduction: a hardcore character
  in `the-droveway` whose run is `Empty` on respawn joins `zone:the-droveway_hardcore` and not `zone:the-droveway`,
  the shard receives the `PlayerEntered`, the other shard receives nothing, and the `ZoneEntered` roster is the shard
  (mate and caller) rather than the normal-difficulty neighbour.
- `Respawn_Out_Of_A_Gone_Run_Keeps_A_Town_Scope_Whole` — the wilderness-only half: a hardcore character in
  `crestfall` whose run is `Purging` joins `zone:crestfall`, and the roster holds both difficulties.
- `Respawn_Inside_A_Run_That_Is_Still_Open_Keeps_Its_Group_And_Stays_Silent` — the guard on the other side: an `Open`
  run is not a scope change, so no group move, no snapshot, no message, and the pointer stays.

**Negative controls, one at a time on the working tree (each restored from a copy afterwards).**

- Group composition reverted to `ZoneGroupName.ForZone(zoneId)`: 1 failed / 2 passed. The failure is the card's own
  symptom — `Expected groups.AddedGroups {"zone:the-droveway"} to contain "zone:the-droveway_hardcore"` — and the town
  case passes because a town's scope has no suffix to lose.
- Snapshot suppressed (`if (zoneEntity is not null && false)`): 2 failed / 1 passed, both at
  `Single(m => m.Method == "ZoneEntered")` with `Sequence contains no matching element`; the open-run case still
  passes, so the snapshot assertion is load-bearing on its own and the two fixes are independent.

**Gates** — `dotnet build Veldrath.Server.slnx` (0 warnings, 0 errors), `dotnet test Veldrath.Server.slnx`
(6651 passed / 0 failed: Shared 1158, Core 2748, Data 697, Server 2048 — the three new cases are the difference),
`dotnet format Veldrath.Server.slnx --verify-no-changes` (exit 0), `node scripts/memory/memory-index.mjs --check`,
`node scripts/check-markdown-links.mjs`.

**No web payload change.** `ZoneEntered` is an event the client already handles and its shape is unchanged; the
server only sends it on a path that used to be silent, so `types.ts` does not drift and no parity allow-list entry is
needed.

**Reachability — unchanged, and deliberately not widened.** This still needs a location with `Traits.IsInstanced`,
and no seeder sets one (see `areas/session-29-2026-04-08-region-map-architecture-playersession-migration.md`), so the
path is wired but not live. The tests reach it by seeding the run and the session pointer directly, the same way the
swept-run reconnect tests do.

**Noticed and deliberately not changed.**

- The wilderness rule is still spelled in two places: `GameHub.SharedScopeDifficulty`, which now feeds both the group
  name and the roster read, and `TravelToNodeHubCommand.SharedZoneGroup`, which composes the same name in the command
  layer where the hub helper is not reachable. Moving it beside `ZoneGroupName.ForZone` is the structural link the
  parent card's review asked for, but it touches the travel command, outside this card's Scope.
- The client's `ZoneEntered` handler writes "Entered {name}." every time it fires, so a respawn wake now logs that
  line for the zone the player never left. Already filed as its own card when the parent change made the same noise
  on a descent; it is a client-side wording fix.
- `Respawn` still calls `LeaveExpiredInstanceAsync` before it sends `CharacterRespawned`, so the snapshot and the
  "instance has closed" sentence reach the client before "You have respawned at …". That ordering predates this card
  (the sentence was already sent first) and the client renders both without depending on the order.
