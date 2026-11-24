---
kind: area
date: 2026-03-21
area: content
title: "World / Region / Zone Schema (session-14 — 2026-03-21; seed facts re-derived 2026-11-20)"
---

## World / Region / Zone Schema (session-14 — 2026-03-21; seed facts re-derived 2026-11-20)

Full World → Region → Zone hierarchy implemented.

**Entities** (all in `Veldrath.Server/Data/Entities/`):
- `World.cs`: `Id` (slug), `Name`, `Description`, `Era`, `ICollection<Region> Regions`
- `Region.cs`: `Id` (slug), `Name`, `Description`, `RegionType Type`, `MinLevel`, `MaxLevel`, `IsStarter`, `IsDiscoverable`, `WorldId` FK, navigation `World`, `ICollection<Zone> Zones`, `ICollection<RegionConnection> Connections`
- `RegionType` enum (in `Region.cs`): `Forest`, `Highland`, `Coastal`, `Volcanic`, `Countryside`
- `RegionConnection.cs`: composite PK (`FromRegionId` + `ToRegionId`), navigate `FromRegion`, `ToRegion`; `Restrict` delete-behavior on both FK sides
- **`ZoneConnection` and `Zone.Exits` do not exist.** Travel is `RegionConnection` (region ↔ region), the **region** map graph, and the **zone** map graph — each of the 19 zone `Map`s carries an `EntryPoint` node and one `Location` node per location (`RefId` = slug). Only **10 of the 19** also carry a `RegionExit` node; the other **nine** (aldenmere, ashfields, pale-moor, saltcliff-heights, skarhold, smoldering-reach, soddenfen, tidewrack-flats, tolvaren) have no exit node at all and are left through `LeaveZone` → `LeaveCurrentZoneAsync` (`GameHub.cs:915`, `:3178`), which ends the session's hold on the zone without moving the character to a node. Four Varenmark maps (crestfall, ashlen-wood, grevenmire, the-droveway) carry **9 `MapNodeType.ZoneExit` nodes** that jump straight to another zone's map — the only zone→zone adjacency authored at zone level. `Walk`/`ExitToRegion`/`ExitToZone` edges are executed by `TravelToNodeHubCommand` (any node with a `TargetMapId` is a transition, `:280-288`); `EnterZoneLocationHubCommand` resolves a slug to its `Location` node and delegates to it. A `ZoneLocationConnection` table exists but is seeded for Crestfall only (11 rows, four slugs) and read by no handler or repository — do not cite it as the in-zone mover.

**Zone entity** (`Zone.cs`): `Id` (slug), `Name`, `Description`, `ZoneType Type`, `MinLevel`, `MaxPlayers`, `IsStarter`, `HasInn`, `HasMerchant`, `IsPvpEnabled`, `IsDiscoverable`, `IsDevOnly`, `RescueFundTotal`, `RegionId` FK + `Region` nav, `ICollection<PlayerSession> Sessions`

**Seed data (Veldrath world)**, re-derived from `ApplicationDataSeeder.cs` (text lives in `WorldCanonText.cs`, which the seeder and `WorldCanonSweep` both read):
- 1 World: `veldrath` / "Veldrath" / era "The Ashen Age (the Age of Reclaiming)"
- 4 Regions: `varenmark` (Countryside, 0–6, starter), `greymoor` (Highland, 5–14), `saltcliff` (Coastal, 10–20), `cinderplain` (Volcanic, 18–30)
- 6 RegionConnections: varenmark↔greymoor, greymoor↔saltcliff, greymoor↔cinderplain (bidirectional)
- 19 Zones, of which `playground` is `IsDevOnly = true` and never returned by the public APIs (`ZoneRepository` filters on it)
- 5 `ZoneType.Dungeon` zones: `the-halrow`, `drowning-pits`, `barrow-deeps`, `sunken-name`, `kaldrek-maw` (display name "The Maw" — slugs are opaque keys and do not carry canon)
- **Starter zone**: `crestfall` (Crestfall, Varenmark)
- **Canon sweep**: on the Postgres startup path `WorldCanonSweep` rewrites a seeded value only when it still carries a retired token or *exactly* equals an entry in `WorldCanonText.KnownStaleValues` (that set holds the pre-rewrite `World.Description`, which names nothing retired). A future reword with no retired token must have its old string recorded there; until then the sweep reports the field in `DriftedFields` and leaves it alone. Drift is reported for `Worlds`/`Regions`/`Zones` only — `Maps`/`MapNodes`/`ZoneLocations` are also written by the Foundry editor, so divergence there is authored content.

**Character default zone**: `Character.StartingZoneId = "crestfall"` (`Character.cs:55`); first location is `crestfall-square` (`CharacterCreationSessionEndpoints.cs:37`)

**Repositories**: `IRegionRepository` + `RegionRepository`, `IWorldRepository` + `WorldRepository` added; `IZoneRepository.GetByRegionIdAsync` added  
**DI**: `IRegionRepository` + `IWorldRepository` registered in `Program.cs`  
**Contracts**: `RegionDto`, `WorldDto` added to `ZoneContracts.cs`; `ZoneDto` extended with optional `RegionId?`, `HasInn`, `HasMerchant`  
**Endpoints**: `GET /api/regions`, `GET /api/regions/{id}`, `GET /api/regions/{id}/connections`, `GET /api/worlds`, `GET /api/worlds/{id}`, `GET /api/zones/by-region/{regionId}`  
**Migration**: `AddWorldRegionZoneConnections` in `Migrations/Application/`

**SQLite FK tests**: FK constraints ARE enforced in `TestDbContextFactory`. Tests that add custom zones without a RegionId will fail. Use seeded zone IDs (`"crestfall"`, `"aldenmere"`, etc.) in ZoneSession tests instead of `MakeZone`.
