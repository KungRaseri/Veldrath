# Game Client Feature Parity Checklist

> **Date:** 2026-07-01
> **Scope:** Track which features are identical between the web client (`Veldrath.Web` + `Veldrath.GameClient.Components`) and the desktop client (`Veldrath.Client` with embedded WebView2).
> **Status:** Initial audit after Phase 1-4 completion.

---

## Parity Status Key

| Icon | Meaning |
|------|---------|
| ✅ Parity | Feature is identical in both clients (shared RCL components) |
| 🟡 Partial | Feature exists in both but differs in implementation or capabilities |
| ❌ Missing | Feature is only available in one client |

---

## Checklist

### 1. Character Select
- **Status:** ✅ Parity
- **Details:** `CharacterSelect.razor` is in the RCL (`Veldrath.GameClient.Components`). Both web and desktop render the same card-based layout with character name, class, level, XP, and last-played time. Both use `IGameHubConnectionService` and `IGameApiClient` interfaces from `Veldrath.GameClient.Core`.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor`](Veldrath.GameClient.Components/Components/Pages/CharacterSelect.razor)
- **Notes:** Desktop auth is handled natively via `TokenStore`; the embedded server uses `EmbeddedAuthStateService` with a sentinel token. The character select experience is identical because both render the same RCL component.

### 2. Create Character
- **Status:** ✅ Parity
- **Details:** `CreateCharacter.razor` is in the RCL. Three-step wizard (Class → Name → Confirm) renders identically in both clients.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor`](Veldrath.GameClient.Components/Components/Pages/CreateCharacter.razor)
- **Notes:** Name availability checking via `IGameApiClient.CheckCharacterNameAsync` works the same way regardless of host.

### 3. Zone View (Tilemap)
- **Status:** ✅ Parity
- **Details:** `GameTilemap.razor` renders a CSS Grid-based tilemap in both clients. The desktop client renders it via WebView2, producing pixel-identical output.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor)
- **Fallback:** `ZoneLocationPanelView` (Avalonia native) available as degraded-mode fallback when WebView2 is unavailable on desktop.

### 4. Movement (Click-to-Move)
- **Status:** ✅ Parity
- **Details:** Both clients send `MoveCharacter` hub command on tile click via `IGameHubConnectionService.SendAsync`. Movement is blocked during combat in both.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor`](Veldrath.GameClient.Components/Components/Pages/GameTilemap.razor#L72-L76)
- **Notes:** The desktop previously used keyboard movement (WASD/arrow keys), which was removed in the reactive UI pivot (Session-41). Both clients now use pure point-and-click.

### 5. Chat (Zone, Global, Whisper, System)
- **Status:** ✅ Parity
- **Details:** `GameChat.razor` provides channel pill UI (Zone/Global/Whisper/System), whisper `/w name` prefix parsing, and channel color coding. All chat features are identical between web and desktop clients.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor)

### 6. Combat (Engage, Attack, Defend, Flee, Abilities)
- **Status:** ✅ Parity
- **Details:** `GameCombat.razor` renders enemy HP bar (via `StatusBar`), action buttons (Attack/Defend/Flee/Respawn via `ActionBar`), and combat log. Both send the same hub commands.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameCombat.razor`](Veldrath.GameClient.Components/Components/Pages/GameCombat.razor)
- **Notes:** Desktop previously used reactive commands directly; now both use the RCL's hub interface.

### 7. Status Bars (HP, MP, XP)
- **Status:** ✅ Parity
- **Details:** `StatusBar.razor` shared component renders HP/MP/XP bars with label, fill percentage, and current/max text. Used by `GameHeader` and `GameCombat`.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Shared/StatusBar.razor`](Veldrath.GameClient.Components/Components/Shared/StatusBar.razor)

### 8. Action Bar (Ability Buttons)
- **Status:** ✅ Parity
- **Details:** `ActionBar.razor` shared component renders Attack/Defend/Flee/Respawn buttons with configurable visibility and disabled state. The hotbar also includes 10 quick-slot ability buttons for rapid ability access.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Shared/ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor)

### 9. Overlays (Inventory, Shop, Journal)
- **Status:** ✅ Parity
- **Details:** Three dedicated overlay components exist in the RCL:
  - **Inventory:** [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) with item grid, equipment slots, and hub command wiring
  - **Shop:** [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) with buy/sell interface connected via `VisitShop` hub command
  - **Journal:** [`JournalOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/JournalOverlay.razor) with quest log and active quest tracking
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/GameOverlay.razor)

### 10. Map (Region View)
- **Status:** ✅ Parity
- **Details:** `GameMap.razor` renders a CSS Grid region map with zone cards showing zone name, danger level, and connected exits. Click-to-navigate sends `MoveCharacter` hub command on zone selection.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor)

### 11. Settings (Audio, Display)
- **Status:** ✅ Parity
- **Details:** `GameSettings.razor` page at `/Game/Settings` provides music volume slider, SFX volume slider, master volume, theme selector (light/dark/system), and accessibility options (font size, contrast mode, reduced motion). Both web and desktop offer identical settings via the RCL component.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameSettings.razor`](Veldrath.GameClient.Components/Components/Pages/GameSettings.razor)

### 12. Server Status Banner
- **Status:** ✅ Parity
- **Details:** `GameFooter.razor` displays connection state (green/yellow/red dot), ping latency, and online player count. `GameHeader.razor` shows the server status banner with degraded/warning state indicators. Both clients render identical status information.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor)

### 13. Disconnect/Reconnect Handling
- **Status:** ✅ Parity
- **Details:** `ReconnectOverlay.razor` provides a full reconnection UI with countdown auto-retry, manual reconnect button, and connection progress indication. `GameHubConnectionService` in Core handles SignalR retry policy, `Reconnecting`/`Reconnected` events, and handler re-registration. Both clients share identical reconnect behavior.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Shared/ReconnectOverlay.razor`](Veldrath.GameClient.Components/Components/Shared/ReconnectOverlay.razor)
- **Core file:** [`Veldrath.GameClient.Core/Services/GameHubConnectionService.cs`](Veldrath.GameClient.Core/Services/GameHubConnectionService.cs)

---

## Summary

| # | Feature | Status |
|---|---------|--------|
| 1 | Character Select | ✅ Parity |
| 2 | Create Character | ✅ Parity |
| 3 | Zone View | ✅ Parity |
| 4 | Movement | ✅ Parity |
| 5 | Chat | ✅ Parity |
| 6 | Combat | ✅ Parity |
| 7 | Status Bars | ✅ Parity |
| 8 | Action Bar | ✅ Parity |
| 9 | Overlays (Inventory, Shop, Journal) | ✅ Parity |
| 10 | Map (Region View) | ✅ Parity |
| 11 | Settings | ✅ Parity |
| 12 | Server Status | ✅ Parity |
| 13 | Disconnect/Reconnect | ✅ Parity |

**Totals:**
- **13/13 ✅ Full Parity** — All features unified

---

## Update — 2026-07-01

All 13 features have been brought to full parity. Remaining gaps from [`game-client-unification-gaps.md`](game-client-unification-gaps.md) have been resolved:
- **Chat** (🟡→✅): Channel pills + whisper parsing + color coding added to `GameChat.razor`
- **Server Status Banner** (🟡→✅): Connection states + ping + player count added to `GameFooter.razor`
- **Disconnect/Reconnect** (🟡→✅): `ReconnectOverlay.razor` with countdown auto-retry + manual reconnect
- **Action Bar Hotbar** (🟡→✅): 10 hotbar ability quick-slot buttons added to `ActionBar.razor`
- **Map (Region View)** (❌→✅): `GameMap.razor` with CSS grid zone cards + click-to-navigate
- **Overlays (Inventory, Shop, Journal)** (❌→✅): `InventoryOverlay.razor`, `ShopOverlay.razor`, `JournalOverlay.razor` with hub command wiring
- **Settings (Audio, Display)** (❌→✅): `GameSettings.razor` page at `/Game/Settings` with volume sliders, theme, accessibility options
