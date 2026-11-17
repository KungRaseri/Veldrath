# Client UI architecture — decisions

> Durable record of the two UI decisions that shape the SvelteKit game client. The original design
> documents were deleted after their still-useful content was folded into this file. Every non-web
> surface has been retired — the Avalonia desktop client and the `RealmForge` editor included
> (Veldrath is web-first, and Foundry in the SvelteKit app is the content editing surface).

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
- [`GameSession`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/session.svelte.ts) is the single orchestrator: it
  applies hub payloads to Svelte runes and exposes commands; components only bind and forward input.

## 2. One DB-backed map graph for world navigation

**Decision:** world navigation is a single graph stored in Postgres (`Map`, `MapNode`, `MapEdge`),
read by the game client and authored through the server's content endpoints.

- **Nodes** carry zone/location identity, position hints, and discovery state; **edges** carry the
  connection type. Server endpoints and hub events deliver the graph as `MapGraphReceived` /
  `NodeTraveled` payloads.
- **Layout is cardinal** (orthogonal connectors, layered columns) rather than free-form, implemented
  in [`web/client/src/lib/game/cardinal-layout.ts`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/cardinal-layout.ts).
- **Validation** lives with the data: `RealmEngine.Data/Validation/MapGraphValidator` rejects
  graphs with dangling edges, duplicate node ids, or unreachable nodes.
- **Authoring** happens through the server write path (validated before it is stored), not by
  hand-editing JSON.
- Tilemap/TMX authoring was removed; do not add tile coordinates back to the map model.

**Consequences**

- Adding a location means adding a node plus edges as data — never a client code change.
- Any layout change must keep the `cardinal-layout` unit tests green
  ([`cardinal-layout.test.ts`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/cardinal-layout.test.ts)).

## 3. HUD layout — sheet, world, social

**Decision:** the game screen is a fixed three-column shell with no tabbed center area and no
full-height drawers. Each column owns one job, and everything that used to be duplicated between the
header, footer and side panels lives in exactly one place.

| Column | Owns | Component |
|---|---|---|
| Top bar | Character identity, zone/location breadcrumb, connection state, "Characters" exit | [`GameHeader.svelte`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/GameHeader.svelte) |
| Left | The character sheet: HP/MP bars, gold/XP, the 8-slot equipment rack, the bag list with per-item actions, and the character menu (Quest log, Merchant, Settings) | [`LeftSidebar.svelte`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/LeftSidebar.svelte) |
| Center | The world only: map graph, selected/current node detail with travel, and the "Local" block (enemies present + Search/Rest/Merchant/Leave zone). Combat replaces this block while a fight is active | [`GameScreen.svelte`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/GameScreen.svelte) |
| Right | Social only: chat channels/log/composer and the "Also here" occupant list | [`RightSidebar.svelte`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/RightSidebar.svelte) |

- **Secondary surfaces are centered dialogs**, not slide-over drawers: quest log, shop and settings
  render through Bits UI `Dialog` with the `.game-dialog` / `.game-dialog-backdrop` primitives in
  [`brand.css`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/routes/brand.css). [`GameOverlays.svelte`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/GameOverlays.svelte)
  owns all three.
- **The bag is the inventory of record.** Equipped items stay in the list and carry an `Equipped`
  badge plus a slot shown in the rack; there is no separate inventory panel to open. The old
  `GameFooter` was deleted — the connection dot and map/occupant counts were duplicates.
- **Item actions are per row**: Equip (only when the server reports a `slot`), Unequip, Sell (only at
  a merchant) and Discard (two-step inline confirm). Discard maps to the server's `DropItem`, which
  removes one unit permanently — it does not place an item on the ground yet.

**Inventory wire contract**

[`GetInventoryHubCommandHandler`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/Veldrath.Server/Features/Characters/GetInventoryHubCommand.cs)
projects items into flat
[`InventoryItemView`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/Veldrath.Server/Features/Characters/InventoryItemView.cs) records and sends
`InventoryLoaded { CharacterId, Items, Equipped, OpenOverlay }`:

- `Items` — every owned item, equipped ones included and flagged with `IsEquipped`.
- `Equipped` — canonical slot → view, for the rack.
- `Rarity` and `Type` are **strings** (`"Rare"`, `"Weapon"`). The raw `Item` graph would serialise
  those enums as numbers over SignalR, which is why the old UI rendered `rarity: 2` and lost every
  `rarity-*` colour.
- `Slot` is resolved by
  [`EquipmentSlots`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/Veldrath.Server/Features/Characters/InventoryItemView.cs) from the item type;
  the client never guesses a slot, and `EquipItem` accepts a request **without** `Slot` so the bag can
  equip in one click.

## 4. Gameplay surfaces the HUD exposes

Everything below rides on the same `GameSession` runes store; no panel keeps its own copy of state.

| Surface | Where | Wire |
|---|---|---|
| Bag actions | Character sheet rows — name line, status line, icon-only action line | `EquipItem` (slot derived server-side), `UseItem`, `SellItem`, `DropItem` (destroy), `DropItemToGround` |
| Item details | Hover/focus card beside the row ([`ItemTooltip.svelte`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/web/client/src/lib/game/ItemTooltip.svelte)), plus a centered dialog on click | `InventoryLoaded` → `InventoryItemView` (damage, armour, durability, effect, requirements, bonuses) |
| Attributes | Dialog behind the attribute strip in the sheet | `AllocateAttributePoints` → `AttributePointsAllocated` |
| Abilities | Sheet menu → Abilities dialog | `UseAbility` (out of combat) |
| Ground items | Center column, "On the ground" block | `GetGroundItems`, `DropItemToGround`, `PickUpGroundItem` → `GroundItemsChanged` |
| Quest log | Sheet menu → Quest log dialog | `GetQuestLog`, `AcceptQuest`, `AbandonQuest`, `TurnInQuest` → `QuestLogReceived`, `QuestTurnedIn` |
| Area search | Center column, "Discovered" block | `SearchArea` → `AreaSearched` |

**Bag rows are three lines**: the name leads (rarity colour, the icon is a small muted glyph beside it),
the status line carries the equipped badge and rarity/type/slot/durability, and the actions are icon-only
on their own line. Hovering or focusing a row (bag or equipment slot) opens the tooltip card, which flips
sides and clamps to the viewport so it stays readable from the narrow left column.

**Quests** live in `Character.QuestBlob` (JSON journal of `CharacterQuestEntry`). Objective keys use a
`kind:key` convention — `kill:<enemy name>`, `collect:<item ref>`, `visit:<location slug>` — and the
hub advances them from the events it already handles (enemy defeats, buys/pickups, navigation). Turn-in
requires every objective and grants gold/experience through the existing `AddGold` / `GainExperience`
commands, so levelling logic is not duplicated.

**Ground items** live in the `GroundItems` table keyed by `(ZoneId, LocationSlug)`. `DropItem` remains a
permanent destroy; `DropItemToGround` is the "leave it on the floor" path, and pickups are validated
against the character's current location.

---

## 5. The Foundry map editor and the review gate

The map graph is authored in Foundry, not in a desktop tool and not in the seeder. Since milestone G the
editor is a canvas plus an inspector, and — the part that matters more — nothing an author does can put a
map in front of a player.

**One draft, one writer.** `/maps/[mapId]` holds a single `MapGraphDto` in component state. The canvas,
the metadata form and the inspector all mutate that object, and the save and validate actions post it
whole in a hidden field. `parseDraftGraph` refuses anything that is not this map's graph and falls back to
the stored copy, so a mangled payload is a no-op rather than a partial write. The write-through form
action that used to reconstruct a graph from individual inputs is gone; keeping both would have discarded
every canvas gesture without an error.

**The rules live in TypeScript modules, not in markup.** `map-canvas.ts` (positions, the unplaced lane,
collision, tidy layout), `map-edges.ts` (direction inference, self-reference and duplicate-direction
refusals, reciprocal edges) and `map-nodes.ts` (suggested ids, node defaults, delete refusals) are pure and
unit tested. The components render and dispatch. Two rules are worth knowing:

- A node with no authored `layoutX`/`layoutY` is **never** given one silently. It sits in an "unplaced"
  lane, because the difference between "the author put it there" and "the layout did" is the difference
  between data and a guess.
- Blocks are *prevented* in the gesture that would create them — a diagonal pair has no inferable
  direction, a second edge in the same direction is refused while drawing, deleting a two-way connection
  removes both directions — because each of those is a validator error that is far cheaper to prevent than
  to read after a save.

**References are pickers.** `GET /api/content/maps/targets` serves every map, the zone and interior owners,
every zone location, the region owners, and every node of every map. A picker cannot produce the typo the
world validator exists to catch, so `target_node_unknown` is a backstop rather than a workflow step.

**The lifecycle is the gate.** A map is `Draft` → `In review` → `Live`, with `Changed since approval`
derived from `ApprovedVersion < Version`:

| State | `IsActive` | Set by | Visible to players |
|---|---|---|---|
| Draft | `false` | `POST /api/content/maps` | no |
| In review | `false` | `POST /{id}/submit` | no |
| Live | `true` | `POST /{id}/review` (`Curator`) | yes |
| Changed since approval | `true` | an edit after approval | yes, while an edit awaits re-review |

`Apply` deliberately does not write `IsActive`: only review and deactivate change it, so a crafted save
payload cannot activate a draft. Approval requires a submission and a clean world validate; rejection
clears the submission and leaves the draft editable, keeping the notes on the map (`LastReviewNotes`) so
the reason is readable where the work is. Live maps are retired by deactivating — refused while another
map's node still exits there — and only drafts can be deleted.

**Access follows the author/reviewer split.** Reads need a session and nothing more (the graph carries hidden
nodes and unlock keys, so it is never anonymous); authoring a map is the author's own work, with
`content.manage` able to write any; review and deactivation need the `content.review` **permission**, never a
role. Drafts are scoped per caller, and `canEdit` on the DTOs is what the editor disables its controls with.

A reviewer deciding on a map is deciding to publish it, so the review queue runs the world validator over each
submitted graph first and says whether approval would accept or refuse it — including the one warning approval
refuses, `map_without_exit`. Decisions are audited, and the author is told in the inbox they already use for
submission decisions: rejections carry the reviewer's notes, and a retirement says the map left the game.

---

## Related

- [`docs/design-system.md`](design-system.md) — tokens, typography, and the retired Blazor appendix.
- [`plans/sveltekit-migration.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/plans/sveltekit-migration.md) — how the web app reached its current shape.
- [`.github/agent-memory/veldrath-memory.md`](https://git.kungraseri.cloud/Veldrath/Veldrath/src/branch/main/.github/agent-memory/veldrath-memory.md) — session-by-session notes on both pivots.
