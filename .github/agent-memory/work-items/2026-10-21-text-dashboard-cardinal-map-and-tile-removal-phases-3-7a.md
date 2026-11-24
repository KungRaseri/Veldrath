---
kind: work-item
date: 2026-10-21
area: content
title: "Text dashboard, cardinal map, and tile removal (Phases 3–7a)"
---

### Session-45 (2026-10-21) — Text dashboard, cardinal map, and tile removal (Phases 3–7a)

**Commits**
- `9625d071` — text-first dashboard, cardinal map, and overlay conversion (phases 11–18).
- `f40f761a` — remove tile navigation from server, client core, and Blazor UI (phase 19a).

**Client core**
- `MapPayloads.cs`: `MapGraphReceivedPayload`, `NodeTraveledPayload`, `TravelSpawnedEnemy`.
- `IGameStateService`/`GameStateService`: `CurrentMapId`, `CurrentNodeId`, `CurrentMap`,
  `AvailableMaps`, `ApplyMapGraph`, `ApplyNodeTraveled`; `ZoneEnteredPayload` and
  `CharacterSelectedPayload` carry optional map/node ids.
- `MapHubCommands.cs`: `RequestMapGraphAsync`, `TravelToNodeAsync`.

**Blazor RCL**
- Text primitives: `TextPanel`, `StatRow`, `StatTable`, `EntityList`, `ItemTable`, `DetailPane`,
  `Models/TextUiModels.cs`; `game.css` now holds the full text UI and cardinal map CSS.
- Shell: responsive `game-shell` grid; text header/footer/sidebars; `Game.razor` renders
  `CardinalMap` + `MapDetailPanel` whenever `CurrentMap` is loaded.
- `Layout/CardinalGraphLayout.cs`: authored positions, cardinal BFS, spiral overlap avoidance.
  `CardinalMap.razor(.cs)` supports arrow-key node selection and Enter travel.
- Overlays: inventory/shop/journal/character sheet use tables + detail panes; shop uses
  `GetDisplayName()` (not `GetShortName()`, which is empty when `BaseName` is unset).
- `ActionBar` hotbar is now an abilities table with keybind text, status text, Material icons,
  and a Use button; no emoji.
- Component test suite: 80 passing (10 map state, 9 layout, 9 cardinal map, 11 text foundation,
  10 overlay, 10 hotbar, 8 combat, plus page tests).

**Server / tile removal (phase 19a)**
- Deleted hub commands and handlers: `MoveCharacter`, `GetZoneTileMap`, `GetRegionMap`,
  `MoveOnRegion`, `ExitZone`, `ChangeRegion`; removed their `GameHub` methods and request DTOs.
- Removed `ITileMapRepository` DI registration, `EnemyAiService`, tile enemy spawning, and
  `ZoneEntitiesSnapshot` broadcasts from `GameHub`.
- `SelectCharacter`/`EnterZone` now persist only map/node position; tile spawn/unstick logic gone.
- `IZoneEntityTracker`/`ZoneEntityTracker` are retained only because `InspectEntity` and
  `TalkToNpc` handlers still read their entity list (no producer remains — replace with a
  node-scoped NPC/enemy source in phase 7b).
- Removed `ZoneTileMap`, `PlayerTileX/Y`, `RegionState`, tile apply methods, and tile payload
  subscriptions from `GameClient.Core`; `OccupantInfo` and `EnemyInfo` no longer carry tiles.
- Deleted Blazor `GameTilemap`, `GameMap`, `Tile`, `TileInfoCard`, `TileStatusBar` and tests.

**Verification**
- `dotnet build Realm.Full.slnx` — 0 warnings / 0 errors.
- Full suite with coverage — **4,877 passing** across 13 projects (Server 759, Client 768,
  Core 1,955, Shared 825, Data 265, Components 80, and others).

**Still remaining (phase 7b)**
- Avalonia native tile stack: `TilemapViewModel`, `RegionTilemapViewModel`, `MapViewModel`,
  `MapRegionGroupViewModel`, `ZoneLocationPanelViewModel`, `TileDescriptionService`,
  `Views/Map/*`, `ZoneLocationPanelView`, related `GameViewModel` handlers/commands, tests.
- Delete `Veldrath.Contracts/Tilemap`, RealmEngine tile query/repo, `TiledMap` models,
  `TmxToMapGraphImporter` and fixtures, tilemap assets/manifests/TMX files.
- Drop `Character.TileX/TileY/TileZoneId`, `PlayerSession.TileX/TileY`, tile repository
  methods, and the compatibility tile write in `TravelToNodeHubCommand`.
- Mark older tilemap plans superseded; runtime smoke tests on web and desktop hosts.

---
