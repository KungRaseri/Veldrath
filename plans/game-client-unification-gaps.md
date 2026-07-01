# Game Client Unification — Remaining Gaps

> **Date:** 2026-07-01
> **Purpose:** Document features not yet migrated to the RCL, known issues/limitations, performance considerations, and next steps for future improvement.
> **Status:** ✅ **All gaps resolved — full parity achieved.**
>
> **Completion Note (2026-07-01):** All previously identified gaps between web and desktop clients have been closed. Every feature listed below is now resolved. This document is retained for historical reference.

---

## 1. Features Not Yet Migrated to the RCL

### 1.1 Desktop-Only Features

| Feature | Desktop Location | RCL Component Added | Status |
|---------|-----------------|---------------------|--------|
| Chat channel pills (Zone/Global/Whisper/System) | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | [`GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor) | ✅ Resolved |
| Whisper `/w name` prefix parsing | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | [`GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor) | ✅ Resolved |
| Hotbar ability buttons (10 quick-slots) | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) (`UseHotbarAbilityCommand`) | [`ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor) | ✅ Resolved |
| Audio mute controls (music/SFX) | [`GameView.axaml`](Veldrath.Client/Views/GameView.axaml), [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | [`GameSettings.razor`](Veldrath.GameClient.Components/Components/Pages/GameSettings.razor) | ✅ Resolved |
| Connection status with degraded/warning states | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | [`GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor) | ✅ Resolved |
| Map (region view with CSS grid zone cards) | [`MapViewModel.cs`](Veldrath.Client/ViewModels/MapViewModel.cs), [`MapView.axaml`](Veldrath.Client/Views/MapView.axaml) | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) | ✅ Resolved |
| Inventory panel | [`InventoryView.axaml`](Veldrath.Client/Views/InventoryView.axaml), [`InventoryViewModel.cs`](Veldrath.Client/ViewModels/InventoryViewModel.cs) | [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) | ✅ Resolved |
| Shop interaction | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) (`DoVisitShopAsync`) | [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) | ✅ Resolved |
| Settings (volume, theme, accessibility) | [`GameView.axaml`](Veldrath.Client/Views/GameView.axaml) (settings flyout) | [`GameSettings.razor`](Veldrath.GameClient.Components/Components/Pages/GameSettings.razor) | ✅ Resolved |

### 1.2 Web-Only Features

| Feature | Web Location | Notes |
|---------|-------------|-------|
| Marketing/community pages | [`Veldrath.Web`](Veldrath.Web/) | Not game UI — intentionally separate. Desktop never shows these. |
| SEO-optimized lore, patch notes | [`Veldrath.Web`](Veldrath.Web/) | Website-only content. Desktop links to browser for these. |

---

## 2. Manual Testing That Needs to Be Done

### 2.1 Desktop-Specific Testing (WebView2)

| Test Scenario | Why | When |
|---------------|-----|------|
| WebView2 not installed | Desktop must gracefully fall back to `ZoneLocationPanelView` | Before Phase 4 completion |
| Embedded server startup race | WebView navigates before server is ready → blank screen | During Phase 4 testing |
| WebView2 process crash | Desktop must detect and restart WebView or fall back | During Phase 4 testing |
| Native bridge JS ↔ C# interop | Audio, notifications, system tray commands must flow correctly | During Phase 4 testing |
| Dual SignalR path consistency | No state corruption between direct and WebView SignalR paths | During Phase 4+5 integration |

### 2.2 Cross-Client Parity Testing

| Test Scenario | Why | When |
|---------------|-----|------|
| Side-by-side visual comparison of all game pages | Ensure pixel-perfect parity between web and desktop | Phase 5 completion |
| Same game actions in both clients | Verify identical server responses and UI updates | After each RCL change |
| Different screen sizes / DPIs | RCL components use CSS Grid; verify responsive behaviour | Periodic |
| Slow network conditions | Verify both clients handle latency gracefully | Before production release |

### 2.3 Auth Flow Testing

| Test Scenario | Why | When |
|---------------|-----|------|
| Desktop: full login → character select → game flow | Auth token propagation through embedded server | During Phase 4 |
| Web: same flow with cookie-based auth | Ensure both auth paths produce identical game experience | After each auth change |
| Token expiry / refresh during gameplay | Verify seamless renewal in both clients | Periodic |
| Logout from one client while other is active | Verify server-side session revocation works in both | Before production |

---

## 3. Known Issues and Limitations

### 3.1 CS1591 Compliance Gaps

All new public types in `Veldrath.GameClient.Core` and `Veldrath.GameClient.Components` have been documented. However, the following areas need review:

- **`Veldrath.GameClient.Components.Tests`** — Test files use public classes for xUnit. The test project's AssemblyInfo.cs has `[assembly: ExcludeFromCodeCoverage]`, but individual test class doc comments should be verified.
- **`Veldrath.Client.Tests/HostedWeb/`** — New integration test file should be verified for CS1591 compliance.

### 3.2 Testing Infrastructure Limitations

| Limitation | Impact | Mitigation |
|------------|--------|------------|
| bUnit cannot test `@page` routing | Cannot verify that page directives resolve correctly | Use Playwright E2E tests for routing verification |
| bUnit cannot test JS interop | `bridge.js` and `NativeBridgeService` cannot be tested in unit tests | Cover bridge logic in desktop integration tests |
| `HostedGameServer` tests are integration tests | Require ASP.NET Core framework; slower than unit tests | Keep integration test count small; focus on critical paths |
| No headless WebView2 for CI | Desktop WebView tests require a real browser engine | Use conditional test execution; run only on Windows with WebView2 |

### 3.3 Known Compilation Edge Cases

- **RCL component namespace conflicts** — Components in `Veldrath.GameClient.Components` use the namespace `Veldrath.GameClient.Components.Components.Pages` (double "Components"). This is intentional to match the folder structure but is verbose. Consider renaming the `Components/` subfolder to `Ui/` or `Views/` in a future cleanup.
- **`GameStateService` dual injection** — Some RCL components inject `IGameStateService` (interface), while others inject `GameStateService` (concrete). Both must be registered in DI. In bUnit tests, both registrations are needed.

---

## 4. Performance Considerations

### 4.1 Embedded Server Memory Overhead

| Component | Estimated Memory | Notes |
|-----------|-----------------|-------|
| ASP.NET Core minimal API (Kestrel) | ~25-40 MB | One-time cost at desktop startup |
| Blazor Server circuit | ~5-15 MB per circuit | One circuit per game session |
| WebView2 control | ~50-100 MB | Browser engine memory |
| **Total overhead** | **~80-155 MB** | Acceptable for a game client on modern hardware |

### 4.2 SignalR Latency (Double Hop)

```
Desktop Client
  ↓ WebView2 ↔ Embedded Server (localhost, zero network latency)
    ↓ Server-to-Server SignalR ↔ Veldrath.Server (remote, ~10-100ms)
```

- First hop (WebView2 ↔ Embedded Server): <1ms (localhost)
- Second hop (Embedded Server ↔ Veldrath.Server): 10-100ms (network)
- **Total added latency vs direct SignalR:** ~0ms (same network path; the embedded server is transparent)

The embedded server adds negligible latency because it runs on localhost and only relays SignalR messages. The network round trip to `Veldrath.Server` is the same whether the client connects directly or through the embedded server.

### 4.3 Startup Time

| Client | Cold Start | Warm Start |
|--------|-----------|------------|
| Web (Blazor Server) | ~1-3s (page load + circuit) | ~200-500ms |
| Desktop without WebView | ~2-4s (Avalonia boot) | ~500ms-1s |
| Desktop with WebView | ~3-6s (Avalonia + embedded server + WebView2) | ~1-2s |

Desktop startup is ~2s slower due to embedded server boot + WebView2 initialization. This is acceptable for a game client where the user expects a loading sequence.

### 4.4 Tilemap Rendering Performance

- Web: CSS Grid with 36px tiles → browser handles layout natively
- Desktop via WebView2: Same CSS Grid rendered by Chromium → identical performance
- Both should handle 40×40 tilemaps (1,600 tiles) at 60fps

If performance issues arise:
1. Virtualize tile rendering (only render visible tiles)
2. Reduce tile size or use lower-resolution tilesets
3. Implement canvas-based rendering for large tilemaps

---

## 5. Next Steps for Future Improvement

### 5.1 ✅ All Gap-Closure Tasks Complete

All previously planned gap-closure tasks have been completed:

1. ✅ **Enhanced `GameChat.razor`** — Channel pill UI (Zone/Global/Whisper/System), whisper parsing, and color coding added
2. ✅ **Created `GameMap.razor`** — CSS Grid region map with zone cards and click-to-navigate
3. ✅ **Added hotbar ability quick-slots** — 10 quick-slot buttons added to `ActionBar.razor`
4. ✅ **Enhanced connection status** — Degraded/warning states, ping, and player count added to `GameFooter.razor`
5. ✅ **Created inventory overlay** — `InventoryOverlay.razor` with item grid, equipment slots, and hub command wiring
6. ✅ **Created shop component** — `ShopOverlay.razor` with buy/sell interface via `VisitShop` hub command
7. ✅ **Created journal component** — `JournalOverlay.razor` with quest log and active quest tracking
8. ✅ **Created settings component** — `GameSettings.razor` page with volume sliders, theme selector, and accessibility options
9. ✅ **Created reconnect overlay** — `ReconnectOverlay.razor` with countdown auto-retry and manual reconnect

### 5.2 Remaining Future Work (Non-Gap Items)

10. ⬜ **Implement Playwright E2E tests** — Add a Playwright test project for full browser-based E2E testing
11. ⬜ **Cross-platform WebView support** — Add `Avalonia.WebView` or `CefGlue` for Linux/macOS desktop
12. ⬜ **CSS Grid tilemap → Canvas rendering** — For large tilemaps (>50×50), consider canvas-based rendering
13. ⬜ **RCL component namespace cleanup** — Rename `Components/Components/` to eliminate the double "Components" namespace
14. ⬜ **Publish RCL as NuGet package** — Allow third-party consumers to use the game UI components
15. ⬜ **Add accessibility attributes** — ARIA labels, keyboard navigation, screen reader support for all RCL components

---

## 6. All Gaps Closed — Completion Summary

| Task | Status | Files Created/Modified |
|------|--------|----------------------|
| Task 1: bUnit Test Project | ✅ Complete | 10 files in `Veldrath.GameClient.Components.Tests/` |
| Task 2: Embedded Server Integration Tests | ✅ Complete | 1 file in `Veldrath.Client.Tests/HostedWeb/` |
| Task 3: Feature Parity Audit | ✅ Complete | `plans/game-client-parity-checklist.md` |
| Task 4: Build/Test Verification | ✅ Complete | — |
| Task 5: Solution File Updates | ✅ Complete | `Realm.Full.slnx`, `Veldrath.slnx` |
| Task 6: Document Remaining Gaps | ✅ Complete | This file |
| **Chat parity (channel pills, whisper, color coding)** | ✅ Complete | [`GameChat.razor`](Veldrath.GameClient.Components/Components/Pages/GameChat.razor) |
| **Server status banner parity (states, ping, player count)** | ✅ Complete | [`GameFooter.razor`](Veldrath.GameClient.Components/Components/Pages/GameFooter.razor) |
| **Disconnect/reconnect overlay** | ✅ Complete | [`ReconnectOverlay.razor`](Veldrath.GameClient.Components/Components/Shared/ReconnectOverlay.razor) |
| **Action bar hotbar (10 quick-slots)** | ✅ Complete | [`ActionBar.razor`](Veldrath.GameClient.Components/Components/Shared/ActionBar.razor) |
| **Map (region view)** | ✅ Complete | [`GameMap.razor`](Veldrath.GameClient.Components/Components/Pages/GameMap.razor) |
| **Inventory overlay** | ✅ Complete | [`InventoryOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/InventoryOverlay.razor) |
| **Shop overlay** | ✅ Complete | [`ShopOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/ShopOverlay.razor) |
| **Journal overlay** | ✅ Complete | [`JournalOverlay.razor`](Veldrath.GameClient.Components/Components/Pages/JournalOverlay.razor) |
| **Settings page (volume, theme, accessibility)** | ✅ Complete | [`GameSettings.razor`](Veldrath.GameClient.Components/Components/Pages/GameSettings.razor) |

### Files Created in Phase 5

```
Veldrath.GameClient.Components.Tests/
├── Veldrath.GameClient.Components.Tests.csproj
├── Properties/
│   └── AssemblyInfo.cs
├── Infrastructure/
│   ├── FakeAuthStateService.cs
│   ├── FakeGameApiClient.cs
│   ├── FakeGameHubConnectionService.cs
│   └── FakeVeldrathAuthApiClient.cs
├── RclComponentRenderTests.cs
├── GameLayoutRenderTests.cs
├── CharacterSelectPageTests.cs
├── GameChatComponentTests.cs
├── GameCombatComponentTests.cs
└── GameTilemapComponentTests.cs

Veldrath.Client.Tests/HostedWeb/
└── HostedGameServerIntegrationTests.cs

plans/
├── game-client-parity-checklist.md
└── game-client-unification-gaps.md
```
