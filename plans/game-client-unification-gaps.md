# Game Client Unification — Remaining Gaps

> **Date:** 2026-07-01
> **Purpose:** Document features not yet migrated to the RCL, known issues/limitations, performance considerations, and next steps for future improvement.
> **Status:** Post-Phase 5 audit.

---

## 1. Features Not Yet Migrated to the RCL

### 1.1 Desktop-Only Features

| Feature | Desktop Location | RCL Component Needed | Priority |
|---------|-----------------|---------------------|----------|
| Chat channel pills (Zone/Global/Whisper/System) | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | Enhance `GameChat.razor` | High |
| Whisper `/w name` prefix parsing | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | Enhance `GameChat.razor` | High |
| Hotbar ability buttons | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) (`UseHotbarAbilityCommand`) | New `GameHotbar.razor` component | Medium |
| Audio mute controls (music/SFX) | [`GameView.axaml`](Veldrath.Client/Views/GameView.axaml), [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | New `GameSettings.razor` component | Medium |
| Connection status with degraded/warning states | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) | Enhance RCL status indicators | Medium |
| Map (region view with graph layout) | [`MapViewModel.cs`](Veldrath.Client/ViewModels/MapViewModel.cs), [`MapView.axaml`](Veldrath.Client/Views/MapView.axaml) | New `GameMap.razor` component | Medium |
| Inventory panel | [`InventoryView.axaml`](Veldrath.Client/Views/InventoryView.axaml), [`InventoryViewModel.cs`](Veldrath.Client/ViewModels/InventoryViewModel.cs) | New `GameInventory.razor` component | Low |
| Shop interaction | [`GameViewModel.cs`](Veldrath.Client/ViewModels/GameViewModel.cs) (`DoVisitShopAsync`) | New `GameShop.razor` component | Low |
| Settings flyout | [`GameView.axaml`](Veldrath.Client/Views/GameView.axaml) (settings flyout) | New `GameSettings.razor` component | Low |

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

### 5.1 Immediate (Next Session)

1. **Enhance `GameChat.razor`** — Add channel pill UI (Zone/Global/Whisper/System) and whisper target input to match desktop feature set
2. **Run full test suite** — `dotnet test Realm.Full.slnx` and fix any failures
3. **Verify bUnit tests pass** — Run `dotnet test Veldrath.GameClient.Components.Tests`

### 5.2 Short-Term (Next 2-3 Sessions)

4. **Create `GameMap.razor`** — Port the desktop's region map (graph with zone nodes and connections) to the RCL
5. **Create `GameHotbar.razor`** — Add ability hotbar to the RCL so both clients have quick-slot abilities
6. **Enhance connection status** — Add degraded/warning states to RCL's `GameFooter` to match desktop

### 5.3 Medium-Term (Next 4-6 Sessions)

7. **Create inventory overlay** — Port `InventoryViewModel` to a new RCL component
8. **Create shop component** — Port shop interaction to RCL
9. **Create settings component** — Port audio/display settings flyout to RCL
10. **Implement Playwright E2E tests** — Add a Playwright test project for full browser-based E2E testing

### 5.4 Long-Term (Future Milestone)

11. **Cross-platform WebView support** — Add `Avalonia.WebView` or `CefGlue` for Linux/macOS desktop
12. **CSS Grid tilemap → Canvas rendering** — For large tilemaps (>50×50), consider canvas-based rendering
13. **RCL component namespace cleanup** — Rename `Components/Components/` to eliminate the double "Components" namespace
14. **Publish RCL as NuGet package** — Allow third-party consumers to use the game UI components
15. **Add accessibility attributes** — ARIA labels, keyboard navigation, screen reader support for all RCL components

---

## 6. Phase 5 Completion Summary

| Task | Status | Files Created/Modified |
|------|--------|----------------------|
| Task 1: bUnit Test Project | ✅ Complete | 10 files in `Veldrath.GameClient.Components.Tests/` |
| Task 2: Embedded Server Integration Tests | ✅ Complete | 1 file in `Veldrath.Client.Tests/HostedWeb/` |
| Task 3: Feature Parity Audit | ✅ Complete | `plans/game-client-parity-checklist.md` |
| Task 4: Build/Test Verification | ⏳ Pending | — |
| Task 5: Solution File Updates | ⏳ Pending | `Realm.Full.slnx`, `Veldrath.slnx` |
| Task 6: Document Remaining Gaps | ✅ Complete | This file |

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
