---
kind: work-item
date: 2026-11-23
area: map
card: t_990e7186
title: "Respawn moves the map node, so a reload wakes where the player did"
---

### Session-D4-t_990e7186 (2026-11-23) — Respawn moves the map node, so a reload wakes where the player did

`RespawnHubCommandHandler` wrote `CurrentZoneLocationSlug` and left `CurrentNodeId` at the node the character fell on.
`SelectCharacter` re-projected the row onto the region entry on the next login, so `EnterZone` resolved a node that did
not exist in the zone graph and fell back to the arrival node: the player who respawned at the safe location came back
at the zone gate after a reload.

- **Respawn now writes the whole position.** It resolves the safe location's map node from
  `IMapGraphRepository.GetMapForZoneAsync` (by `NodeType == Location` + `RefId`, not by a `zone:x:location:y` string
  guess — the Foundry editor writes its own node ids) and calls `UpdateMapPositionAsync(mapId, nodeId, zoneId, slug)`.
  A zone with no graph, or a location with no node, keeps the old slug-only write and logs an `Error`.
- **The location slug survives the login projection.** `SelectCharacter` still re-points the row at the region entry,
  but it now passes `CurrentZoneLocationSlug` through instead of nulling it. `EnterZone` resolves that slug back to its
  location node, so a reload — and the tutorial handoff, which also writes a slug without a node — resumes where the
  server left the character.
- **The live client follows the node.** `CharacterRespawned` in `session.svelte.ts` re-reads `GetMapGraph(null)` when
  the payload names a location; without it the map kept drawing the character on the node they fell on while the server
  (and the sheet) had already moved them, so the next click could be a node the server refuses.
- **Zone maps are connected, so the A1 specs stay green.** `MapGraphSeeder.ConnectZoneEntriesToLocations` chains
  `entry → location(sortOrder) → …` and bridges visible locations across a hidden one, which is why a respawn at the
  fallback location (Ashlen Wood has no visible `SafeRespawn`; `strangled-oak` is hidden, so the degraded path picks the
  first visible location) can still walk back to the encounter.
- Tests: `RespawnHubCommandHandlerTests.Handle_Moves_The_Map_Node_To_The_Safe_Locations_Node`,
  `GameHubZoneTests.EnterZone_Resumes_The_Location_Node_A_Login_Projected_Onto_The_Region_Map`,
  `GameHubZoneTests.SelectCharacter_Keeps_The_Zone_Location_Across_The_Region_Projection`; all three fail on the old
  code. The A1 Grevenmire spec reads `CurrentNodeId` and the `MapNodes` row back from Postgres after the respawn and
  asserts the map draws the safe location as current before walking out of it.
