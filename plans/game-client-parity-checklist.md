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
- **Status:** 🟡 Partial
- **Details:** `GameChat.razor` is in the RCL and provides the core chat UI (message list, input field, send button). However:
  - Channel pills (Zone/Global/Whisper/System) are in the **desktop's** `GameViewModel` but NOT yet in the RCL `GameChat.razor`
  - Whisper `/w name` prefix parsing exists in the desktop client but not in the RCL
  - Desktop has `ChatMessageViewModel` with channel color mapping; RCL uses CSS classes for styling
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor)
- **Gap:** RCL chat component needs channel pill UI and whisper target input to match desktop parity.

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
- **Details:** `ActionBar.razor` shared component renders Attack/Defend/Flee/Respawn buttons with configurable visibility and disabled state.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Shared/ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor)
- **Notes:** Desktop has additional hotbar ability buttons (`HotbarSlotViewModel`) that are not present in the RCL yet.

### 9. Overlays (Inventory, Shop, Journal)
- **Status:** ❌ Missing
- **Details:** `GameOverlay.razor` is a generic panel wrapper in the RCL. However:
  - **Inventory:** Not yet implemented in RCL (desktop has `InventoryView` + `InventoryViewModel`)
  - **Shop:** Not yet implemented in RCL (desktop has shop interaction via `VisitShop` hub command)
  - **Journal/Quest log:** Not yet implemented in either (planned)
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/GameOverlay.razor)
- **Gap:** Need inventory, shop, and journal sub-components in the RCL.

### 10. Map (Region View)
- **Status:** ❌ Missing
- **Details:** Desktop has a full `MapViewModel` + `MapView.axaml` with graph layout (nodes = zones, edges = connections). The RCL has no map component yet.
- **Desktop files:** [`Veldrath.Client/ViewModels/MapViewModel.cs`](Veldrath.Client/ViewModels/MapViewModel.cs), [`Veldrath.Client/Views/MapView.axaml`](Veldrath.Client/Views/MapView.axaml)
- **Gap:** Need `GameMap.razor` component in the RCL to provide region map in both clients.

### 11. Settings (Audio, Display)
- **Status:** ❌ Missing (RCL) / ✅ Partial (Desktop)
- **Details:**
  - Desktop: Settings flyout in `GameView.axaml` with music mute, SFX mute, and server URL configuration
  - RCL: No settings component exists
- **Desktop files:** [`Veldrath.Client/ViewModels/GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs), [`Veldrath.Client/Views/GameView.axaml`](Veldrath.Client/Views/GameView.axaml)
- **Gap:** Need `GameSettings.razor` component in the RCL.

### 12. Server Status Banner
- **Status:** 🟡 Partial
- **Details:**
  - Desktop: Connection status dot (green/yellow/red) + status tooltip in header; offline banner in `MainWindow.axaml`
  - RCL: `GameFooter.razor` shows connection dot and "Connected"/"Disconnected" text. `GameHeader.razor` shows nothing.
- **RCL file:** [`Veldrath.GameClient.Components/Components/Pages/GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor)
- **Notes:** Desktop has richer status indication (degraded/warning states, ping-based detection).

### 13. Disconnect/Reconnect Handling
- **Status:** 🟡 Partial
- **Details:**
  - Desktop: Full reconnection state machine (`ServerConnectionService` with `ConnectionGuard` pattern, automatic retry via SignalR, server status polling every 5s/30s)
  - RCL: `GameHubConnectionService` has `RetryPolicy` and `Reconnecting`/`Reconnected` event handling. The RCL components (Game.razor) handle reconnection by re-registering handlers.
- **Core file:** [`Veldrath.GameClient.Core/Services/GameHubConnectionService.cs`](Veldrath.GameClient.Core/Services/GameHubConnectionService.cs)
- **Notes:** Core connection service supports reconnect, but the RCL UI could benefit from the desktop's richer feedback (degraded state, reconnect progress).

---

## Summary

| # | Feature | Status |
|---|---------|--------|
| 1 | Character Select | ✅ Parity |
| 2 | Create Character | ✅ Parity |
| 3 | Zone View | ✅ Parity |
| 4 | Movement | ✅ Parity |
| 5 | Chat | 🟡 Partial |
| 6 | Combat | ✅ Parity |
| 7 | Status Bars | ✅ Parity |
| 8 | Action Bar | ✅ Parity |
| 9 | Overlays (Inventory, Shop, Journal) | ❌ Missing |
| 10 | Map (Region View) | ❌ Missing |
| 11 | Settings | ❌ Missing (RCL) |
| 12 | Server Status | 🟡 Partial |
| 13 | Disconnect/Reconnect | 🟡 Partial |

**Totals:**
- ✅ Parity: 7 / 13
- 🟡 Partial: 3 / 13
- ❌ Missing: 3 / 13

---

## Next Steps

1. **Short-term (next session):** Enhance RCL `GameChat.razor` with channel pills and whisper support to match desktop parity
2. **Medium-term:** Create map component (`GameMap.razor`) in RCL to replace desktop's native `MapView`
3. **Medium-term:** Create inventory/shop/journal overlay components in RCL
4. **Long-term:** Migrate settings flyout from desktop to RCL
5. **Ongoing:** Add bUnit tests for each new RCL component as it's created
