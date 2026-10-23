# Web-First Game Client Roadmap

> **Status:** Active — Session-53 (2026-10-23)
> **Decision:** The browser game client (RCL hosted by `Veldrath.Web`) is the primary game
> experience. The Avalonia desktop client follows the behaviour and design decisions made here
> rather than driving them.

---

## Why web-first

- `Veldrath.GameClient.Components` is already the shared game UI. `Veldrath.Web` hosts it through
  `EmbeddedRoutes`, and the Avalonia client can reuse the same components/payloads.
- Web-first keeps one gameplay surface authoritative: hub payload shapes, action wiring, and UX
  decisions are validated in the browser before being mirrored on desktop.
- The desktop client remains a supported consumer, but new gameplay features should land in the
  RCL first (or in shared `Veldrath.GameClient.Core` code).

## Ground rules

1. **RCL/shared core first.** New hub payloads, state, and actions go into
   `Veldrath.GameClient.Core` / `Veldrath.GameClient.Components` before any Avalonia-specific code.
2. **Payload contracts are explicit.** Keep ref maps and full-item maps under different property
   names (see `InventoryLoaded`: `Equipped` vs `EquippedItems`).
3. **No dead buttons.** Every RCL action must invoke an existing hub method with the exact request
   shape the server binds.
4. **Tests at the RCL boundary.** bUnit tests should assert the hub method and payload for each
   sidebar/footer/overlay action, not just that the component renders.
5. **Desktop follows.** When a web behaviour is settled, port it to `Veldrath.Client` and add an
   Avalonia test.

---

## Completed — Session-53 (web action correctness)

- Removed the dead `/Game/Map` navigation link; the map is rendered inline on `/Game/Play`.
- Fixed RCL hub payloads:
  - `VisitShop` now sends `{ ZoneId }`.
  - `RestAtLocation` now sends `{ LocationId, CostInGold }` (sidebar and footer).
  - `NavigateToLocation` now sends `{ LocationSlug }` instead of a bare string.
  - `LeaveZone` replaced the removed `ExitZone` call and refreshes the map graph afterward.
- Added `HasInn` / `HasMerchant` to the `ZoneEntered` payload and `ZoneState`, so shop/rest
  actions render in the web client.
- Removed the footer's unbound combat `ActionBar`; `GameCombat` owns working combat actions.
- Added `WebGameActionTests` covering the above hub payloads and dead-route removal.

## Next priorities

### P0 — Web gameplay loop correctness

1. **Combat ability hotbar** — wire `ActionBar.OnUseAbility` to `UseAbilityInCombat`, build
   `HotbarAbility` slots from `GameState`, and show cooldown/availability.
2. **Death/respawn flow** — verify `Respawn` works outside and inside combat, and that the
   character sheet/no-zone states recover after defeat.
3. **Inventory/equipment browser verification** — ✅ Completed Session-53.
   - Equip routes shields to OffHand, weapons to MainHand, and armor/jewellery to their slots.
   - Drop sends the bare item slug accepted by `DropItem(string itemRef)`.
   - Buy/sell send the bare slugs accepted by `BuyItem(string itemRef)` / `SellItem(string itemRef)`.
   - The merchant catalog is requested after `ShopVisited` and rendered from the new `Catalog` field.
   - Shop/drop transactions refresh the bag/equipment via a background `InventoryLoaded` snapshot
     (`OpenOverlay = false`) so the current panel stays open.
   - Equipped items render in the equipment panel only; the bag list excludes them.
   - The dead `UseItem` button was removed (no matching hub method).
   - `WebInventoryInteractionTests` covers equip, drop, no-duplication, no-Use, buy, and sell.
4. **Zone transitions** — ensure `ZoneLeft` clears zone state (not just a system message), and that
   region-map travel/enter-zone flows update the footer/map consistently.

### P1 — Web UX and onboarding

5. **Character creation** — finish wizard interaction coverage (G119) and verify validation/error
   recovery on slow or failed hub/API calls.
6. **Game settings** — replace the blocked `GameSettings` test workaround and verify persistence
   through the web client.
7. **Chat** — channel tabs, whisper, ignore list, slash-command hints, and message filtering.
8. **Map panel** — layout refreshed Session-55: compact text cards, direction-labelled
   connectors, compacted authored coordinates, edge-first keyboard navigation, and a
   two-column map/detail layout. Remaining: screen-reader announcements for selection/travel and
   a final browser visual pass.

### P2 — Web-first gameplay features

9. **Crafting** — add a crafting overlay/action using `CraftItem` and `ItemCrafted`.
10. **Dungeons** — add a dungeon entry surface using `EnterDungeon` / `DungeonEntered`.
11. **Non-combat abilities** — surface `UseAbility` outside combat with mana/cooldown feedback.
12. **NPC dialogue/inspection** — add the payloads/UI if the server surfaces dialogue or inspect
    events.

### P3 — Desktop parity

13. Port settled web behaviours to `Veldrath.Client` (delete confirmation, inventory/equipment
    snapshot, action payloads) and add Avalonia tests.
14. Keep `plans/game-client-parity-checklist.md` current with any remaining desktop-only gaps.

---

## Verification checklist for each batch

- `dotnet build Veldrath.Web.slnx`
- `dotnet test Veldrath.GameClient.Components.Tests`
- `dotnet test Veldrath.GameClient.Core.Tests`
- For hub/payload changes: `dotnet test Veldrath.Server.Tests --filter <affected area>`
- Full suite before commit: `dotnet test Realm.Full.slnx --no-build`
