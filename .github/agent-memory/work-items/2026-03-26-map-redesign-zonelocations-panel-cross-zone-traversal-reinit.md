---
kind: work-item
date: 2026-03-26
area: content
title: "Map redesign + ZoneLocations panel + cross-zone traversal reinit"
---

### Session-24 (2026-03-26) — Map redesign + ZoneLocations panel + cross-zone traversal reinit

**Phase 1 — Map graph redesign (zones as primary nodes)**

- `MapNodeViewModel.cs`: added `RegionId`, `RegionLabel` (both `string? init`), `IsRegionHeader` (`bool`, derived from `NodeType == "region_header"`)
- `GraphLayout.ComputeGroupedZones()` added: groups `zone` nodes by `RegionId`, positions `region_header` label nodes above each 2-column zone cluster, deduplication-safe; falls back to `Compute` when no header nodes
- `MapViewModel.LoadFullGraphAsync()` rewritten:
  - Creates `region_header` nodes (non-interactive) instead of `region` nodes
  - Creates `zone` nodes with `RegionId` + `RegionLabel` populated from the enclosing region
  - Creates `zone_exit` edges via `GetZoneConnectionsAsync` per zone (deduplicated bidirectional pairs)
  - Calls `ComputeGroupedZones` instead of `ComputeHierarchical`
  - Removed dead `_characterId` private field (constructor param kept for caller compatibility)
- `MapView.axaml`:
  - Removed `level-tab` and `level-tab.active` style blocks (stale breadcrumb tab buttons)
  - Removed `<StackPanel Grid.Column="0">` breadcrumb block (bound to unimplemented `CanDrillOut`, `FocusedRegionName`, etc.)
  - Added `Border.map-node.region-header` style: `Background=Transparent`, `BorderThickness=0`, `IsHitTestVisible=False`, `Cursor=Arrow`
  - Added `Classes.region-header="{Binding IsRegionHeader}"` to DataTemplate Border
- `FakeZoneService` (tests): added `ZoneConnections : Dictionary<string, List<string>>` + updated `GetZoneConnectionsAsync` to serve from dict
- `MapViewModelTests`: updated 4 tests (`region` → `region_header` NodeType, zone_membership/region_exit tests replaced with `ZoneExitEdges_Connect_Adjacent_Zones` + `ZoneNodes_Carry_RegionId_And_RegionLabel`), fixed `BeEquivalentTo(["a","b"])` collection-expression syntax

**Phase 2 — In-game ZoneLocations panel**

- `ZoneLocationItemViewModel.cs` (new): display model for zone location list entries; `Slug`, `DisplayName`, `LocationType`, `MinLevel?`, reactive `IsCurrent` (also raises `CanNavigate`), `NavigateCommand? : ReactiveCommand<Unit, Unit>` (null when current)
- `GameViewModel`:
  - `ZoneLocations : ObservableCollection<ZoneLocationItemViewModel>` (public)
  - `CurrentZoneLocationDisplayName : string?` (derived, computed from `ZoneLocations`)
  - `CurrentZoneLocationSlug` setter: now also raises `CurrentZoneLocationDisplayName`
  - `LoadZoneCoreAsync(string zoneId)` extracted from `InitializeAsync`: sets `_currentZoneId`, resets `CurrentZoneLocationSlug = null`, fetches zone + calls `LoadWorldContextAsync` + `LoadZoneLocationsAsync`
  - `LoadZoneLocationsAsync(string zoneId)`: calls `GetZoneLocationsAsync(zoneId, _characterId)`, fills `ZoneLocations` with items that capture navigation callbacks
  - `InitializeAsync`: now calls `LoadZoneCoreAsync` + handles music + appends welcome log
  - `OnLocationEntered`: now also sets `IsCurrent` on all `ZoneLocations` entries
- `GameView.axaml`: after "Zones in this region" ItemsControl, added a separator + "Locations" caption + `ZoneLocationItemViewModel` ItemsControl (label, type, optional level, "Go →" button with `CanNavigate` + `NavigateCommand`)

**Phase 3 — Cross-zone traversal reinit**

- `GameViewModel.OnConnectionTraversed` changed to fire-and-forget `_ = HandleConnectionTraversedAsync(...)`
- `HandleConnectionTraversedAsync`: cross-zone (`isCrossZone + toZoneId`) → `AppendLog` + `await LoadZoneCoreAsync(toZoneId)`; same-zone → update slug + set IsCurrent flags + AppendLog

**Final count: 512 client + 468 server = 980 total passing**
