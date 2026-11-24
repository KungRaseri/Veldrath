---
kind: area
date: 2026-04-08
area: engine
title: "Session-29 (2026-04-08) — Region Map Architecture + PlayerSession Migration"
---

## Session-29 (2026-04-08) — Region Map Architecture + PlayerSession Migration

> **Half of this note is now history.** The `PlayerSession` replacement of `ZoneSession` landed and is still
> the live session entity. The region-map half — a TMX map with zone-entry objects, tile coordinates and a
> `RegionTilemapViewModel` — was removed on 2026-10-22 (session-46): `TiledFileMapRepository`, the TMX assets,
> the tile layers and `PlayerSession.TileX`/`TileY` all went; the drop shipped as migration
> `20261022142119_RemoveTilePositionFields`.
> The removal is recorded in
> [`work-items/2026-10-22-phase-7b-complete-tmx-layer-fully-removed.md`](../work-items/2026-10-22-phase-7b-complete-tmx-layer-fully-removed.md).
> Travel is node-based over the database map graph instead (`GameHub.GetMapGraph` / `TravelToNode`, drawn by
> `web/client/src/lib/game/CardinalMap.svelte`), and the client the note names — `CharacterSelectViewModel`,
> `GameViewModel`, `RegionTilemapViewModel` — was deleted with the Avalonia client in session-68 (2026-10-25).

### Design decisions (locked)
- Characters navigate a **region TMX map** between zones; entering a zone is triggered by walking to a zone-entry tile object
- Zone positions come from the TMX **object layer** (named `zones`), NOT from DB columns
- `ZoneSession` entity replaced by `PlayerSession` — tracks position at both region-map and zone level
- User authors TMX files manually; agent defines the spec

### PlayerSession Entity (`Veldrath.Server/Data/Entities/PlayerSession.cs`)
Replaces `ZoneSession`. New properties vs old:
- `RegionId` (`string`, required) — always set; FK → Regions with Restrict delete
- `ZoneId` (`string?`, nullable) — null = player is on the region map, not inside a zone; FK → Zones with SetNull delete
- `TileX`, `TileY` (int) — current tile coords (region map or zone) — **dropped 2026-10-22**, see the banner
- All prior fields kept: `CharacterId`, `CharacterName`, `ConnectionId`, `EnteredAt`, `LastMovedAt` — `LastMovedAt` was dropped with the tile columns
- Unique indexes on `CharacterId` and `ConnectionId`

**Fields today** (read from the entity): `Id`, `CharacterId`, `CharacterName`, `ConnectionId`, `RegionId`,
`ZoneId`, `CurrentMapId`, `CurrentNodeId`, `ZoneInstanceId`, `EnteredAt`. Position on the map graph is the
`CurrentMapId` / `CurrentNodeId` pair. `ZoneInstanceId` is the run pointer for the zone instance the character is
inside, `null` in the shared zone: `GameHub.ApplyArrivalGroupAsync` writes it when a travel resolves into a run and
clears it on a shared-zone arrival, `EfCoreZoneInstanceService` writes and clears it on run join and leave, the run's
chests and floor loot are scoped by it (`ChestHubCommands`, `GroundItemHubCommands`), `LeaveCurrentZoneAsync` reads
it to close the character's participation, and admin counts occupants by it. Enemy rows carry the pointer so a run's
roster cascades away when it is purged, but the roster itself is read by zone group and location slug
(`EfCoreEnemyInstanceStore.LoadLocationAsync`). A reconnect does **not** carry it, in two steps: `onreconnected` calls
`restoreSession()`, which calls `SelectCharacter`, and that removes the stale session and inserts a fresh one with
`ZoneId = null` and no run pointer at `ResolveResumeNodeId`; the `CharacterSelected` reply then carries
`Character.CurrentZoneId`, and the shell answers it with `if (payload.currentZoneId) await this.enterZone(...)`
(`session.svelte.ts:1404`). `GameHub.EnterZone` replaces that session with one carrying `ZoneId` set, the saved
location node and still no run pointer, so a character who dropped inside a zone comes back inside that zone in its
shared scope; only one who stood on the region map stays there. Two doc comments still promise otherwise — the
`ZoneInstanceId` summary in `PlayerSession.cs` and the summary above `ApplyArrivalGroupAsync` — and the bodies do
not. `ZoneSession` exists nowhere.

### IPlayerSessionRepository (in `IZoneRepository.cs`)
Replaces `IZoneSessionRepository`. Same core methods plus new:
- `GetByRegionIdAsync(string regionId)` — all sessions in a region
- `GetOnRegionMapAsync(string regionId)` — sessions where `ZoneId is null`
- `UpdatePositionAsync(Guid characterId, int tileX, int tileY)` — update tile coords only — **removed 2026-10-22**
- `SetZoneAsync(Guid characterId, string? zoneId)` — enter/exit zone (null = back to region map) — **removed 2026-10-22**

**The interface today**: `GetByZoneIdAsync`, `GetByRegionIdAsync`, `GetOnRegionMapAsync`, `GetByConnectionIdAsync`,
`GetByCharacterIdAsync`, `GetByCharacterNameAsync`, `AddAsync`, `RemoveAsync`, `RemoveByConnectionIdAsync`,
`UpdateMapPositionAsync(characterId, mapId, nodeId, zoneId, regionId)` and `SetZoneInstanceAsync(characterId, instanceId)`.
The position write is the map-graph pair; `SetZoneInstanceAsync` writes the run pointer, which scopes the run's
chests and floor loot. A reconnect does not restore it: `SelectCharacter` builds the new session without it, and
`EnterZone`, which the shell calls next because the reply carries `CurrentZoneId`, builds its session without it too.

### Repository (`PlayerSessionRepository` in `ZoneRepository.cs`)
Replaces `ZoneSessionRepository`. Implements all 11 interface methods.

### ApplicationDbContext changes
- `DbSet<PlayerSession> PlayerSessions` replaces `DbSet<ZoneSession> ZoneSessions`
- Zone config no longer has `HasMany(z => z.Sessions)` — driven from PlayerSession side with `SetNull`
- `Zone.Sessions` → `ICollection<PlayerSession>`

### Files changed (session-29)
- `PlayerSession.cs` — **created** (new entity)
- `ZoneSession.cs` — **deleted**
- `Zone.cs` — `ICollection<ZoneSession>` → `ICollection<PlayerSession>`
- `IZoneRepository.cs` — `IZoneSessionRepository` → `IPlayerSessionRepository`
- `ZoneRepository.cs` — `ZoneSessionRepository` class → `PlayerSessionRepository`
- `ApplicationDbContext.cs` — DbSet + EF config fully swapped
- `GameHub.cs` — field/ctor/9 call-sites swapped; `LeaveCurrentZoneAsync` guards `zoneId is null` (player may be on region map, not in a zone)
- `MoveCharacterHubCommand.cs` — `IZoneSessionRepository` → `IPlayerSessionRepository` (field + doc comments)
- `ZoneEndpoints.cs` — 3 endpoint lambda params updated
- `Program.cs` — DI: `IZoneSessionRepository, ZoneSessionRepository` → `IPlayerSessionRepository, PlayerSessionRepository`
- `ZoneRepositoryTests.cs` — all session tests updated; `RegionId` added to all `new PlayerSession { }` objects
- `GameHubTests.cs` — `CreateHub` + inline constructions updated; `EnterZone_Should_Create_PlayerSession_In_Database`
- **Migration**: `20260408225405_ReplaceZoneSessionWithPlayerSession.cs` in `Veldrath.Server/Migrations/`

### TestDbContext RegionId values (for session tests)
- Zone `"crestfall"` → `RegionId = "varenmark"`
- Zone `"aldenmere"` → `RegionId = "greymoor"`
- Zone `"fenwick-crossing"` → `RegionId = "thornveil"`

### Region TMX Map Spec (finalized 2026-04-08 — removed 2026-10-22 with the TMX layer)
**Tile layers** (3): `ground`, `detail`, `decoration` (no `overhead` — player always on top)  
**Object groups** (2):
- `zones` — zone entry objects, `name` = zone slug, properties: `displayName`, `minLevel`, `maxLevel`
- `region_exits` — border crossings to adjacent regions, `name` = target region slug  
**Map TMX properties**: `regionId`, `tilesetKey="onebit_packed"`, `fogMode="none"` (fully visible)  
**File path convention**: `{mapsBasePath}/{regionId}.tmx` flat — e.g. `maps/varenmark.tmx`  
**Existing file**: `maps/Varenmark.tmx` (already in assets — repurpose/rename to match convention)

### Next work — never started, and its design has been deleted

Phases 3–8 were never built. The TMX region map they were written for was removed on 2026-10-22 (session-46) —
see the banner above — so no phase here has a target left: `RegionMapDto`, `ZoneObjectDto` and
`TilemapContracts` do not exist, nor do `GetRegionMap`, `MoveOnRegion` or `ExitZone`.

- Phases 3–5 were server contracts, hub commands and hub methods for the region map.
- Phases 6–8 were a client `RegionTilemapViewModel` plus its hub wiring and tests. That client was deleted in session-68. Map travel is node-based over the database map graph instead: `GameHub.GetMapGraph` / `TravelToNode`, called from `web/client/src/lib/game/session.svelte.ts` and drawn by `web/client/src/lib/game/CardinalMap.svelte`.

### Test counts after session-29
- Server tests: **560 passing**, 8 pre-existing failures (unrelated to PlayerSession work)
- Client tests: **525 passing** (unchanged — server-only session)
