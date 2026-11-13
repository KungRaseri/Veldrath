# Foundry map editor (Milestone G)

> **Status: G0–G1 in progress** on `g/foundry-map-editor`. The read, inspect and save path shipped
> in F5 (PR #70). This plan is the remaining authoring surface: the canvas, node and edge editing,
> map lifecycle, and the validation loop that makes a graph safe to change.
>
> Supersedes the outline in [`dungeons.md`](dungeons.md) §8, which remains the record of the dungeon
> model. Location-side authoring (traits, actor pools, interiors) is deliberately a separate plan;
> see §5.

---

## 1. What already exists

| Piece | Where | Fact |
|---|---|---|
| World-validated save | [`MapEditorService.cs`](../Veldrath.Server/Features/Content/MapEditorService.cs) | Replaces the graph, runs `MapGraphValidator.ValidateWorld`, refuses on errors, optimistic `expectedVersion` |
| REST surface | [`MapEditorEndpoints.cs`](../Veldrath.Server/Features/Content/MapEditorEndpoints.cs) | `GET /api/content/maps`, `GET /{id}`, `PUT /{id}` behind `content.manage` |
| Contracts | [`MapContracts.cs`](../Veldrath.Contracts/Map/MapContracts.cs) | Full graph DTO with a version; the write request carries the expected version |
| Validator | [`MapGraphValidator.cs`](../RealmEngine.Data/Validation/MapGraphValidator.cs) | Reachability, exits, unlock keys, target map existence, interior owner |
| Foundry pages | [`/maps`](../web/client/src/routes/%28foundry%29/maps/+page.svelte), [`/maps/[mapId]`](../web/client/src/routes/%28foundry%29/maps/%5BmapId%5D/+page.svelte) | List and table inspector; edges read-only |
| Pure rules | [`map-editor.ts`](../web/client/src/lib/foundry/map-editor.ts) | `nodeRows`, `readNodeEdit`, `applyNodeEdits`, `issueLabel` — unit tested |
| Game layout | [`cardinal-layout.ts`](../web/client/src/lib/game/cardinal-layout.ts), [`CardinalMap.svelte`](../web/client/src/lib/game/CardinalMap.svelte) | Reads `layoutX`/`layoutY` and draws direction connectors |

The save path already reconciles nodes and edges the client adds or removes. What is missing is a
surface that produces those changes, and a way to see a problem before the save is refused.

## 2. Decisions

| # | Decision | Choice |
|---|---|---|
| **M1** | Unit of save | The **whole graph**. Canvas gestures mutate a local draft; one save sends the graph with the version it started from. No per-node endpoint that can leave a half-written graph. |
| **M2** | Where the rules live | Pure modules in `#lib/foundry` (`map-canvas.ts`, `map-editor.ts`). Components render and dispatch; they do not own graph rules. |
| **M3** | Positions | `LayoutX`/`LayoutY` are the authored source of truth and the editor shows them exactly. Unplaced nodes sit in an "unplaced" lane. **Tidy layout** derives coordinates from edge directions and writes authored values; it does not compact or renumber the grid. |
| **M4** | Local draft discipline | Undo/redo over graph snapshots, a dirty-state prompt, and a conflict state that keeps the draft and offers a reload. The server stays authoritative. |
| **M5** | Validation | Add a **validate-only** endpoint that runs the same world validator without persisting. Save still validates. The editor calls validate on demand and the author sees per-node issues while editing. |
| **M6** | References are pickers | Owner by map type, `RefId` by node type, target map, target node fetched from the target graph, unlock type from its enum, icon from a curated list. Free text remains only for labels, descriptions and keys that name content. |
| **M7** | Identifiers | Map ids stay `region:<owner>` and `zone:<owner>`; an interior map is `interior:<locationSlug>`. Node ids stay `<mapId>:<kind>:<slug>`, suggested by the client and editable. The server remains the duplicate check. |
| **M8** | Lifecycle | Create seeds a valid minimal graph (one entry node). Delete is refused when another map targets it or when its owner still needs it; deactivate is the fallback. `IsActive` is already honoured by the game read path, so silently deactivating a referenced map would break travel. |
| **M9** | Milestone rules | No identifier on screen (D1), cost and consequence before commit (D2), server-authoritative state (D3), delete a parity exemption rather than add one (D4), extract components instead of growing a page (D5), tests ship (D6), DTO documentation stays complete (D7). |

## 3. Phases

Each phase is independently shippable. A phase is not done by satisfying its section alone; it
also satisfies the seven milestone rules above.

### G0 — Navigation and the test floor

The editor is currently reachable only by typing a URL. Both Foundry map pages link to routes that
do not exist because the `(foundry)` group strips from the URL: `/foundry/maps` and
`/foundry/content` are 404s, while the pages live at `/maps` and `/content`.

- Fix both links and add a shared Foundry sub-navigation (Maps, Content, Submissions, Notifications,
  Languages) in a `(foundry)/+layout.svelte`.
- Add the map loaders to `foundry-loaders.test.ts` (list, detail, 404, validate and save actions).
- Add the list and editor pages to `pages-foundry.test.ts`, including the read-only state.
- Reference `/maps` in a Playwright spec so the route exemption in `routes.test.ts` can shrink.

**Done when:** the map editor is reachable from the Foundry header, the 404 links are gone, and the
pages have loader and component tests.

### G1 — Validate without saving

- Contract: `MapGraphValidationRequest(MapGraphDto Map)` and
  `MapGraphValidationResponse(bool IsValid, IReadOnlyList<MapGraphIssueDto> Issues)`.
- Server: `POST /api/content/maps/{mapId}/validate`. Refactor the existing internal validation so
  the save and the dry run use one implementation, and keep the "issues for this map plus
  `target_map_unknown`" filter.
- Web: a **Validate** action next to Save. Issues are grouped by node, shown as badges on the rows,
  and summarised above the form. Warnings never block; errors do.
- Pure rule: `issuesByNode` in `map-editor.ts`, unit tested.

**Done when:** an author can iterate without writing, and a refused graph names the node that is
wrong before the save.

### G2 — The canvas and moving nodes

- Pure module `map-canvas.ts`: placements from authored coordinates, an unplaced lane, bounds,
  collision detection, `moveNode`, `tidyLayout`, and issue indexing. Unit tested without a browser.
- Component `MapCanvas.svelte`: scrollable absolute grid, nodes as cards, SVG connectors with
  direction labels, selection, issue badges, legend, and a read-only mode for viewers without
  `content.manage`.
- Interaction: drag to move with integer snapping, arrow-key nudge, numeric X/Y fields in the
  inspector. The keyboard path is the testable one; the pointer path is the convenience.
- Undo/redo over graph snapshots and a dirty-state guard when navigation would discard a draft.

**Done when:** a room can be repositioned, saved, reloaded, and appears in the game exactly where
the author placed it.

### G3 — The node inspector and node CRUD

- Inspector beside the canvas, driven by node type. Identity, description, icon, coordinates, sort
  order, hidden flag, unlock type and key, discover threshold, `RefId`, and the interior link.
- Pickers: `RefId` resolves against zone locations, zones or regions according to node type.
- Create node: choose type, suggested id, defaults (`sortOrder` = max + 10, a free cell near the
  selection), then type-specific required fields.
- Delete node: removes its edges, shows inbound references first, and refuses when the node is the
  entry node or the target of another map's exit.

**Done when:** a room can be added, wired and removed without touching the seeder.

### G4 — Edge editing

- Create an edge by selecting origin then target, or by dragging between nodes; infer the direction
  from relative position and let the author correct it.
- Block self-references and a second outgoing edge in the same direction, which the validator
  already rejects.
- Edge inspector: direction, type, label, traversable, hidden; delete edge.

**Done when:** the Halrow-style stranded rooms can be fixed entirely in the editor, without a
seeder change.

### G5 — Exits and map metadata

- Header form: name, description, entry node, active flag, owner (locked after creation; moving a
  map to a different owner is a separate decision).
- Target map picker; once chosen, fetch that graph's nodes and pick `TargetNodeId`, defaulting to
  the target entry node. This closes the half-finished exit editor: `TargetMapId` is editable today
  but `TargetNodeId` is not, and the validator requires both.

**Done when:** a complete cross-map exit can be authored in one place, and the target node is never
a guess.

### G6 — Map lifecycle

- `POST /api/content/maps`: create a minimal valid graph from a type, owner and name, with id
  derived from the type and owner.
- **Create interior map** from a location node: pre-fill `interior:<locationSlug>`, create the
  entry node, and set the location node's `TargetMapId`.
- `DELETE /api/content/maps/{id}`: refuse when another map targets it or the owner still needs it,
  listing the reasons. Deactivate as the explicit fallback; the list shows active state.
- Validation always knows every stored map, so an inactive map that is still targeted is a content
  defect the editor must refuse rather than hide.

**Done when:** a new zone's map, or a dungeon room's interior, can be created in Foundry without a
seeder change, and a mistaken unreferenced map can be removed safely.

### G7 — Gates and documentation

- Coverage for the new `#lib/foundry` modules above the existing per-glob floors.
- Playwright coverage of the drag, validate and save path; the `/maps` exemption in
  [`routes.test.ts`](../web/client/src/lib/parity/routes.test.ts) is deleted rather than widened.
- Update [`docs/client-ui-architecture.md`](../docs/client-ui-architecture.md) with the editor
  model, update [`dungeons.md`](dungeons.md) §8 to point here, and update
  [`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md).

## 4. Deliberately not in this milestone

- **Location traits and actor pools.** They live on `ZoneLocation`, not on the graph. The generic
  content write path copies values with `CurrentValues.SetValues`, which does not persist the
  `ActorPool` collection, so this needs its own write path and its own plan.
- **Bulk import/export, content versioning and rollback, draft/publish.** Deferred from milestone B
  and still deferred.
- **Real-time collaboration.** Optimistic concurrency is the contract; two authors editing the same
  graph get a conflict, not a live cursor.
- **World map authoring.** `MapType.World` stays reserved.
- **Procedural generation and tilemaps.** The world is authored; the engine generator stays unused.

## 5. The next Foundry work

These are separate plans, recorded here only so the ordering is visible:

1. **Location editor** — traits (`IsSafeRespawn`, `IsInstanced`, `IsHidden`, unlock fields), the
   actor-pool table, and the interior link. This is the other half of dungeon authoring.
2. **Foundry home** — a landing page with pending submissions, recent content edits, and world
   validation health, instead of using Submissions as the front door.
3. **Content creation for every schema** — only languages have a create form today.
4. **Validation dashboard** — built on [`ContentAuditService.cs`](../Veldrath.Server/Features/Content/ContentAuditService.cs):
   unlock keys that name nothing, locations with empty pools, maps that reference missing content.
5. **Dialogue editor** — the content-schema blocker behind E4.
6. **Submission-to-content promotion** — the review queue approves a submission today but is not the
   write path that lands it in the catalog.

## 6. See also

- [`dungeons.md`](dungeons.md) — the dungeon model and the F5 ship record.
- [`ui-depth.md`](ui-depth.md) — the seven milestone rules this plan inherits.
- [`content-pipeline.md`](content-pipeline.md) — the Foundry write path and validators.
- [`web-feature-parity-and-coverage.md`](web-feature-parity-and-coverage.md) — the parity suites
  and coverage floors.
