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

## 3. HUD layout — sheet, world, social

**Decision:** the game screen is a fixed three-column shell with no tabbed center area and no
full-height drawers. Each column owns one job, and everything that used to be duplicated between the
header, footer and side panels lives in exactly one place.

| Column | Owns | Component |
|---|---|---|
| Top bar | Character identity, zone/location breadcrumb, connection state, "Characters" exit | [`GameHeader.svelte`](../web/client/src/lib/game/GameHeader.svelte) |
| Left | The character sheet: HP/MP bars, gold/XP, the 8-slot equipment rack, the bag list with per-item actions, and the character menu (Quest log, Merchant, Settings) | [`LeftSidebar.svelte`](../web/client/src/lib/game/LeftSidebar.svelte) |
| Center | The world only: map graph, selected/current node detail with travel, and the "Local" block (enemies present + Search/Rest/Merchant/Leave zone). Combat replaces this block while a fight is active | [`GameScreen.svelte`](../web/client/src/lib/game/GameScreen.svelte) |
| Right | Social only: chat channels/log/composer and the "Also here" occupant list | [`RightSidebar.svelte`](../web/client/src/lib/game/RightSidebar.svelte) |

- **Secondary surfaces are centered dialogs**, not slide-over drawers: quest log, shop and settings
  render through Bits UI `Dialog` with the `.game-dialog` / `.game-dialog-backdrop` primitives in
  [`brand.css`](../web/client/src/routes/brand.css). [`GameOverlays.svelte`](../web/client/src/lib/game/GameOverlays.svelte)
  owns all three.
- **The bag is the inventory of record.** Equipped items stay in the list and carry an `Equipped`
  badge plus a slot shown in the rack; there is no separate inventory panel to open. The old
  `GameFooter` was deleted — the connection dot and map/occupant counts were duplicates.
- **Item actions are per row**: Equip (only when the server reports a `slot`), Unequip, Sell (only at
  a merchant) and Discard (two-step inline confirm). Discard maps to the server's `DropItem`, which
  removes one unit permanently — it does not place an item on the ground yet.

**Inventory wire contract**

[`GetInventoryHubCommandHandler`](../Veldrath.Server/Features/Characters/GetInventoryHubCommand.cs)
projects items into flat
[`InventoryItemView`](../Veldrath.Server/Features/Characters/InventoryItemView.cs) records and sends
`InventoryLoaded { CharacterId, Items, Equipped, OpenOverlay }`:

- `Items` — every owned item, equipped ones included and flagged with `IsEquipped`.
- `Equipped` — canonical slot → view, for the rack.
- `Rarity` and `Type` are **strings** (`"Rare"`, `"Weapon"`). The raw `Item` graph would serialise
  those enums as numbers over SignalR, which is why the old UI rendered `rarity: 2` and lost every
  `rarity-*` colour.
- `Slot` is resolved by
  [`EquipmentSlots`](../Veldrath.Server/Features/Characters/InventoryItemView.cs) from the item type;
  the client never guesses a slot, and `EquipItem` accepts a request **without** `Slot` so the bag can
  equip in one click.

## 4. Gameplay surfaces the HUD exposes

Everything below rides on the same `GameSession` runes store; no panel keeps its own copy of state.

| Surface | Where | Wire |
|---|---|---|
| Bag actions | Character sheet rows | `EquipItem` (slot derived server-side), `UseItem`, `SellItem`, `DropItem` (destroy), `DropItemToGround` |
| Item details | Centered dialog, opened from a bag row or an equipment slot | `InventoryLoaded` → `InventoryItemView` (damage, armour, durability, effect, requirements, bonuses) |
| Attributes | Dialog behind the attribute strip in the sheet | `AllocateAttributePoints` → `AttributePointsAllocated` |
| Abilities | Sheet menu → Abilities dialog | `UseAbility` (out of combat) |
| Ground items | Center column, "On the ground" block | `GetGroundItems`, `DropItemToGround`, `PickUpGroundItem` → `GroundItemsChanged` |
| Quest log | Sheet menu → Quest log dialog | `GetQuestLog`, `AcceptQuest`, `AbandonQuest`, `TurnInQuest` → `QuestLogReceived`, `QuestTurnedIn` |
| Area search | Center column, "Discovered" block | `SearchArea` → `AreaSearched` |

**Quests** live in `Character.QuestBlob` (JSON journal of `CharacterQuestEntry`). Objective keys use a
`kind:key` convention — `kill:<enemy name>`, `collect:<item ref>`, `visit:<location slug>` — and the
hub advances them from the events it already handles (enemy defeats, buys/pickups, navigation). Turn-in
requires every objective and grants gold/experience through the existing `AddGold` / `GainExperience`
commands, so levelling logic is not duplicated.

**Ground items** live in the `GroundItems` table keyed by `(ZoneId, LocationSlug)`. `DropItem` remains a
permanent destroy; `DropItemToGround` is the "leave it on the floor" path, and pickups are validated
against the character's current location.

---

## Related

- [`docs/design-system.md`](design-system.md) — tokens, typography, and the retired Blazor appendix.
- [`plans/sveltekit-migration.md`](../plans/sveltekit-migration.md) — how the web app reached its current shape.
- [`.github/agent-memory/veldrath-memory.md`](../.github/agent-memory/veldrath-memory.md) — session-by-session notes on both pivots.
