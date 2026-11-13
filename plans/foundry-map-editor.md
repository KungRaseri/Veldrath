# Foundry map editor (Milestone G)

> **Status: G0 shipped, G1 in progress** on `feat/milestone-g-foundry-map-editor`
> ([PR #71](https://git.kungraseri.cloud/Veldrath/Veldrath/pulls/71)). The read, inspect and save path
> shipped earlier in F5 (PR #70, merged); this plan is the remaining authoring surface: the canvas,
> node and edge editing, the review gate that lets a map into the game, and the validation loop that
> makes a graph safe to change.
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
| Review pipeline | [`FoundryEndpoints.cs`](../Veldrath.Server/Features/Foundry/FoundryEndpoints.cs), [`FoundryService.cs`](../Veldrath.Server/Features/Foundry/FoundryService.cs) | Submissions are created by any authenticated account, reviewed under the `Curator` policy, and notify the submitter through `FoundryNotifications` — the vocabulary this plan reuses for map review |
| Seed lifecycle | [`MapGraphSeeder.cs`](../RealmEngine.Data/Seeders/MapGraphSeeder.cs:21) | `if (await db.Maps.AnyAsync()) return;` — a one-shot bootstrap, so a map added after the first boot can only come from Foundry or a migration. Create is therefore not redundant |
| Game read gate | [`EfCoreMapGraphRepository.cs`](../RealmEngine.Data/Repositories/EfCoreMapGraphRepository.cs:23) | `GetMapAsync`, `GetMapByOwnerAsync` and `GetMapsAsync` all filter `IsActive`; `GetNodeAsync` does not |

The save path already reconciles nodes and edges the client adds or removes. What is missing is a
surface that produces those changes, a way to see a problem before the save is refused, and a gate
that decides when a map enters the game.

Three premises were checked against the code and corrected before execution:

- **`IsActive` is honoured but unreachable.** The read path filters it in three places, yet it is on
  neither [`MapGraphDto`](../Veldrath.Contracts/Map/MapContracts.cs:32) nor `MapSummaryDto`, and
  [`Apply`](../Veldrath.Server/Features/Content/MapEditorService.cs:205) never writes it. No editor
  can see it, list it, or change it today.
- **The validator sees only one of the two mistakes the editor exists to make.** The entry-node
  counterparts already exist — `entry_missing`, `entry_not_found` and `entry_flag_mismatch` in
  [`ValidateEntryNode`](../RealmEngine.Data/Validation/MapGraphValidator.cs:241) — but
  `exit_target_missing` checks only that `TargetNodeId` is **non-empty**, never that the node exists,
  so an exit pointing at a plausible name that resolves to nothing passes.
- **Nothing HTTP-level covers the API.** Grepping `Veldrath.Server.Tests` for `api/content/maps`
  returns no results; `MapEditorServiceTests` is service-level only.

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
| **M10** | Completeness first | The validator gains `target_node_unknown` in G1 — the one destination rule it is missing — **before** the canvas relies on the dry run. The editor's refusals are only honest if the server agrees with them. |
| **M11** | Review is the gate into the game | A map enters the game only when a reviewer approves it. `POST` creates a **draft** (inactive, entry node only); `submit` queues it; `review` approves (activates) or rejects; `deactivate` retires a live map. `Map.ApprovedVersion` records which version was signed off, so "changed since approval" is derived rather than stored. |
| **M12** | One writer per save | The write-through `save` action and `applyNodeEdits` are **deleted** in G2, not adapted. The canvas keeps a draft locally and one action persists the serialised draft with the draft's base version. Two writers would discard every canvas gesture without an error. |

### 2.1 Who may do what, and when a map is in the game

The review gate reuses the vocabulary the submission queue already uses: `content.manage` to author,
the `Curator` policy to review, `FoundryNotifications` to tell the author what happened. Maps are
**not** stored as submission payloads — a map is edited in place under optimistic versions, which is
not the shape of an item or quest submission, and promotion of submissions into the catalog is a
separate plan (§5.6).

| Action | Required | Notes |
|---|---|---|
| Read a graph (`GET`) | `content.view` | Never anonymous and never a plain player account: the graph carries `IsHidden` nodes and `UnlockKey`s, which are the dungeon's secrets |
| List graphs | `content.view` | A draft appears only to its author and to reviewers |
| Create a draft, edit a draft, submit it | `content.manage` | ContentEditor and Admin today; a wider `content.submit` grant can be added later without touching the editor |
| Edit a **live** map | `content.manage` **and** the reviewer policy | Only a reviewer may change a map players are standing on; the change is audited and sets `ApprovedVersion < Version` |
| Approve or reject | `Curator` | Same policy as `/api/foundry/submissions/{id}/review` |
| Deactivate a live map | `Curator` | Refused while any node targets it or a character occupies it |
| Delete | Author or `Curator` | **Drafts only**, and refused when referenced. A live map is retired by deactivating, never deleted |

| State | `IsActive` | `ApprovedVersion` | Visible in game |
|---|---|---|---|
| Draft | `false` | `0` | no |
| In review | `false` | `0`, `SubmittedAt` set | no |
| Live | `true` | `== Version` | yes |
| Changed since approval | `true` | `< Version` | yes — an approved map stays live while an edit awaits review |

## 3. Phases

Each phase is independently shippable. A phase is not done by satisfying its section alone; it
also satisfies the seven milestone rules above.

### G0 — Navigation, access and the test floor

The editor is reachable only by typing a URL, and the links back out are wrong. Three point at routes
that do not exist because the `(foundry)` group strips from the URL: `/foundry/content` in
[`maps/+page.svelte:23`](../web/client/src/routes/%28foundry%29/maps/+page.svelte:23), `/foundry/maps`
in [`maps/[mapId]/+page.svelte:41`](../web/client/src/routes/%28foundry%29/maps/%5BmapId%5D/+page.svelte:41),
and `/foundry/maps/{id}` built in
[`maps/+page.svelte:48`](../web/client/src/routes/%28foundry%29/maps/+page.svelte:48).

- Fix all three links and add a shared Foundry sub-navigation (Maps, Content, Submissions,
  Notifications, Languages) in a **new** `(foundry)/+layout.svelte`; only `+layout.server.ts` exists.
- **Split the endpoint group's authorisation** (§2.1): reads require `content.view`, writes require
  `content.manage`. Today
  [`MapEditorEndpoints.cs:20`](../Veldrath.Server/Features/Content/MapEditorEndpoints.cs:20) puts
  `ManageContent` on the whole group, so a Reviewer gets a 403 page and G2's read-only mode is
  unreachable. Guard the two loaders with `content.view` and the actions with `content.manage`.
- Add the map loaders to `foundry-loaders.test.ts` (list, detail, 404, validate and save actions), and
  the sparse variants to `pages-foundry-branches.test.ts`.
- Add the list and editor pages to `pages-foundry.test.ts`, including the read-only state and the
  state badge.
- Reference `/maps` in a Playwright spec so the route exemption in `routes.test.ts` can shrink.

**Done when:** the map editor is reachable from the Foundry header, no Foundry link 404s, a
`content.view` account sees graphs read-only, and the pages have loader and component tests.

### G1 — Validate without saving, and make the validator complete

- **Validator rule (M10), before anything relies on the dry run:** `target_node_unknown` — an exit's
  `TargetNodeId` must exist on the target map. `exit_target_missing` only checks that it is non-empty,
  so a typo passes today. The entry-node rules (`entry_missing`, `entry_not_found`,
  `entry_flag_mismatch`) already exist and need no work. Cases in
  [`MapGraphValidatorIssueCodeTests`](../RealmEngine.Data.Tests/Validation/MapGraphValidatorIssueCodeTests.cs),
  plus the seeded-world run.
- Contract: `MapGraphValidationRequest(MapGraphDto Map)` and
  `MapGraphValidationResponse(bool IsValid, IReadOnlyList<MapGraphIssueDto> Issues)`.
- Server: `POST /api/content/maps/{mapId}/validate`. Refactor the existing internal validation so the
  save, the dry run and later the approval all use one implementation, and keep the "issues for this
  map plus `target_map_unknown`" filter. **Write the filter's intent down**: a pre-existing defect in
  a sibling map never blocks an edit here.
- **HTTP tests for the group — none exist today.** 403 for a non-manager, 404 for an unknown map, 409
  for a stale `expectedVersion`, 400 carrying issues for a refused graph.
- Web: a **Validate** action next to Save. Issues are grouped by node, shown as badges on the rows,
  and summarised above the form. Warnings never block; errors do.
- Pure rule: `issuesByNode` in `map-editor.ts`, unit tested.

**Done when:** an author can iterate without writing, a refused graph names the node that is wrong
before the save, and a missing or mistyped exit target is refused rather than ignored.

### G2 — The canvas, moving nodes, and one writer

- Pure module `map-canvas.ts`: placements from authored coordinates, an unplaced lane, bounds,
  collision detection, `moveNode`, `tidyLayout`, and issue indexing. Unit tested without a browser —
  `tidyLayout` and the collision check are named in the done-when, because they are exactly what
  lands untested under a coverage floor.
- Component `MapCanvas.svelte`: scrollable absolute grid, nodes as cards, SVG connectors with
  direction labels, selection, issue badges, legend, and a read-only mode driven by `canEdit`.
- Interaction: drag to move with integer snapping, arrow-key nudge, numeric X/Y fields in the
  inspector. The keyboard path is the testable one; the pointer path is the convenience.
- Undo/redo over graph snapshots and a dirty-state guard when navigation would discard a draft.
- **One writer (M12).** Delete the write-through `save` action in
  [`maps/[mapId]/+page.server.ts:34`](../web/client/src/routes/%28foundry%29/maps/%5BmapId%5D/+page.server.ts:34)
  and delete `applyNodeEdits` from `map-editor.ts`. The replacement persists the serialised draft with
  the draft's base version; keeping both would discard every canvas gesture without an error.
- The page shows the map's state (§2.1): Save and Submit appear only when the account may act on that
  state, and a live map offers neither to an author who is not a reviewer.

**Done when:** a room can be repositioned, saved, reloaded and appears in the game exactly where the
author placed it — and exactly one code path writes a graph.

### G3 — The node inspector and node CRUD

- Inspector beside the canvas, driven by node type. Identity, description, icon, coordinates, sort
  order, hidden flag, unlock type and key, discover threshold, `RefId`, and the interior link.
- Pickers: `RefId` resolves against zone locations, zones or regions according to node type.
- Create node: choose type, suggested id, defaults (`sortOrder` = max + 10, a free cell near the
  selection), then type-specific required fields.
- Delete node: removes its edges, shows inbound references first, and refuses when the node is the
  entry node (`entry_node_unknown` would catch it, but the refusal is the better experience), the
  target of another map's exit, or the map is live and the account is not a reviewer.

**Done when:** a room can be added, wired and removed without touching the seeder, and neither the
entry node nor a cross-map target can be deleted by accident.

### G4 — Edge editing

- Create an edge by selecting origin then target, or by dragging between nodes; infer the direction
  from relative position and let the author correct it.
- Block self-references and a second outgoing edge in the same direction — both are validator errors
  (`edge_self_reference`, `edge_direction_duplicate`) — and check them **while drawing**, not at save.
- Reciprocal edges: offer to create the return edge with the opposite direction in one action.
  `Portal` and `Door` default it off and the direction to `In`/`Out`, because a one-way portal is
  legitimate. Deleting one half warns and offers to delete both.
- A new edge mints its `Guid` once and undo/redo preserves it; re-minting would make reconcile delete
  and re-add the row on every save.
- Edge inspector: direction, type, label, traversable, hidden; delete edge.

**Done when:** the Halrow-style stranded rooms can be fixed entirely in the editor, without a
seeder change, and a one-way connection is impossible to create by accident.

### G5 — Exits and map metadata

- Header form: name, description, entry node, owner (locked after creation; moving a map to a
  different owner is a separate decision). **State is not a form field** — it is changed by the review
  verbs in G6 and shown read-only here.
- Target map picker; once chosen, fetch that graph's nodes and pick `TargetNodeId`, defaulting to the
  target entry node. This closes the half-finished exit editor: `TargetMapId` is editable today but
  `TargetNodeId` is not, and the validator requires both — and with `target_node_unknown` from G1 the
  choice is checked rather than trusted.

**Done when:** a complete cross-map exit can be authored in one place, and the target node is never
a guess.

### G6 — Lifecycle and the review gate

This is the gate the milestone exists for: an author creates a map, a reviewer decides whether it
enters the game. It reuses the submission queue's vocabulary — the `Curator` policy to review,
`FoundryNotifications` to tell the author — without storing maps as submission payloads (§5.6).

- **Entity and contract.** `Map` gains `CreatedByAccountId`, `SubmittedAt`, `SubmittedByAccountId`,
  `ApprovedAt`, `ApprovedByAccountId` and `ApprovedVersion`, plus a migration; `MapSummaryDto` and
  `MapGraphDto` gain `IsActive` and the review fields the UI needs; `Apply` learns to write
  `IsActive`. **`IsActive` is never a form field.**
- `POST /api/content/maps` — a **draft**: inactive, one entry node, id derived from type and owner
  (`region:<owner>`, `zone:<owner>`, `interior:<locationSlug>`), refused on a duplicate id or a
  second map for the same owner. **No exit is fabricated**: a dungeon draft is simply not approvable
  until the author adds one, which is where `map_without_exit` belongs.
- **Create interior map** from a location node: pre-fill `interior:<locationSlug>`, create the entry
  node, and set the location node's `TargetMapId` — as a draft, so the link is not live early.
- `POST /{id}/submit` — `content.manage`; refuses while validation reports any Error, and stamps
  `SubmittedAt`/`SubmittedByAccountId`.
- `POST /{id}/review` — `Curator`, body `MapReviewRequest(bool Approved, string? Notes)`, mirroring
  [`FoundryService.ReviewAsync`](../Veldrath.Server/Features/Foundry/FoundryService.cs:111). Approval
  requires `SubmittedAt` and a clean validate, then sets `IsActive = true`, `ApprovedVersion =
  Version`, reviewer and timestamp, and writes a `FoundryNotification` to the author. Rejection records
  the notes and leaves the draft editable. Both audit.
- `POST /{id}/deactivate` — `Curator`; refused while any node anywhere targets the map or a character
  occupies it; sets `IsActive = false` and keeps `ApprovedVersion`.
- `DELETE /{id}` — drafts only, refused when any node targets the map. A live map is retired by
  deactivating, never deleted.
- The list shows the four states from §2.1 — including **Changed since approval**, derived from
  `ApprovedVersion < Version` rather than stored — and the review queue is that list filtered to
  `SubmittedAt` set.
- Validation always knows every stored map, so a draft that is still targeted is a content defect the
  editor must refuse rather than hide.

**Done when:** a new zone's map, or a dungeon room's interior, can be created in Foundry, typed by an
author, approved by a reviewer, and entered by a player only after that — with no seeder change and
no rebuild.

### G7 — Gates and documentation

- Coverage for the new `#lib/foundry` modules above the existing per-glob floors.
- Playwright coverage of the drag, validate, submit and approve path; the `/maps` exemption in
  [`routes.test.ts`](../web/client/src/lib/parity/routes.test.ts) is deleted rather than widened, and
  the new endpoints need no `rest-routes` exemption because the pages consume them.
- Update [`docs/client-ui-architecture.md`](../docs/client-ui-architecture.md) with the editor model
  and the review gate; update [`dungeons.md`](dungeons.md) §5, §8 **and its status header** to point
  here; record the lifecycle in
  [`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md).

## 4. Deliberately not in this milestone

- **Location traits and actor pools.** They live on `ZoneLocation`, not on the graph. The generic
  content write path copies values with `CurrentValues.SetValues`, which does not persist the
  `ActorPool` collection, so this needs its own write path and its own plan.
- **Bulk import/export, content versioning and rollback.** Deferred from milestone B and still
  deferred. The lifecycle in G6 is a review gate over the live row, not a publish pipeline: an
  approved map stays live while an edit awaits re-review, because serving the last approved version
  would need history.
- **Maps as submission payloads.** Map review is map-native (§2.1); promoting submissions into the
  catalog is §5.6 and stays separate.
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
   write path that lands it in the catalog. This plan does not wait for it: map review is map-native
   (§2.1), so nothing here depends on that promotion path.

## 6. See also

- [`dungeons.md`](dungeons.md) — the dungeon model and the F5 ship record.
- [`ui-depth.md`](ui-depth.md) — the seven milestone rules this plan inherits.
- [`content-pipeline.md`](content-pipeline.md) — the Foundry write path and validators.
- [`web-feature-parity-and-coverage.md`](web-feature-parity-and-coverage.md) — the parity suites
  and coverage floors.
