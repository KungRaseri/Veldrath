# Client UI architecture — decisions

> Durable record of the two UI decisions that shape the SvelteKit game client. The original design
> documents were deleted after their still-useful content was folded into this file; the Avalonia
> desktop client that once shared these decisions was retired (Veldrath is web-first).

---

## 1. Panel-driven UI instead of 2D map rendering

**Decision:** the game is presented through **panels** — structured text, tables, and clickable
lists — not a rendered tilemap or sprite scene.

- Movement and interaction are point-and-click: click an exit to travel, click an entity to
  interact, click a node on the map graph to travel to it.
- The tile/map **data model stays**: zone tile data, region maps, collision masks, fog masks, exit
  tiles, and entity positions are still delivered by the server and still drive the UI. They are
  projected into lists and cards rather than drawn as a grid.
- All renderer abstractions (sprite/ASCII renderers, tile texture caches, tile registries,
  `RendererMode`) were removed with the desktop client. Do not reintroduce them.

**Consequences**

- One UI concept per panel: zone/location detail, region overview, character, chat, combat,
  inventory, shop, journal, attribute allocation.
- [`GameSession`](../web/client/src/lib/game/session.svelte.ts) is the single orchestrator: it
  applies hub payloads to Svelte runes and exposes commands; components only bind and forward input.

## 2. One DB-backed map graph for world navigation

**Decision:** world navigation is a single graph stored in Postgres (`Map`, `MapNode`, `MapEdge`),
read by the game client and authored in RealmForge.

- **Nodes** carry zone/location identity, position hints, and discovery state; **edges** carry the
  connection type. Server endpoints and hub events deliver the graph as `MapGraphReceived` /
  `NodeTraveled` payloads.
- **Layout is cardinal** (orthogonal connectors, layered columns) rather than free-form, implemented
  in [`web/client/src/lib/game/cardinal-layout.ts`](../web/client/src/lib/game/cardinal-layout.ts).
- **Validation** lives with the data: `RealmEngine.Data/Validation/MapGraphValidator` rejects
  graphs with dangling edges, duplicate node ids, or unreachable nodes.
- **Authoring** happens in RealmForge (`MapGraphEditorService`), not by hand-editing JSON.
- Tilemap/TMX authoring was removed; do not add tile coordinates back to the map model.

**Consequences**

- Adding a location means adding a node plus edges in RealmForge — never a client code change.
- Any layout change must keep the `cardinal-layout` unit tests green
  ([`cardinal-layout.test.ts`](../web/client/src/lib/game/cardinal-layout.test.ts)).

---

## Related

- [`docs/design-system.md`](design-system.md) — tokens, typography, and the retired Blazor appendix.
- [`plans/sveltekit-migration.md`](../plans/sveltekit-migration.md) — how the web app reached its current shape.
- [`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md) — session-by-session notes on both pivots.
