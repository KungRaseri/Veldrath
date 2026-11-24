---
kind: work-item
date: 2026-10-22
area: content
title: "Phase 7b complete: TMX layer fully removed"
---

### Session-46 (2026-10-22) — Phase 7b complete: TMX layer fully removed

**Deleted from the engine/contracts**
- `Veldrath.Contracts/Tilemap`, `GetTileMapQuery`, `ITileMapRepository`,
  `CompositeITileMapRepository`, `InMemoryTileMapRepository`, `TiledFileMapRepository`,
  `TiledMap` + `TiledMapGameExtensions`, `DungeonGenerator`, `TmxToMapGraphImporter`,
  and their test files.
- `Veldrath.Assets/Manifest/TilemapAssets.cs` and the whole
  `Veldrath.Assets/GameAssets/tilemaps` tree (22 TMX maps, TSX/PNG sheets, Tiled project).
- TMX generation/scan scripts (`generate-zone-tmx.ps1`, `_gen_varenmark_zones.py`,
  `generate-onebit-labeled.ps1`, `scan-tiles.ps1`).

**Avalonia client**
- Removed `TilemapViewModel`, `RegionTilemapViewModel`, `MapViewModel`,
  `MapRegionGroupViewModel`, `ZoneLocationPanelViewModel`, `TileDescriptionService`,
  `Views/Map/*`, `ZoneLocationPanelView`, and 5 tile/region test files.
- `GameViewModel` now exposes `CurrentMap`, `CurrentNodeId`, `CurrentMapName`,
  `MapPositionText`, `MapNodes`, `SelectedMapNode`, `RefreshMapCommand`,
  `TravelToSelectedNodeCommand`, `ApplyMapGraph`, `ApplyNodeTraveled`.
- `CharacterSelectViewModel` subscribes to `MapGraphReceived`/`NodeTraveled` and sends
  `GetMapGraph` after `EnterZone`; the Blazor `Game.razor` page is used through WebView2.
- Native fallback centre panel is a text ListBox of map nodes with a Travel button;
  footer “Mini-map” replaced with a Map refresh button.

**Server/schema**
- Dropped `Character.TileX/TileY/TileZoneId`, `PlayerSession.TileX/TileY`,
  `PlayerSession.LastMovedAt`, plus `UpdateTilePositionAsync`, `UpdatePositionAsync`,
  `UpdateLastMovedAtAsync`, `SetZoneAsync`, `SetRegionAsync`.
- Migration `20261022142119_RemoveTilePositionFields` (ApplicationDbContext).
- Removed tile writes from `NavigateToLocationHubCommand` and `TravelToNodeHubCommand`.
- Removed `TalkToNpc`/`InspectEntity` hub methods and handlers plus `ZoneEntityTracker`
  (their only producer was the removed tile spawner). Re-introduce them later with a
  node-scoped NPC/enemy source.
- `LastSessionDto` now carries `CurrentMapId`/`CurrentNodeId` instead of `TileX`/`TileY`.

**Verification**
- `Realm.Full.slnx`: 0 warnings / 0 errors.
- Full suite: **4,725 passing**, 0 failed (Server 759, Client 663, Core 1,944,
  Shared 803, Data 253, Components 80, others 213).
- Runtime: Postgres container healthy; server `/health` 200; database has
  23 maps / 130 nodes / 196 edges and **0** tile columns; web `/`, `/Game`,
  `/Game/Play`, `/health` all 200; Avalonia client ran 25 s without error.
- Grep check confirms no `Tilemap`, `TiledMap`, `.tmx`, `ZoneEntityTracker`,
  `TalkToNpc`, or `InspectEntity` references remain outside historical migrations/plans.

**Remaining**: Phase 9 optional RealmForge map graph editor and JSON import/export.

---


---
