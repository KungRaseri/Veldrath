---
kind: work-item
date: 2026-11-26
area: engine
card: t_d87692e0
title: "A death inside a still-open run wakes at that run's own lobby, and the group decision comes from the same read"
---

### Session t_d87692e0 (2026-11-26) — a still-open run keeps its character and its lobby

Follow-up to [`2026-11-25-t_a8d54c69-respawn-rejoins-the-characters-shard.md`](2026-11-25-t_a8d54c69-respawn-rejoins-the-characters-shard.md),
which fixed what a *gone* run does. The other half was still broken: `GameHub.Respawn` sent
`RespawnHubCommand(characterId)` and nothing else, so the handler asked the zone for its lobby and took the first
visible `IsSafeRespawn` row. Keeping the character's SignalR group made the transport right and the destination
wrong — they stayed in the run but woke at the zone's shared anchor, which is the position/scope mismatch
`plans/dungeons.md` calls out. The plan's claim that retaining the group was enough is false at baseline; the F2
shipped-status bullet now says so and separates the two decisions.

**What changed**

- `RespawnHubCommand(CharacterId, Guid? ZoneInstanceId = null)` and `RespawnHubResult.RetainedZoneInstanceId`.
  `GameHub.Respawn` passes `Context.Items["CurrentZoneInstanceId"]` — server-side only, never client input — and acts
  on the command's single decision: same id back means leave the group and pointer untouched; supplied id with no
  retained id means `LeaveRunScopeAsync`. That helper is the old body of `LeaveExpiredInstanceAsync`
  (`GameHub.cs`) split out, so the respawn moves the scope **without** a second `GetAsync` that could disagree with
  the read the heal was based on.
- The handler injects `IZoneInstanceService` and reads exactly the supplied run. Retainable is `Open` **and** the
  run's `ZoneId` equal to the character's `CurrentZoneId` (ordinal ignore case); no `FindResumableAsync`, no
  allocate/join, no party reasoning. An ordinary death (no id supplied) never touches the run service — pinned with
  a `MockBehavior.Strict` service, which fails the test on any call at all.
- `IZoneLobbyResolver` gained a scoped overload `ResolveAsync(zoneId, characterId, bool instanced,
  IReadOnlySet<string> placedLocationSlugs, string? preferredFallbackSlug, ct)`. The existing three-argument entry
  point is now explicitly the *shared* one: it excludes `IsInstanced` rows from the lobby, from the hidden
  (`NotUnlocked`) candidate, and from the entrance fallback. Reconnect needed no change and inherits the explicit
  shared anchor (`GameHub.ResolveSweptInstanceLobbyAsync` still calls the three-argument form).
- The strict path resolves the map and node **before** healing and writes map + node + slug; the slug-only fallback
  in `MoveToLocationAsync` stays for the ordinary path only. A failed strict resolution returns
  `No valid respawn location is available.` before HP/mana, position or combat-session writes.

**Content rules this encodes** (worth repeating whenever a lobby is discussed)

- A **run lobby** is a same-zone, active location that is visible to the character (or unlocked by them) and flagged
  **both** `IsInstanced` and `IsSafeRespawn`, placed on a `Location` node of the zone map.
- A **shared anchor** is the same thing without `IsInstanced`. Runs in one zone reuse the content node; the node is
  not part of a run's identity.
- Degradation never crosses scopes. Inside a run the only fallback is the room the character fell in, and only while
  the saved `CurrentMapId`/`CurrentNodeId` still name that room; otherwise the respawn is refused rather than
  silently relocated. A locked or unplaced run lobby is content debt: logged, and said once to the player
  ("This run has no safe lobby you can use; you respawn where you fell.").
- A run that is `Empty`, `Purging` or in another zone cannot take the character: only a **mapped, non-instanced**
  anchor will do, and a zone without one refuses the respawn.

**Test mechanics that cost a cycle**

- `ZoneInstance.ZoneId` is a real FK to `Zones`, so a handler test that invents `zone-1` must seed that zone row
  (`EnsureZoneAsync` in `RespawnHubCommandHandlerTests`) — otherwise `SaveChangesAsync` fails with
  `SQLite Error 19: FOREIGN KEY constraint failed` at the run insert, not at the assertion.
- `FakeZoneLocationRepository` matches `ZoneLocationEntry.ZoneId` exactly, so the catalog's zone must equal the
  character's `CurrentZoneId`; the pre-existing handler tests happen to use `zone-1` for both.
- The harness mediator is the adapter: `Mock<ISender>` set up on
  `Send(It.IsAny<IRequest<RespawnHubResult>>(), …)` and returning `handler.Handle((RespawnHubCommand)request, ct)`
  runs the real handler with only dispatch stubbed. `HubHarness.Create(..., locations:, mapRepo:)` then wires the
  same catalog and map into the hub.
- A mutation probe that leaves unreachable code fails the build (`CS0162` is an error here), and the stale binary
  then reports the *previous* probe's failures. Check the `Build succeeded` line before reading test output.

**Evidence**

- Red first, on the destination: `respawned.LocationSlug` was `"drove-road"` against an expected `"drove-lobby"`.
- Four mutation probes, each restored by `cp` and hash-checked: forcing retention (`retainsRun = run is not null`)
  → 4 red (3 gone-run theory rows + the wilderness hub case); ignoring `preferredFallbackSlug` → 1 red
  (`Resolve_Scoped_Run_Only_Falls_Back_To_The_Preferred_Room`); dropping the `IsInstanced` scope filter → 11 red
  (resolver and handler both directions); accepting an in-run stay on the slug alone → 1 red
  (`Handle_Refuses_The_Respawn_When_The_Run_Offers_No_Destination`).

Related: [`2026-11-25-t_025183f2-a-swept-run-wakes-in-the-lobby.md`](2026-11-25-t_025183f2-a-swept-run-wakes-in-the-lobby.md)
(the gone-run wake), [`2026-11-25-t_d4617b1e-a-hidden-lobby-is-not-a-missing-one.md`](2026-11-25-t_d4617b1e-a-hidden-lobby-is-not-a-missing-one.md)
(the resolver this extends), [`2026-11-25-t_375207b7-a-scope-change-inside-one-zone-is-an-arrival.md`](2026-11-25-t_375207b7-a-scope-change-inside-one-zone-is-an-arrival.md)
(why the shared-scope move speaks).
