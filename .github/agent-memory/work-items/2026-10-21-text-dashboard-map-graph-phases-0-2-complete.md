---
kind: work-item
date: 2026-10-21
area: map
title: "Text dashboard map graph: Phases 0–2 complete"
---

### Session-44 (2026-10-21) — Text dashboard map graph: Phases 0–2 complete

**Schema + persistence**
- `RealmEngine.Data/Entities/Content/MapGraph.cs`: `Map`, `MapNode`, `MapEdge` (string enums stored via
  `HasConversion<string>()`; unique `(MapType, OwnerId)` and `(MapId, FromNodeId, Direction)`).
- `ContentDbContext` DbSets + `ContentModelConfiguration`; entities only exist in `ContentDbContext`.
- Added `Character.CurrentMapId/CurrentNodeId` and `PlayerSession.CurrentMapId/CurrentNodeId`.
- Migrations: `20261021172256_AddMapGraph` (Data), `20261021172306_AddCharacterMapPosition` (Server).
- `IMapGraphRepository` / `EfCoreMapGraphRepository`; DI registration in `Veldrath.Server/Program.cs`.

**Importer + seeder** — the importer this session built has been deleted; the seeder is hand-authored and is the
source of truth. Counts below re-derived from the seeders and the seeded graph at `development` `79deae37`
(2026-11-24), not carried forward from the session-44 snapshot (that one said 23 / 130 / 196 and is superseded).
- **`TmxToMapGraphImporter` is gone**: deleted in `dff88e83` (phase 7b, 2026-10-22) with its tests, fixtures and
  the whole TMX asset tree. No map-graph importer ships. `RealmEngine.Data/Import/` holds only
  `MapGraphImportModels.cs` (catalog/report records), and the only remaining reference to those types is
  `RealmEngine.Data.Tests/DataLayerStubAndReportTests.cs` — nothing produces a `MapGraphImportCatalog` any more.
  `RealmEngine.Data/Authoring/ContentPacks/ContentPackImporter.cs` is the content-pack importer and unrelated.
- `RealmEngine.Data/Seeders/MapGraphSeeder.cs` authors the graph directly, plus a seed-time walk chain
  (`ConnectZoneEntriesToLocations`) and the hidden-node bypass (`ConnectVisibleLocationsAcrossHiddenNodes`) that
  add the remaining `Walk` edges. **The totals are not restated here:**
  `RealmEngine.Data.Tests/Seeders/MapGraphSeederTests.cs` asserts the map, node and edge counts and
  idempotency. This paragraph used to restate the numbers and drifted from the test twice; the test is the source.
- Nodes by type: `EntryPoint` 23, `Zone` 18, `RegionExit` 17, `Location` 67, `ZoneExit` 11. Eighteen `Zone` nodes
  for nineteen zone maps because `playground` (`IsDevOnly`) sits on no region map.
- Edges by type once seeded: `Walk` 246, `ExitToRegion` 35, `ExitToZone` 25 — 212 authored plus 94 generated.
- **A live zone being targeted is not the same as being walkable into.** `LinkRegionZonesToZoneMaps` gives every
  region `Zone` node a `TargetMapId`, so `map_unreachable` used to stay silent for a zone whose only inbound target
  is its region node — the region board is not a through-route: `region:varenmark`'s entry node is adjacent to
  `zone:crestfall` alone, and every `Zone` node on the board enters its zone. The Drowning Pits was unreachable on
  foot until `zone:grevenmire:exit:zone:drowning-pits` (a `ZoneExit` on the Sunken Estate, the same shape as
  `ashlen-wood → grevenmire`) landed in t_a39b46eb. Since t_53311432 the rule does not count a region board's
  `Zone` node as an inbound exit, so it warned on the 14 seeded zones that still had no door — `the-halrow` among
  them — and `MapGraphSeederTests.SeedAsync_EveryLiveZoneCanBeEnteredThroughADoorRatherThanItsRegionNode` pins that
  list and may only shrink, and the list itself lives in the test. t_835e7914 gave the Halrow the same repair the Pits got
  (`zone:the-droveway:exit:zone:the-halrow` on the Weathered Waypost), so the test's
  row for it goes with it — a stale row would pass silently and stop catching the next zone that loses its door.
- The chain is per zone map: entry → each `Location` node in `SortOrder`, one hop each way — 134 hop directions
  over the 19 zones, two for each of the 67 location nodes. 39 of those directions already existed as authored
  edges and 5 were dropped because the source node already used all four cardinal directions (`zone:crestfall` 2,
  `zone:playground` 3), so the chain adds 90 `Walk` edges. `ConnectZoneEntriesToLocations` (`:86-106`).
- **The chain is no longer the only generated pass.** `ConnectVisibleLocationsAcrossHiddenNodes` (`:140-175`) runs
  immediately after it, because 19 of the 67 `Location` nodes are hidden and the chain links a hidden node like
  any other: a hidden location between two visible ones cuts the rest of the chain off until a player happens to
  unlock it. For each visible location that Arrival cannot reach while the previous visible location can, it adds
  one bidirectional visible `Walk` pair between those two nodes, and only when both directions are free
  (`FirstFreeDirection`; a one-way bypass would walk the player past the hidden node and strand them there).
  Reachability is recomputed after each pair, so a run of several hidden nodes still costs one pair. The seeded
  graph holds exactly two pairs:
  `zone:grevenmire` `entry ↔ location:mire-crossing` (South / North) and `zone:ashlen-wood`
  `location:old-clearing ↔ location:wood-road` (North / West). That is 4 edges, so 94 generated edges in total,
  and they land on exactly two maps: counted after the chain, `zone:ashlen-wood` moves 14 → 16 edges and
  `zone:grevenmire` 16 → 18. It is additive: nothing is removed, and no `SortOrder`, `IsHidden`,
  unlock metadata or authored edge changes. The authored edges it reasons over are uniform today — none of the
  306 edges is hidden or non-traversable.
- `LinkRegionZonesToZoneMaps` (`:47-66`) sets `TargetMapId = zone:<RefId>` on every region `Zone` node, because
  `TravelToNodeHubCommand` only loads another map when the node names one.
- **The reachability claim that matters is the client's own walk, not a BFS over every seeded edge.** A plain BFS
  passed while three visible locations could not be entered: it walks *through* the hidden nodes that the client
  neither renders nor lets you select. What holds instead is that every visible location is walkable from Arrival
  when the walk skips hidden nodes and does not expand a node carrying a `TargetMapId` — an exit, portal or zone
  node leaves the map rather than continuing on it. `MapGraphSeederTests.SeedAsync_EveryVisibleZoneLocationIsWalkableFromArrival`
  asserts that across all 19 zone maps, and it was RED for exactly `ashlen-wood:wood-road`,
  `grevenmire:mire-crossing` and `grevenmire:sunken-estate` before the bypass pass landed.
- `ZoneLocationsSeeder.cs`: **67 `ZoneLocation` rows over 19 zones** — 37 `TypeKey = "locations"`, 17
  `"environments"`, 13 `"dungeons"`. `playground` holds 13 and `soddenfen` 2; every other zone 3, except
  `crestfall` with 4.
- `ZoneLocationsConnectionSeeder.cs`: **11 `ZoneLocationConnection` rows, Crestfall only** (8 `walk`, 2 `exit`,
  1 `region_exit`) over four location slugs. **No handler or repository reads that table** — in-zone movement is
  the map graph's `Walk` edges, so these rows are inert seed data.

**Server navigation**
- `GetMapGraphHubCommand`: resolves current map (or explicit), filters hidden nodes/edges using
  `ICharacterUnlockedLocationRepository`, always includes the current node, returns map summaries.
- `TravelToNodeHubCommand`: validates current map/position, visibility, direct edge, and exit target;
  updates Character + PlayerSession via new `UpdateMapPositionAsync` methods; sets zone/region context;
  updates tile coords from node layout for zone maps; runs passive discovery and location roster spawn.
- `GameHub`: `GetMapGraph(string? mapId)` → `MapGraphReceived`; `TravelToNode(TravelToNodeHubRequest)` →
  `NodeTraveled`. `SelectCharacter` sets region entry; `EnterZone` sets zone entry.
  Old tile methods are deprecated in XML remarks only (keep until TMX removal).

**Test baselines**
- `RealmEngine.Data.Tests`: 265 passing (9 validator, 9 importer incl. real TMX asset, 3 seeder).
- `Veldrath.Server.Tests`: 806 passing (10 GetMapGraph, 10 TravelToNode).
- `Veldrath.Server.slnx`: 0 warnings / 0 errors.

**Next**: Phase 3 client core (`IGameStateService`/`GameStateService` map state, hub subscriptions),
then Phases 4–6 UI, then Phase 7 TMX removal.

---
