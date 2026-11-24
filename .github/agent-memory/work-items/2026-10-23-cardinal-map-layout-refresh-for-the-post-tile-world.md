---
kind: work-item
date: 2026-10-23
area: web
title: "Cardinal map layout refresh for the post-tile world"
---

### Session-55 (2026-10-23) — Cardinal map layout refresh for the post-tile world

**Layout**
- `CardinalMap` is now a compact text graph rather than a tile-sized canvas: node cards are
  152×60 with 8px horizontal / 10px vertical gutters (cells 160×70), an 8px canvas padding,
  and a centred canvas inside a bounded (`max-height: 52vh`) bordered viewport.
- Connectors now run between facing card edges instead of card centres, and each carries a
  cardinal arrow (`↑`/`↓`/`→`/`←`) plus a tooltip with the edge label. Blocked and hidden
  connectors get distinct styles.
- Authored `LayoutX/LayoutY` values are density-gated: dense layouts keep their relative grid
  (compacted to ordered indices), while sparse TMX-era layouts (e.g. 8 nodes spread across a
  64×40 tile rectangle) are discarded in favour of a compact edge-driven layout seeded at the
  entry node. This removes the large empty gaps between nodes.
- Node metadata uses compact inline badges (`Entry`/`Exit`) instead of MudChips; the current node
  uses the player-indicator gold token and a `● You are here` label.
- Added a map legend toolbar (Current / Selected / Exit) and `MapSubtitle` counts.
- `Game.razor` keeps the shell at exactly three columns (left sidebar / center / right chat);
  `.map-layout` stacks the map above the detail panel inside the center column.
- Keyboard arrow navigation now prefers a traversable graph edge in the requested direction before
  falling back to spatial nearest-neighbour selection. Enter no longer travels to the current node.
- Removed the unused `--vds-tile-*` design tokens from the RCL token file.

**Orientation follow-up (Session-56)**
- `CardinalGraphLayout` now reserves computed nodes in the edge's cardinal half-plane, so a North
  edge cannot place its target to the west when the straight cell is occupied; it uses a nearby
  perpendicular offset instead.
- Dense authored grids are only used when their positions agree with the edge directions (80%
  threshold); otherwise the compact edge-driven layout is used. This keeps arrows and placement
  consistent.
- L-shaped routes hug the destination card and suppress the direction arrow (no single direction).
- Added a compass hint to the map toolbar: `N ↑ · E → · S ↓ · W ←`.
- Tests cover conflicting north placement, authored-layout rejection on contradiction, and
  arrow-free L connectors.

**Tests**
- `CardinalGraphLayoutTests` updated for compacted authored coordinates.
- `CardinalMapComponentTests` adds direction-label, compact badge, edge-first keyboard navigation,
  and no-travel-to-current-node coverage. Components project: **102 passing**.
