# Client UI architecture — decisions

> Durable record of the two UI decisions that shape both the Avalonia desktop client and the
> SvelteKit web client. The original design documents were deleted after their still-useful
> content was folded into this file.

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
  `RendererMode`) were removed from `Veldrath.Client`. Do not reintroduce them.

**Consequences**

- One UI concept per panel: zone/location detail, region overview, character, chat, combat,
  inventory, shop, journal, attribute allocation.
- `GameViewModel` remains the single orchestrator: it applies hub payloads to observable state and
  exposes `ReactiveCommand`s; views only bind and forward input.
- The web client mirrors the same model with Svelte runes in
  [`web/client/src/lib/game/session.svelte.ts`](../web/client/src/lib/game/session.svelte.ts).

## 2. One DB-backed map graph for world navigation

**Decision:** world navigation is a single graph stored in Postgres (`Map`, `MapNode`, `MapEdge`),
shared by the web client, the desktop client, and RealmForge.

- **Nodes** carry zone/location identity, position hints, and discovery state; **edges** carry the
  connection type. Server endpoints and hub events deliver the graph as `MapGraphReceived` /
  `NodeTraveled` payloads.
- **Layout is cardinal** (orthogonal connectors, layered columns) rather than free-form. The same
  algorithm runs in both clients:
  - desktop: `Veldrath.Client/Services/GraphLayout` (port of the original layout);
  - web: [`web/client/src/lib/game/cardinal-layout.ts`](../web/client/src/lib/game/cardinal-layout.ts).
- **Validation** lives with the data: `RealmEngine.Data/Validation/MapGraphValidator` rejects
  graphs with dangling edges, duplicate node ids, or unreachable nodes.
- **Authoring** happens in RealmForge (`MapGraphEditorService`), not by hand-editing JSON.
- Tilemap/TMX authoring was removed; do not add tile coordinates back to the map model.

**Consequences**

- Adding a location means adding a node plus edges in RealmForge — never a client code change.
- Any layout change must be made in both implementations and covered by the client tests
  (`Veldrath.Client.Tests/Services/GraphLayoutTests`, `cardinal-layout` unit tests on the web side).

---

## Related

- [`docs/design-system.md`](design-system.md) — tokens, typography, and the retired Blazor appendix.
- [`plans/sveltekit-migration.md`](../plans/sveltekit-migration.md) — how the web app reached its current shape.
- [`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md) — session-by-session notes on both pivots.
